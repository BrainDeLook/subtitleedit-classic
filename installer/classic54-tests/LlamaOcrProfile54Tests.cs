using System;
using System.IO;
using Nikse.SubtitleEdit.Logic;
namespace Classic54Tests
{
 public class LlamaOcrProfile54Tests : IDisposable
 {
  readonly byte[] general = File.Exists(ClassicLlamaSettings.FileName) ? File.ReadAllBytes(ClassicLlamaSettings.FileName) : null;
  readonly byte[] ocr = File.Exists(ClassicLlamaSettings.OcrFileName) ? File.ReadAllBytes(ClassicLlamaSettings.OcrFileName) : null;
  [Fact] public void SavingOcrDoesNotChangeTranslationProfile()
  {
   new ClassicLlamaSettings { Model="translation-test.gguf", Context=4096 }.Save();
   var original=File.ReadAllText(ClassicLlamaSettings.FileName);
   new ClassicLlamaSettings { Model="GLM-OCR-Q8_0.gguf", Context=8192 }.SaveOcr();
   new ClassicLlamaSettings { Model="LFM3-VL-2B-Q4_K_M.gguf", Context=16384 }.SaveOcr();
   Assert.Equal(original,File.ReadAllText(ClassicLlamaSettings.FileName));
   Assert.Equal("translation-test.gguf",ClassicLlamaSettings.Load().Model);
   Assert.Equal("LFM3-VL-2B-Q4_K_M.gguf",ClassicLlamaSettings.LoadOcr().Model);
   Assert.Equal(16384,ClassicLlamaSettings.LoadOcr().Context);
   Assert.Equal(ClassicLlamaServer.Models(new ClassicLlamaSettings()),ClassicLlamaServer.Models(ClassicLlamaSettings.LoadOcr()));
  }
  public void Dispose()
  {
   if(general==null)File.Delete(ClassicLlamaSettings.FileName);else File.WriteAllBytes(ClassicLlamaSettings.FileName,general);
   if(ocr==null)File.Delete(ClassicLlamaSettings.OcrFileName);else File.WriteAllBytes(ClassicLlamaSettings.OcrFileName,ocr);
  }
 }
}
