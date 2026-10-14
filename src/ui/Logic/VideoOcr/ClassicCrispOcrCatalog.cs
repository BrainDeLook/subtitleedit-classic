using System.Collections.Generic;
using System.Linq;
namespace Nikse.SubtitleEdit.Logic.VideoOcr
{
// Catalog from stable v5.2.0; the Video OCR whitelist matches its ViewModel.
public static class ClassicCrispOcrCatalog
{
public static List<CrispEmbedBackend> VideoBackends() => GetBackends().Where(b=>new[]{"GLM-OCR","DeepSeek-OCR-2","PP-OCRv6"}.Contains(b.Name)).ToList();
    public static List<CrispEmbedBackend> GetBackends()
    {
        return new List<CrispEmbedBackend>
        {
            // PP-OCRv6 (CrispEmbed v0.17.0) is a detector + recognizer pair rather than a VLM:
            // two small GGUFs instead of one ~1 GB model, and it keeps punctuation and per-line
            // breaks on subtitle bitmaps. Only the "medium" tier is offered - "tiny" returns
            // garbage on subtitle-sized text and "small", while as accurate here, saves too
            // little to be worth a second entry (verified 2026-08-04).
            new()
            {
                Name = "PP-OCRv6",
                Models = new List<CrispEmbedModel>
                {
                    new()
                    {
                        Name = "PP-OCRv6_medium_rec-f16.gguf",
                        Size = "79 MB",
                        Url = "https://huggingface.co/cstr/PP-OCRv6-medium-rec-GGUF/resolve/main/PP-OCRv6_medium_rec-f16.gguf",
                        DetectorName = "PP-OCRv6_medium_det-f16.gguf",
                        DetectorUrl = "https://huggingface.co/cstr/PP-OCRv6-medium-det-GGUF/resolve/main/PP-OCRv6_medium_det-f16.gguf",
                        MinimumSizeBytes = 30_000_000,
                        MinimumDetectorSizeBytes = 30_000_000,
                    },
                },
            },
            new()
            {
                Name = "GLM-OCR",
                Models = new List<CrispEmbedModel>
                {
                    new()
                    {
                        Name = "glm-ocr-q4_k.gguf",
                        Size = "889 MB",
                        Url = "https://huggingface.co/cstr/glm-ocr-crispembed-GGUF/resolve/main/glm-ocr-q4_k.gguf",
                    },
                    new()
                    {
                        Name = "glm-ocr-q8_0.gguf",
                        Size = "1.18 GB",
                        Url = "https://huggingface.co/cstr/glm-ocr-crispembed-GGUF/resolve/main/glm-ocr-q8_0.gguf",
                    },
                },
            },
            new()
            {
                Name = "GOT-OCR2",
                Models = new List<CrispEmbedModel>
                {
                    new()
                    {
                        Name = "got-ocr2-q4_k.gguf",
                        Size = "445 MB",
                        Url = "https://huggingface.co/cstr/got-ocr2-crispembed-GGUF/resolve/main/got-ocr2-q4_k.gguf",
                    },
                    new()
                    {
                        Name = "got-ocr2-q8_0.gguf",
                        Size = "600 MB",
                        Url = "https://huggingface.co/cstr/got-ocr2-crispembed-GGUF/resolve/main/got-ocr2-q8_0.gguf",
                    },
                },
            },
            // PaddleOCR-VL (109 languages) omitted for now: in CrispEmbed v0.14.0 both the
            // q4_k and q8_0 quants truncate or hallucinate on subtitle-style single-line
            // images via both the server and CLI single-model paths - verified 2026-07-11.
            new()
            {
                Name = "Qwen3-VL-2B",
                Models = new List<CrispEmbedModel>
                {
                    new()
                    {
                        Name = "qwen3-vl-2b-q4_k.gguf",
                        Size = "1.59 GB",
                        Url = "https://huggingface.co/cstr/qwen3-vl-2b-crispembed-gguf/resolve/main/qwen3-vl-2b-q4_k.gguf",
                    },
                    new()
                    {
                        Name = "qwen3-vl-2b-q8_0.gguf",
                        Size = "2.29 GB",
                        Url = "https://huggingface.co/cstr/qwen3-vl-2b-crispembed-gguf/resolve/main/qwen3-vl-2b-q8_0.gguf",
                    },
                },
            },
            // DeepSeek-OCR-2 (CrispEmbed v0.17.5): the most accurate backend here on
            // subtitle-style images - the only one exact on a 9-image EN/DE/FR/ES/IT/ZH/JA/RU
            // corpus where GLM-OCR scored 6/9 and GOT-OCR2 0/9 (verified 2026-08-05). ~2 s/image
            // on Apple Silicon with the single-view mode CrispEmbedOcr requests via
            // DS2_CROP_MODE=0. Only the q4_k "stacked" quant is offered: q8_0 measured slightly
            // worse on the same corpus (8/9, drops an umlaut) while being 1.3 GB larger and no
            // faster.
            new()
            {
                Name = "DeepSeek-OCR-2",
                Models = new List<CrispEmbedModel>
                {
                    new()
                    {
                        Name = "deepseek-ocr2-q4_k-stacked.gguf",
                        Size = "2.31 GB",
                        Url = "https://huggingface.co/cstr/deepseek-ocr2-crispembed-GGUF/resolve/main/deepseek-ocr2-q4_k-stacked.gguf",
                        MinimumSizeBytes = 2_000_000_000,
                    },
                },
            },
        };
    }

}
}
