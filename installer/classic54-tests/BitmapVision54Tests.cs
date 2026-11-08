using System;
using System.IO;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Ocr;

namespace Classic54Tests
{
    public class BitmapVision54Tests
    {
        private sealed class Handler : HttpMessageHandler
        {
            public JObject Payload;
            public int Calls, Width, Height;
            public Color Corner;
            public CancellationTokenSource Cancel;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                Calls++;
                Payload = JObject.Parse(await request.Content.ReadAsStringAsync());
                var content = (JArray)Payload["messages"][0]["content"];
                var url = (string)content[1]["image_url"]["url"];
                using (var stream = new MemoryStream(Convert.FromBase64String(url.Substring(url.IndexOf(',') + 1))))
                using (var image = new Bitmap(stream)) { Width = image.Width; Height = image.Height; Corner = image.GetPixel(0, 0); }
                Cancel?.Cancel(); token.ThrowIfCancellationRequested();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new JObject { ["choices"] = new JArray(new JObject { ["message"] = new JObject { ["content"] = "  First line  \n Second line  " } }) }.ToString()) };
            }
        }
        private static ClassicBitmapSettings Settings() => new ClassicBitmapSettings { Api = new Nikse.SubtitleEdit.Logic.SpeechToText.ClassicCompatibleAsrSettings { Url = "https://ocr.invalid/v1/chat/completions", Model = "test", TimeoutSeconds = 10 } };
        [Theory]
        [InlineData("GLM-OCR-Q8_0.gguf", 240, 240)]
        [InlineData("LightOnOCR-3-4B.Q8_0.gguf", 220, 60)]
        [InlineData("HunyuanOCR-Q8_0.gguf", 220, 60)]
        [InlineData("Qari-OCR-0.2.2.1-VL-2B-Instruct-merged.Q8_0.gguf", 220, 60)]
        [InlineData("LightOnOCR-3-0.8B-Q8_0.gguf", 220, 60)]
        [InlineData("unknown-model", 240, 240)]
        public void RequestsUseModelSpecificFraming(string model, int width, int height)
        {
            using (var handler = new Handler())
            using (var engine = new ClassicBitmapOcr("compatible", Settings(), handler))
            using (var image = new Bitmap(200, 40))
            {
                using (var graphics = Graphics.FromImage(image)) graphics.Clear(Color.White);
                Assert.Equal("First line" + Environment.NewLine + "Second line", engine.Ocr(image, model, "Russian", CancellationToken.None).GetAwaiter().GetResult());
                Assert.Equal(width, handler.Width); Assert.Equal(height, handler.Height); Assert.Equal(Color.Black.ToArgb(), handler.Corner.ToArgb());
                Assert.Equal(model, (string)handler.Payload["model"]);
                Assert.Contains("Russian", (string)handler.Payload["messages"][0]["content"][0]["text"]);
                Assert.Equal(Color.White.ToArgb(), image.GetPixel(0, 0).ToArgb());
            }
        }
        [Fact] public void TrainedPromptAndCustomOverrideAreUsedAcrossMultipleImages()
        {
            using (var handler = new Handler())
            using (var engine = new ClassicBitmapOcr("compatible", Settings(), handler))
            using (var image = new Bitmap(200, 40))
            {
                engine.Ocr(image, "LFM3-VL-2B-Q4_K_M.gguf", "Japanese", CancellationToken.None).GetAwaiter().GetResult();
                Assert.Contains("keeping symbols such as ♪", (string)handler.Payload["messages"][0]["content"][0]["text"]);
                engine.Ocr(image, "LFM3-VL-2B-Q4_K_M.gguf", "Japanese", CancellationToken.None, "Read {language}: \"exact\"").GetAwaiter().GetResult();
                Assert.Equal("Read Japanese: \"exact\"", (string)handler.Payload["messages"][0]["content"][0]["text"]);
                Assert.Equal(2, handler.Calls);
            }
        }
        [Fact] public void CallerCancellationPropagates()
        {
            using (var cancel = new CancellationTokenSource())
            using (var handler = new Handler { Cancel = cancel })
            using (var engine = new ClassicBitmapOcr("compatible", Settings(), handler))
            using (var image = new Bitmap(50, 20))
                Assert.Throws<OperationCanceledException>(() => engine.Ocr(image, "test", "English", cancel.Token).GetAwaiter().GetResult());
        }
        [Fact] public void LightOnServerUsesProjectorAndDisablesThinking()
        {
            var settings = new ClassicLlamaSettings { Model = "LightOnOCR-3-4B.Q8_0.gguf" };
            var args = ClassicLlamaServer.Arguments(settings, 12345);
            Assert.Contains("--reasoning off", args); Assert.Contains("--mmproj", args); Assert.Contains("mmproj-LightOnOCR-3-4B.Q8_0.gguf", args);
            Assert.Equal(7, ClassicLlamaCatalog.OcrModels.Count);
        }
    }
}
