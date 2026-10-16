using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Nikse.SubtitleEdit.Logic.Ocr.ClassicLens;

namespace Nikse.SubtitleEdit.Logic.Ocr
{
    public sealed class ClassicLensSharp : IDisposable
    {
        private readonly HttpClient _http;
        public ClassicLensSharp(HttpMessageHandler handler = null)
        {
            _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 32 * 1024 * 1024 };
        }
        public async Task<string> Ocr(byte[] png, int width, int height, string language, CancellationToken cancel)
        {
            // This is the public Lens client identity used by stable 5.3, not a user API key.
            var requestBytes = new LensProtoRequest { ImageBytes = png, Width = width, Height = height, RequestId = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0).ToString(CultureInfo.InvariantCulture), TargetLanguage = LanguageCode(language) }.Serialize();
            for (var attempt = 0; ; attempt++)
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, "https://lensfrontend-pa.googleapis.com/v1/crupload") { Content = new ByteArrayContent(requestBytes) })
                {
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
                    request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", "AIzaSyDr2UxVnv_U85AbhhY8XSHSIavUW0DC-sY");
                    request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                    request.Headers.TryAddWithoutValidation("Accept", "*/*");
                    request.Headers.TryAddWithoutValidation("Accept-Language", LanguageCode(language) + ",en;q=0.9");
                    using (var response = await _http.SendAsync(request, cancel).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            if (attempt < 2 && ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)) { await Task.Delay(1000 * (attempt + 1), cancel).ConfigureAwait(false); continue; }
                            throw new HttpRequestException("Google Lens Sharp returned HTTP " + (int)response.StatusCode + ".");
                        }
                        return ReadText(await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false));
                    }
                }
            }
        }
        private static string LanguageCode(string language)
        {
            try { return CultureInfo.GetCultures(CultureTypes.AllCultures).FirstOrDefault(c => c.Name.Equals(language, StringComparison.OrdinalIgnoreCase) || c.EnglishName.Equals(language, StringComparison.OrdinalIgnoreCase))?.TwoLetterISOLanguageName ?? "en"; }
            catch (CultureNotFoundException) { return "en"; }
        }
        public static string ReadText(byte[] responseBytes)
        {
            var response = LensProtoResponse.Deserialize(responseBytes);
            if (response.HasError && response.ErrorType != 0) throw new InvalidDataException("Google Lens recognition error " + response.ErrorType + ".");
            if (!response.HasObjectsResponse) throw new InvalidDataException("Google Lens returned no objects response.");
            var lines = response.TextData?.TextLayout?.Paragraphs.SelectMany(p => p.Lines).ToList() ?? new List<Line>();
            var chunks = lines.Select(line => new Chunk
            {
                Text = Regex.Replace(string.Concat(line.Words.Select((w, i) => w.PlainText + (w.HasTextSeparator ? w.TextSeparator : i < line.Words.Count - 1 ? " " : ""))), @"\s+", " ").Trim(),
                Box = line.Geometry?.BoundingBox ?? new BoundingBoxProto { CenterX = .5f, CenterY = .5f, Width = 1, Height = 1 }
            }).Where(c => c.Text.Length > 0).ToList();
            if (chunks.Count < 2 || chunks.All(c => c.Box.CenterX == chunks[0].Box.CenterX && c.Box.CenterY == chunks[0].Box.CenterY)) return ClassicBitmapOcr.Normalize(string.Join(Environment.NewLine, chunks.Select(c => c.Text)));
            // Stable 5.3 reading-order correction: Lens can interleave chunks from two subtitle rows.
            var rows = new List<List<Chunk>>();
            foreach (var chunk in chunks.OrderBy(c => c.Box.CenterY))
            {
                var row = rows.FirstOrDefault(r => Math.Abs(r[0].Box.CenterY - chunk.Box.CenterY) <= Math.Min(r[0].Box.Height, chunk.Box.Height) * .5);
                if (row == null) { row = new List<Chunk>(); rows.Add(row); }
                row.Add(chunk);
            }
            return ClassicBitmapOcr.Normalize(string.Join(Environment.NewLine, rows.Select(r => string.Join(" ", r.OrderBy(c => c.Box.CenterX).Select(c => c.Text)))));
        }
        private sealed class Chunk { public string Text; public BoundingBoxProto Box; }
        public void Dispose() => _http.Dispose();
    }
}
