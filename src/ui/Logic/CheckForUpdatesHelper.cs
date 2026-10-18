using System;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic.Plugins;

namespace Nikse.SubtitleEdit.Logic
{
    public class CheckForUpdatesHelper
    {
        public const string RepositoryUrl="https://github.com/BrainDeLook/subtitleedit-classic";
        public const string StableReleaseApi="https://api.github.com/repos/BrainDeLook/subtitleedit-classic/releases/latest";
        public const string LatestStableUrl=RepositoryUrl+"/releases/latest";
        private readonly HttpMessageHandler _handler;
        public string Error { get; set; }
        public int PluginUpdates { get; set; }
        public bool Done { get; private set; }
        public bool ManualCheck { get; set; }
        public string LatestVersionNumber { get; set; }
        public string LatestChangeLog { get; set; }
        public string ReleasePageUrl { get; private set; }=LatestStableUrl;

        public CheckForUpdatesHelper() { }
        public CheckForUpdatesHelper(HttpMessageHandler handler) { _handler = handler; }

        private static HttpMessageHandler CreateHandler()
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            var settings = Configuration.Settings.Proxy;
            if (!string.IsNullOrWhiteSpace(settings.ProxyAddress))
            {
                var proxy = new WebProxy(settings.ProxyAddress);
                if (settings.UseDefaultCredentials) proxy.Credentials = CredentialCache.DefaultNetworkCredentials;
                else if (!string.IsNullOrWhiteSpace(settings.UserName)) proxy.Credentials = new NetworkCredential(settings.UserName, settings.Password, settings.Domain);
                handler.Proxy = proxy; handler.UseProxy = true;
            }
            return handler;
        }

        public static Version ParseStableVersion(string tag)
        {
            var match=Regex.Match(tag??"",@"^[vV]?(\d+\.\d+\.\d+(?:\.\d+)?)$");
            if(!match.Success || !Version.TryParse(match.Groups[1].Value,out var version))throw new FormatException("Invalid Classic stable release version.");
            return new Version(version.Major,version.Minor,Math.Max(0,version.Build),Math.Max(0,version.Revision));
        }
        public void ReadStableRelease(string json)
        {
            var release=JObject.Parse(json);
            if((bool?)release["draft"]!=false || (bool?)release["prerelease"]!=false)throw new FormatException("The Classic update endpoint did not return a stable release.");
            var tag=(string)release["tag_name"];var version=ParseStableVersion(tag);
            LatestVersionNumber=version.ToString();
            ReleasePageUrl=RepositoryUrl+"/releases/tag/"+Uri.EscapeDataString(tag);
            LatestChangeLog="Subtitle Edit Classic "+tag+Environment.NewLine+Environment.NewLine+((string)release["body"]??"");
        }
        public async Task CheckForUpdates(bool manualCheck)
        {
            ManualCheck=manualCheck;Done=false;Error=null;PluginUpdates=0;LatestVersionNumber=null;LatestChangeLog=null;ReleasePageUrl=LatestStableUrl;
            try
            {
                using(var http=new HttpClient(_handler ?? CreateHandler(), _handler == null))
                {
                    http.Timeout=TimeSpan.FromSeconds(15);
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("SubtitleEdit-Classic/4.0");
                    http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                    using(var response=await http.GetAsync(StableReleaseApi).ConfigureAwait(false))
                    {
                        if (response.StatusCode == HttpStatusCode.Forbidden || (int)response.StatusCode == 429)
                        {
                            // Public /latest uses GitHub's stable channel without the API rate limit.
                            using (var latest = await http.GetAsync(LatestStableUrl).ConfigureAwait(false))
                            {
                                var location = latest.Headers.Location;
                                if (location != null && !location.IsAbsoluteUri) location = new Uri(new Uri(RepositoryUrl + "/"), location);
                                var prefix = RepositoryUrl + "/releases/tag/";
                                if ((latest.StatusCode != HttpStatusCode.Redirect && latest.StatusCode != HttpStatusCode.MovedPermanently) || location == null || !location.AbsoluteUri.StartsWith(prefix, StringComparison.Ordinal))
                                    throw new HttpRequestException("The Classic stable release could not be checked. Try again later.");
                                var tag = Uri.UnescapeDataString(location.AbsoluteUri.Substring(prefix.Length));
                                ParseStableVersion(tag);
                                ReadStableRelease(new JObject { ["tag_name"] = tag, ["draft"] = false, ["prerelease"] = false, ["body"] = "Release notes: " + location.AbsoluteUri }.ToString());
                            }
                        }
                        else
                        {
                            if (!response.IsSuccessStatusCode) throw new HttpRequestException("Classic update check failed (HTTP " + (int)response.StatusCode + ").");
                            ReadStableRelease(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                        }
                    }
                }
                // Plugin metadata is independent of the application's release channel.
                // Its failure must not hide a successfully checked Classic release.
                try
                {
                    var installed=new InstalledPluginMetadataProvider().GetPlugins();
                    if(installed.Count>0){var online=new OnlinePluginMetadataProvider(PluginsGet.PluginXmlFileUrl).GetPlugins();PluginUpdates=PluginUpdateChecker.GetAvailableUpdates(installed,online.ToArray()).Count;}
                }
                catch{PluginUpdates=0;}
            }
            catch(Exception exception){Error=exception.Message;}
            finally{Done=true;}
        }
        public bool IsUpdateAvailable()=>string.IsNullOrEmpty(Error) && (PluginUpdates>0 || IsNewSubtitleEditAvailable());
        public bool IsNewSubtitleEditAvailable()=>string.IsNullOrEmpty(Error) && IsNewer(LatestVersionNumber,typeof(CheckForUpdatesHelper).Assembly.GetName().Version.ToString());
        public static bool IsNewer(string latest,string installed)
        {
            try{return ParseStableVersion(latest)>ParseStableVersion(installed);}catch(FormatException){return false;}
        }
    }
}
