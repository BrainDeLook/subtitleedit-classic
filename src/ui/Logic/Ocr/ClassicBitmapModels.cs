using System.Collections.Generic;
namespace Nikse.SubtitleEdit.Logic.Ocr { public static class ClassicBitmapModels { public static readonly List<ClassicBitmapModel> All = new List<ClassicBitmapModel> {
new ClassicBitmapModel { Name="PP-OCRv6_medium_rec-f16.gguf", Url="https://huggingface.co/cstr/PP-OCRv6-medium-rec-GGUF/resolve/main/PP-OCRv6_medium_rec-f16.gguf", Detector="PP-OCRv6_medium_det-f16.gguf", DetectorUrl="https://huggingface.co/cstr/PP-OCRv6-medium-det-GGUF/resolve/main/PP-OCRv6_medium_det-f16.gguf" },
new ClassicBitmapModel { Name="glm-ocr-q4_k.gguf", Url="https://huggingface.co/cstr/glm-ocr-crispembed-GGUF/resolve/main/glm-ocr-q4_k.gguf" },
new ClassicBitmapModel { Name="glm-ocr-q8_0.gguf", Url="https://huggingface.co/cstr/glm-ocr-crispembed-GGUF/resolve/main/glm-ocr-q8_0.gguf" },
new ClassicBitmapModel { Name="got-ocr2-q4_k.gguf", Url="https://huggingface.co/cstr/got-ocr2-crispembed-GGUF/resolve/main/got-ocr2-q4_k.gguf" },
new ClassicBitmapModel { Name="got-ocr2-q8_0.gguf", Url="https://huggingface.co/cstr/got-ocr2-crispembed-GGUF/resolve/main/got-ocr2-q8_0.gguf" },
new ClassicBitmapModel { Name="qwen3-vl-2b-q4_k.gguf", Url="https://huggingface.co/cstr/qwen3-vl-2b-crispembed-gguf/resolve/main/qwen3-vl-2b-q4_k.gguf" },
new ClassicBitmapModel { Name="qwen3-vl-2b-q8_0.gguf", Url="https://huggingface.co/cstr/qwen3-vl-2b-crispembed-gguf/resolve/main/qwen3-vl-2b-q8_0.gguf" },
new ClassicBitmapModel { Name="deepseek-ocr2-q4_k-stacked.gguf", Url="https://huggingface.co/cstr/deepseek-ocr2-crispembed-GGUF/resolve/main/deepseek-ocr2-q4_k-stacked.gguf" }
}; } }