using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.Logic
{
    public static partial class ClassicElevenLabs
    {
        public static readonly string[] Models = { "eleven_v4_turbo", "eleven_v4", "eleven_turbo_v2_5", "eleven_v3", "eleven_multilingual_v2" };
        public static string LanguageCode(string value, string model)
        {
            foreach (var pair in Languages(model)) if (pair[0].Equals(value, StringComparison.OrdinalIgnoreCase) || pair[1].Equals(value, StringComparison.OrdinalIgnoreCase)) return pair[1];
            return value == "cz" ? "cs" : value == "ph" ? "fil" : value == "kr" ? "ko" : "";
        }
        private static readonly Regex Break = new Regex("<break\\s+time\\s*=\\s*[\"']?(?<value>\\d+(?:\\.\\d+)?)\\s*(?<unit>ms|s)[\"']?\\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static string ConvertPauses(string text) => Break.Replace(text, m => {
            var seconds = double.Parse(m.Groups["value"].Value, CultureInfo.InvariantCulture);
            if (m.Groups["unit"].Value.Equals("ms", StringComparison.OrdinalIgnoreCase)) seconds /= 1000;
            return seconds < .75 ? "[short pause]" : seconds <= 1.5 ? "[pause]" : "[long pause]";
        });
        public static JObject BuildBody(string text, string voice, string model, string language, double stability, double similarity, double speed)
        {
            model = string.IsNullOrWhiteSpace(model) ? "eleven_v4_turbo" : model;
            if (language == "cz") language = "cs"; if (language == "ph") language = "fil"; if (language == "kr") language = "ko";
            text = Utilities.UnbreakLine(text);
            if (model == "eleven_v3" || model == "eleven_v4" || model == "eleven_v4_turbo") text = ConvertPauses(text);
            JObject body;
            if (model == "eleven_v3") body = new JObject { ["inputs"] = new JArray(new JObject { ["text"] = text, ["voice_id"] = voice }), ["settings"] = new JObject { ["stability"] = stability } };
            else
            {
                body = new JObject { ["text"] = text, ["model_id"] = model, ["voice_settings"] = new JObject { ["stability"] = stability, ["similarity_boost"] = similarity, ["speed"] = speed } };
                if (text.IndexOf("<break", StringComparison.OrdinalIgnoreCase) >= 0) body["enable_ssml_parsing"] = true;
            }
            if (!string.IsNullOrEmpty(language) && (model == "eleven_v3" || model == "eleven_v4" || model == "eleven_v4_turbo" || model == "eleven_turbo_v2_5")) body["language_code"] = language;
            return body;
        }
        public static async Task<byte[]> SpeakAsync(System.Net.Http.HttpClient client, string text, string voice, string model, string language, string key, double stability, double similarity, double speed, CancellationToken cancel)
        {
            model = string.IsNullOrWhiteSpace(model) ? "eleven_v4_turbo" : model;
            var uri = model == "eleven_v3" ? "https://api.elevenlabs.io/v1/text-to-dialogue" : "https://api.elevenlabs.io/v1/text-to-speech/" + Uri.EscapeDataString(voice);
            var body = BuildBody(text, voice, model, language, stability, similarity, speed).ToString(Newtonsoft.Json.Formatting.None);
            for (int attempt = 0; ; attempt++)
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
                {
                    request.Headers.TryAddWithoutValidation("xi-api-key", key.Trim());
                    request.Headers.TryAddWithoutValidation("accept", "audio/mpeg");
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    using (var response = await client.SendAsync(request, cancel).ConfigureAwait(false))
                    {
                        if ((int)response.StatusCode == 429 && attempt < 2) { await Task.Delay(attempt == 0 ? 2753 : 5707, cancel).ConfigureAwait(false); continue; }
                        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("ElevenLabs HTTP " + (int)response.StatusCode + ". Check your API key, account limits and selected model.");
                        var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false); cancel.ThrowIfCancellationRequested();
                        if (bytes.Length == 0) throw new InvalidOperationException("ElevenLabs returned empty audio.");
                        return bytes;
                    }
                }
            }
        }
    }
}
