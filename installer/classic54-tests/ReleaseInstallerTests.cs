using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Logic;
namespace Classic54Tests
{
 public class ReleaseInstallerTests
 {
  private const string Name="SubtitleEdit-Classic-4.0.29-Setup.exe";
  private static CheckForUpdatesHelper Release(string assets=null)
  {
   var release=new CheckForUpdatesHelper();
   var prefix=CheckForUpdatesHelper.RepositoryUrl+"/releases/download/v4.0.29/";
   release.ReadStableRelease("{\"tag_name\":\"v4.0.29\",\"draft\":false,\"prerelease\":false,\"assets\":"+(assets??"[{\"name\":\""+Name+"\",\"browser_download_url\":\""+prefix+Name+"\"},{\"name\":\"SHA256SUMS.txt\",\"browser_download_url\":\""+prefix+"SHA256SUMS.txt\"}]")+"}");
   return release;
  }
  private sealed class Handler:HttpMessageHandler
  {
   public byte[] Content=Encoding.UTF8.GetBytes("offline installer fixture");
   public string Sums; public HttpStatusCode Status=HttpStatusCode.OK; public bool RequestedInstaller;
   public Handler(){using(var sha=SHA256.Create()) Sums=FixtureHex.To(sha.ComputeHash(Content))+"  "+Name+"\n";}
   protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel)
   {
    cancel.ThrowIfCancellationRequested();
    var sums=request.RequestUri.AbsolutePath.EndsWith("SHA256SUMS.txt");
    RequestedInstaller |= !sums;
    return Task.FromResult(new HttpResponseMessage(Status){Content=sums?(HttpContent)new StringContent(Sums):new ByteArrayContent(Content)});
   }
  }
  [Fact] public void VerifiedInstallerMatchesReleaseChecksum()
  {
   using(var handler=new Handler())
   {
    var file=ClassicReleaseInstaller.Download(Release(),CancellationToken.None,null,handler).GetAwaiter().GetResult();
    try { Assert.Equal(handler.Content,File.ReadAllBytes(file));Assert.True(handler.RequestedInstaller); }
    finally { File.Delete(file);Directory.Delete(Path.GetDirectoryName(file)); }
   }
  }
  [Fact] public void Utf8BomChecksumFileIsAccepted()
  {
   using(var handler=new Handler())
   {
    handler.Sums="\ufeff"+handler.Sums;
    var file=ClassicReleaseInstaller.Download(Release(),CancellationToken.None,null,handler).GetAwaiter().GetResult();
    try{Assert.Equal(handler.Content,File.ReadAllBytes(file));}finally{File.Delete(file);Directory.Delete(Path.GetDirectoryName(file));}
   }
  }
  [Fact] public void CorruptedInstallerIsRejected()
  {
   using(var handler=new Handler()){handler.Content=new byte[]{0,1,2};Assert.Throws<InvalidDataException>(()=>ClassicReleaseInstaller.Download(Release(),CancellationToken.None,null,handler).GetAwaiter().GetResult());}
  }
  [Fact] public void MissingChecksumNeverDownloadsInstaller()
  {
   using(var handler=new Handler()){handler.Sums=new string('a',64)+"  unrelated.exe";Assert.Throws<InvalidDataException>(()=>ClassicReleaseInstaller.Download(Release(),CancellationToken.None,null,handler).GetAwaiter().GetResult());Assert.False(handler.RequestedInstaller);}
  }
  [Fact] public void CancelledChecksumRequestNeverDownloadsInstaller()
  {
   using(var handler=new Handler()) using(var cancel=new CancellationTokenSource()){cancel.Cancel();Assert.Throws<OperationCanceledException>(()=>ClassicReleaseInstaller.Download(Release(),cancel.Token,null,handler).GetAwaiter().GetResult());Assert.False(handler.RequestedInstaller);}
  }
  [Fact] public void HttpFailureIsNotTreatedAsInstaller()
  {
   using(var handler=new Handler()){handler.Status=HttpStatusCode.NotFound;Assert.Throws<HttpRequestException>(()=>ClassicReleaseInstaller.Download(Release(),CancellationToken.None,null,handler).GetAwaiter().GetResult());Assert.False(handler.RequestedInstaller);}
  }
  [Fact] public void ExternalReleaseAssetIsRejectedAtBoundary()
  {
   var release=Release("[{\"name\":\""+Name+"\",\"browser_download_url\":\"https://example.org/"+Name+"\"}]");Assert.Null(release.InstallerUrl);Assert.Null(release.ChecksumsUrl);
  }
  [Fact] public void ExcessiveChecksumFileIsRejectedBeforeInstaller()
  {
   using(var handler=new Handler()){handler.Sums=new string('a',1024*1024+1);Assert.Throws<InvalidDataException>(()=>ClassicReleaseInstaller.Download(Release(),CancellationToken.None,null,handler).GetAwaiter().GetResult());Assert.False(handler.RequestedInstaller);}
  }
 }
}
