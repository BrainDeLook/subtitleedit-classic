using System.Collections.Generic;
namespace Nikse.SubtitleEdit.Logic {
public sealed class LlamaCppModel {
public string DisplayName { get; }
public string FileName { get; }
public string Size { get; }
public string Url { get; }
public string MmprojFileName { get; }
public string MmprojUrl { get; }
public string ChatTemplate { get; }
public bool NoJinja { get; }
public string PromptTemplate { get; } public double Temperature { get; } public double TopP { get; } public int TopK { get; } public double RepeatPenalty { get; } public bool CompletionOnly { get; } public bool NoThinking { get; }
public LlamaCppModel(string DisplayName, string FileName, string Size, string Url, string MmprojFileName = null, string MmprojUrl = null, string ChatTemplate = null, bool NoJinja = false, string PromptTemplate = null, double Temperature = -1, double TopP = -1, int TopK = -1, double RepeatPenalty = -1, bool CompletionOnly = false, bool NoThinking = false) { this.DisplayName = DisplayName; this.FileName = FileName; this.Size = Size; this.Url = Url; this.MmprojFileName = MmprojFileName; this.MmprojUrl = MmprojUrl; this.ChatTemplate = ChatTemplate; this.NoJinja = NoJinja; this.PromptTemplate = PromptTemplate; this.Temperature = Temperature; this.TopP = TopP; this.TopK = TopK; this.RepeatPenalty = RepeatPenalty; this.CompletionOnly = CompletionOnly; this.NoThinking = NoThinking; }
public override string ToString() => DisplayName + " (" + Size + ")"; }
public static class ClassicLlamaCatalog {
    private const string HyMt2PromptTemplate =
        "Translate the following text into {1}. Keep line breaks exactly the same. Note that you should only output the translated result without any additional explanation:";

    // MiLMMT-46's trained raw-completion format (with language English names). The text sits
    // inside the template ({2}) and the trailing "{1}:" cue is mandatory - without it the model
    // does not switch language and just echo-loops the source. Its GGUF chat template is a pure
    // passthrough ("{{ message.content }}", no role markers), so the chat endpoint delivers
    // this verbatim and no ChatTemplate/NoJinja override is wanted.
    private const string MiLmMt46PromptTemplate =
        "Translate this from {0} to {1}:\n{0}: {2}\n{1}:";

    public static readonly IReadOnlyList<LlamaCppModel> TranslateModels = new[]
    {
        new LlamaCppModel("TranslateGemma 4B (Q4_K_M)", "translategemma-4b_Q4_K_M.gguf", "2.5 GB",
            "https://huggingface.co/SandLogicTechnologies/translategemma-4b-it-GGUF/resolve/main/translategemma-4b_Q4_K_M.gguf",
            ChatTemplate: "gemma", NoJinja: true),
        new LlamaCppModel("TranslateGemma 4B (Q5_K_M)", "translategemma-4b_Q5_K_M.gguf", "2.8 GB",
            "https://huggingface.co/SandLogicTechnologies/translategemma-4b-it-GGUF/resolve/main/translategemma-4b_Q5_K_M.gguf",
            ChatTemplate: "gemma", NoJinja: true),
        new LlamaCppModel("TranslateGemma 4B (Q8_0)", "translategemma-4b-it-q8_0.gguf", "4.1 GB",
            "https://huggingface.co/NikolayKozloff/translategemma-4b-it-Q8_0-GGUF/resolve/main/translategemma-4b-it-q8_0.gguf",
            ChatTemplate: "gemma", NoJinja: true),
        new LlamaCppModel("TranslateGemma 12B (Q4_K_M)", "translategemma-12b-it-q4_k_m.gguf", "7.3 GB",
            "https://huggingface.co/NikolayKozloff/translategemma-12b-it-Q4_K_M-GGUF/resolve/main/translategemma-12b-it-q4_k_m.gguf",
            ChatTemplate: "gemma", NoJinja: true),
        new LlamaCppModel("TranslateGemma 12B (Q5_K_M)", "translategemma-12b-it-q5_k_m.gguf", "8.5 GB",
            "https://huggingface.co/NikolayKozloff/translategemma-12b-it-Q5_K_M-GGUF/resolve/main/translategemma-12b-it-q5_k_m.gguf",
            ChatTemplate: "gemma", NoJinja: true),

        // MiLMMT-46 v1.0 (Xiaomi, 2026) - Gemma3-based translation-specialized models, 46
        // languages including Danish/Norwegian/Swedish (the gap in Hy-MT2's coverage); the paper
        // reports it ahead of TranslateGemma and Hy-MT 1.5. Temperature 0 matches the model
        // card's greedy-decoding usage. CompletionOnly: see the record field - regular engine only.
        new LlamaCppModel("MiLMMT-46 4B (Q4_K_M) - 46 languages incl. Nordic", "MiLMMT-46-4B-v1.0.Q4_K_M.gguf", "2.5 GB",
            "https://huggingface.co/mradermacher/MiLMMT-46-4B-v1.0-GGUF/resolve/main/MiLMMT-46-4B-v1.0.Q4_K_M.gguf",
            PromptTemplate: MiLmMt46PromptTemplate, Temperature: 0, CompletionOnly: true),
        new LlamaCppModel("MiLMMT-46 4B (Q8_0) - 46 languages incl. Nordic", "MiLMMT-46-4B-v1.0.Q8_0.gguf", "4.1 GB",
            "https://huggingface.co/mradermacher/MiLMMT-46-4B-v1.0-GGUF/resolve/main/MiLMMT-46-4B-v1.0.Q8_0.gguf",
            PromptTemplate: MiLmMt46PromptTemplate, Temperature: 0, CompletionOnly: true),
        new LlamaCppModel("MiLMMT-46 12B (Q4_K_M) - 46 languages incl. Nordic", "MiLMMT-46-12B-v1.0.Q4_K_M.gguf", "7.3 GB",
            "https://huggingface.co/mradermacher/MiLMMT-46-12B-v1.0-GGUF/resolve/main/MiLMMT-46-12B-v1.0.Q4_K_M.gguf",
            PromptTemplate: MiLmMt46PromptTemplate, Temperature: 0, CompletionOnly: true),
        new LlamaCppModel("MiLMMT-46 12B (Q5_K_M) - 46 languages incl. Nordic", "MiLMMT-46-12B-v1.0.Q5_K_M.gguf", "8.4 GB",
            "https://huggingface.co/mradermacher/MiLMMT-46-12B-v1.0-GGUF/resolve/main/MiLMMT-46-12B-v1.0.Q5_K_M.gguf",
            PromptTemplate: MiLmMt46PromptTemplate, Temperature: 0, CompletionOnly: true),

        // Gemma 4 (Google, 2026) - 140+ languages, the strongest general model here for translation
        // into non-English targets. NOTE: unlike Gemma 2/3 this must use its own embedded Jinja
        // template - Gemma 4 replaced the <start_of_turn> scheme with <|turn>role ... <turn|>, so
        // llama.cpp's built-in "gemma" template does NOT apply and forcing it produces garbage.
        // That template turns thinking ON by default, which for subtitle-sized requests means no
        // translation at all: 7 of 16 English->Danish lines and 11 of 16 English->German lines came
        // back empty at ~10-12 s/line, each burning the whole max_tokens budget inside
        // reasoning_content. Hence NoThinking on every Gemma 4 entry - with it, 0 of 16 empty at
        // ~1 s/line.
        new LlamaCppModel("Gemma 4 E4B it (Q4_K_M)", "google_gemma-4-E4B-it-Q4_K_M.gguf", "5.4 GB",
            "https://huggingface.co/bartowski/google_gemma-4-E4B-it-GGUF/resolve/main/google_gemma-4-E4B-it-Q4_K_M.gguf",
            NoThinking: true),
        new LlamaCppModel("Gemma 4 E4B it (Q8_0)", "google_gemma-4-E4B-it-Q8_0.gguf", "8.0 GB",
            "https://huggingface.co/bartowski/google_gemma-4-E4B-it-GGUF/resolve/main/google_gemma-4-E4B-it-Q8_0.gguf",
            NoThinking: true),
        // The 12B repo (and its file names) drop the "google_" prefix the E4B repo uses.
        new LlamaCppModel("Gemma 4 12B it (Q4_K_M)", "gemma-4-12B-it-Q4_K_M.gguf", "7.6 GB",
            "https://huggingface.co/bartowski/gemma-4-12B-it-GGUF/resolve/main/gemma-4-12B-it-Q4_K_M.gguf",
            NoThinking: true),

        // Alternative model family. Qwen 3 is the strongest open model for CJK
        // (Chinese/Japanese/Korean) and competitive elsewhere — useful fallback
        // when Gemma's quirks bite (occasional refusals, formatting drift, etc).
        // NoThinking (--reasoning off) suppresses the hybrid Qwen3 template's thinking mode so
        // output is clean translation, not <think>...</think> reasoning blocks - same mechanism
        // as Gemma 4 above. This used to be "--no-jinja --chat-template chatml" instead (forcing
        // the template also bypasses enable_thinking), but that proved unreliable: a controlled
        // A/B (seconv EN->DA, 10 reps x 20 lines on the Q8_0 9B, 200 lines/variant) measured
        // chatml+no-jinja leaking raw <think> text - which runs the model out of its token budget
        // before it ever reaches the translation - into 2.5% of lines (5/200), while NoThinking
        // alone stayed at 0% (0/200) across every rep on both the 9B and the 4B Q4_K_M (0/60).
        // Stacking both overrides is worse, not better (6.5% on 9B, 16.7% on 4B): --reasoning off
        // does not reliably suppress thinking once --chat-template chatml has replaced the
        // template it hooks into, so the two must never be combined.
        new LlamaCppModel("Qwen 3 4B Instruct (Q4_K_M)", "Qwen_Qwen3-4B-Instruct-2507-Q4_K_M.gguf", "2.5 GB",
            "https://huggingface.co/bartowski/Qwen_Qwen3-4B-Instruct-2507-GGUF/resolve/main/Qwen_Qwen3-4B-Instruct-2507-Q4_K_M.gguf",
            NoThinking: true),
        new LlamaCppModel("Qwen 3 8B (Q4_K_M)", "Qwen_Qwen3-8B-Q4_K_M.gguf", "4.7 GB",
            "https://huggingface.co/bartowski/Qwen_Qwen3-8B-GGUF/resolve/main/Qwen_Qwen3-8B-Q4_K_M.gguf",
            NoThinking: true),

        // Qwen 3.5 - newer Qwen generation. Same NoThinking handling as Qwen 3 above. Kept to <= 8 GB.
        new LlamaCppModel("Qwen 3.5 4B (Q4_K_M)", "Qwen_Qwen3.5-4B-Q4_K_M.gguf", "2.8 GB",
            "https://huggingface.co/bartowski/Qwen_Qwen3.5-4B-GGUF/resolve/main/Qwen_Qwen3.5-4B-Q4_K_M.gguf",
            NoThinking: true),
        new LlamaCppModel("Qwen 3.5 4B (Q8_0)", "Qwen_Qwen3.5-4B-Q8_0.gguf", "4.3 GB",
            "https://huggingface.co/bartowski/Qwen_Qwen3.5-4B-GGUF/resolve/main/Qwen_Qwen3.5-4B-Q8_0.gguf",
            NoThinking: true),
        new LlamaCppModel("Qwen 3.5 9B (Q4_K_M)", "Qwen_Qwen3.5-9B-Q4_K_M.gguf", "5.7 GB",
            "https://huggingface.co/bartowski/Qwen_Qwen3.5-9B-GGUF/resolve/main/Qwen_Qwen3.5-9B-Q4_K_M.gguf",
            NoThinking: true),
        new LlamaCppModel("Qwen 3.5 9B (Q8_0)", "Qwen_Qwen3.5-9B-Q8_0.gguf", "9.8 GB",
            "https://huggingface.co/bartowski/Qwen_Qwen3.5-9B-GGUF/resolve/main/Qwen_Qwen3.5-9B-Q8_0.gguf",
            NoThinking: true),

        // Qwen 3.6 35B-A3B - a mixture-of-experts model: 35B total but only ~3B active per token, so
        // it generates fast even fully on CPU. That makes it the option for machines with plenty of
        // RAM but no usable GPU. The catch is the quant: UD-IQ2_M is 2-bit, and low-bit hurts MoE
        // more than dense models (few active params means little redundancy to absorb the error), so
        // translation quality may well trail the much smaller Qwen 3.5 9B Q4_K_M - hence the note in
        // the display name. IQ2_M is nonetheless the smallest Qwen 3.6 build available; every other
        // quant of this model is larger, not smaller. Its GGUF reports architecture "qwen35moe" (the
        // Qwen 3.5 MoE arch), which the pinned engine already supports.
        new LlamaCppModel("Qwen 3.6 35B-A3B (IQ2_M) - fast on CPU, 2-bit quality", "Qwen3.6-35B-A3B-UD-IQ2_M.gguf", "11.5 GB",
            "https://huggingface.co/unsloth/Qwen3.6-35B-A3B-GGUF/resolve/main/Qwen3.6-35B-A3B-UD-IQ2_M.gguf",
            NoThinking: true),

        // Hy-MT2 (Tencent Hunyuan-MT 2, 2026) - translation-specialized, official GGUFs, Apache-2.0.
        // Excellent for its 33+5 supported languages (CJK, major European/Asian) but has NO Nordic
        // languages (Danish/Swedish/Norwegian/Finnish), no Greek/Romanian/Hungarian - generation in
        // unsupported languages produces garbage, hence the coverage note in the display name.
        // Trained on a fixed prompt (PromptTemplate below, with language NAMES - the generic prompt
        // makes it answer in Chinese) and Tencent-recommended sampling. Embedded chat template works
        // as-is, so no ChatTemplate/NoJinja overrides.
        new LlamaCppModel("Hy-MT2 7B (Q4_K_M) - 33 languages, no Nordic", "Hy-MT2-7B-Q4_K_M.gguf", "4.6 GB",
            "https://huggingface.co/tencent/Hy-MT2-7B-GGUF/resolve/main/Hy-MT2-7B-Q4_K_M.gguf",
            PromptTemplate: HyMt2PromptTemplate, Temperature: 0.7, TopP: 0.6, TopK: 20, RepeatPenalty: 1.05),
        new LlamaCppModel("Hy-MT2 7B (Q8_0) - 33 languages, no Nordic", "HY-MT2-7B-Q8_0.gguf", "8.0 GB",
            "https://huggingface.co/tencent/Hy-MT2-7B-GGUF/resolve/main/HY-MT2-7B-Q8_0.gguf",
            PromptTemplate: HyMt2PromptTemplate, Temperature: 0.7, TopP: 0.6, TopK: 20, RepeatPenalty: 1.05),
        new LlamaCppModel("Hy-MT2 1.8B (Q8_0) - 33 languages, no Nordic", "Hy-MT2-1.8B-Q8_0.gguf", "1.9 GB",
            "https://huggingface.co/tencent/Hy-MT2-1.8B-GGUF/resolve/main/Hy-MT2-1.8B-Q8_0.gguf",
            PromptTemplate: HyMt2PromptTemplate, Temperature: 0.7, TopP: 0.6, TopK: 20, RepeatPenalty: 1.05),

        // TranslatePsy-AfriSLM 4B (Tether/QVAC, 2026) - a Qwen 3.5 4B fine-tune for English <-> 19
        // Sub-Saharan African languages (Afrikaans, Amharic, Hausa, Igbo, Kinyarwanda, Lingala,
        // Luganda, Malagasy, Nyanja, Oromo, Shona, Somali, Southern Sotho, Swahili, Tswana, Wolof,
        // Xhosa, Yoruba, Zulu), official GGUFs, Apache-2.0. Fills a real gap: the base Qwen 3.5 4B
        // writes fluent-looking nonsense in Zulu/Yoruba (16 subtitle lines, back-translated: 1-2 of
        // 16 recognizable vs 15-16 of 16 for this model). Verified 2026-09-21 on b10840:
        // - NoThinking is mandatory - it inherits Qwen 3.5's thinking template and without
        //   "--reasoning off" 12 of 16 lines came back empty (answer in reasoning_content).
        // - No PromptTemplate on purpose: the model card's prompt ("Please translate the following
        //   {0} text into {1}: {2}.\n\nTranslation:") drops line breaks and, translating INTO
        //   English, pads short lines with invented sentences ("Yeah." -> "Yes. Yes, that's
        //   right."); SE's generic prompt has neither problem. The advanced engine's JSON batch
        //   protocol works as well (all lines translated, all line breaks kept).
        new LlamaCppModel("TranslatePsy-AfriSLM 4B (Q4_K_M) - English <-> 19 African languages", "TranslatePsy-AfriSLM-4B-Q4_K_M-imat.gguf", "3.1 GB",
            "https://huggingface.co/qvac/TranslatePsy-AfriSLM-4B-Q4-GGUF/resolve/main/TranslatePsy-AfriSLM-4B-Q4_K_M-imat.gguf",
            NoThinking: true),
        new LlamaCppModel("TranslatePsy-AfriSLM 4B (Q8_0) - English <-> 19 African languages", "TranslatePsy-AfriSLM-4B-Q8_0-imat.gguf", "5.2 GB",
            "https://huggingface.co/qvac/TranslatePsy-AfriSLM-4B-Q8-GGUF/resolve/main/TranslatePsy-AfriSLM-4B-Q8_0-imat.gguf",
            NoThinking: true),

        // Index-Translate (bilibili, 2026) - a Qwen 3.5 fine-tune for translation, 150 languages,
        // Apache-2.0 (#15518). Temperature 0 = bilibili's greedy default. NoThinking because it keeps
        // Qwen 3.5's thinking template: without "--reasoning off" every line still came back, but at
        // ~4x the time (34 s vs 8 s for 24 lines EN->DA on the 2B). No PromptTemplate - its trained
        // prompt ("Translate the following text into {1}. Output the translation directly...") is
        // already what SE's generic prompt asks for. Downloads come from IndexTeam's official GGUF
        // repos (published 2026-10-02); the local file names are kept from the earlier community
        // quants so existing downloads and saved selections still match. Compared 2026-10-01 (seconv, 24 dialog lines
        // EN->DA/DE/JA, b10840): the 9B was on par with TranslateGemma 12B (best in Japanese, never
        // left a line untranslated) and ahead of Gemma 4 E4B; the 2B is roughly TranslateGemma 4B
        // level at twice its speed. The 2B Q4_K_M is not offered - it made clearly worse slips than
        // the Q8_0 (e.g. "sweetie" -> the Danish insult "kærling"). Index-Homura (same family) is
        // left out: it needs a target syllable count per line, which the engines cannot supply.
        new LlamaCppModel("Index-Translate 2B (Q8_0) - 150 languages", "Index-Translate-2B.Q8_0.gguf", "2.1 GB",
            "https://huggingface.co/IndexTeam/Index-Translate-2B-GGUF/resolve/main/Index-Translate-2B.Q8_0.gguf",
            Temperature: 0, NoThinking: true),
        new LlamaCppModel("Index-Translate 9B (Q4_K_M) - 150 languages", "index-translate-9b-q4_k_m.gguf", "5.8 GB",
            "https://huggingface.co/IndexTeam/Index-Translate-9B-GGUF/resolve/main/Index-Translate-9B.Q4_K_M.gguf",
            Temperature: 0, NoThinking: true),

        // Aya Expanse 8B (Cohere) - a dedicated multilingual model (23 languages), a good translation
        // alternative to the Gemma/Qwen families. Uses its own embedded (Cohere) chat template, so we
        // leave ChatTemplate/NoJinja at their defaults instead of forcing gemma/chatml. Kept to <= 8 GB.
        new LlamaCppModel("Aya Expanse 8B (Q4_K_M)", "aya-expanse-8b-Q4_K_M.gguf", "4.7 GB",
            "https://huggingface.co/bartowski/aya-expanse-8b-GGUF/resolve/main/aya-expanse-8b-Q4_K_M.gguf"),
        new LlamaCppModel("Aya Expanse 8B (Q5_K_M)", "aya-expanse-8b-Q5_K_M.gguf", "5.4 GB",
            "https://huggingface.co/bartowski/aya-expanse-8b-GGUF/resolve/main/aya-expanse-8b-Q5_K_M.gguf"),
        new LlamaCppModel("Aya Expanse 8B (Q8_0)", "aya-expanse-8b-Q8_0.gguf", "7.9 GB",
            "https://huggingface.co/bartowski/aya-expanse-8b-GGUF/resolve/main/aya-expanse-8b-Q8_0.gguf"),
    };

    // Models for the AI review tool (proofreading). Translation-tuned models (TranslateGemma,
    // Aya) are deliberately absent - proofreading needs general instruction-following and strict
    // JSON output, where the plain instruct models are much stronger. Kept to ~12 GB or below.
    public static readonly IReadOnlyList<LlamaCppModel> ReviewModels = new[]
    {
        // NoThinking instead of chatml+no-jinja - see the note in TranslateModels for the A/B data.
        new LlamaCppModel("Qwen 3.5 4B (Q4_K_M)", "Qwen_Qwen3.5-4B-Q4_K_M.gguf", "2.8 GB",
            "https://huggingface.co/bartowski/Qwen_Qwen3.5-4B-GGUF/resolve/main/Qwen_Qwen3.5-4B-Q4_K_M.gguf",
            NoThinking: true),
        new LlamaCppModel("Qwen 3.5 4B (Q8_0)", "Qwen_Qwen3.5-4B-Q8_0.gguf", "4.3 GB",
            "https://huggingface.co/bartowski/Qwen_Qwen3.5-4B-GGUF/resolve/main/Qwen_Qwen3.5-4B-Q8_0.gguf",
            NoThinking: true),
        new LlamaCppModel("Qwen 3.5 9B (Q4_K_M)", "Qwen_Qwen3.5-9B-Q4_K_M.gguf", "5.7 GB",
            "https://huggingface.co/bartowski/Qwen_Qwen3.5-9B-GGUF/resolve/main/Qwen_Qwen3.5-9B-Q4_K_M.gguf",
            NoThinking: true),
        new LlamaCppModel("Qwen 3.5 9B (Q8_0)", "Qwen_Qwen3.5-9B-Q8_0.gguf", "9.8 GB",
            "https://huggingface.co/bartowski/Qwen_Qwen3.5-9B-GGUF/resolve/main/Qwen_Qwen3.5-9B-Q8_0.gguf",
            NoThinking: true),
        // MoE, ~3B active - fast on CPU; see the note in TranslateModels for the 2-bit caveat.
        new LlamaCppModel("Qwen 3.6 35B-A3B (IQ2_M) - fast on CPU, 2-bit quality", "Qwen3.6-35B-A3B-UD-IQ2_M.gguf", "11.5 GB",
            "https://huggingface.co/unsloth/Qwen3.6-35B-A3B-GGUF/resolve/main/Qwen3.6-35B-A3B-UD-IQ2_M.gguf",
            NoThinking: true),
        new LlamaCppModel("Gemma 3 4B it (Q4_K_M)", "google_gemma-3-4b-it-Q4_K_M.gguf", "2.5 GB",
            "https://huggingface.co/bartowski/google_gemma-3-4b-it-GGUF/resolve/main/google_gemma-3-4b-it-Q4_K_M.gguf",
            ChatTemplate: "gemma", NoJinja: true),
        new LlamaCppModel("Gemma 3 12B it (Q4_K_M)", "google_gemma-3-12b-it-Q4_K_M.gguf", "7.3 GB",
            "https://huggingface.co/bartowski/google_gemma-3-12b-it-GGUF/resolve/main/google_gemma-3-12b-it-Q4_K_M.gguf",
            ChatTemplate: "gemma", NoJinja: true),
        // Gemma 4 uses its own embedded Jinja template - see the note in TranslateModels; the
        // built-in "gemma" template above is the Gemma 2/3 format and must not be forced here.
        // NoThinking for the same reason as there: the review client reads message.content too,
        // so a model that answers in reasoning_content returns an empty review.
        // E2B is the smallest option in this list - for laptops/iGPUs where even the 4B models
        // are a stretch.
        new LlamaCppModel("Gemma 4 E2B it (Q4_K_M)", "google_gemma-4-E2B-it-Q4_K_M.gguf", "3.5 GB",
            "https://huggingface.co/bartowski/google_gemma-4-E2B-it-GGUF/resolve/main/google_gemma-4-E2B-it-Q4_K_M.gguf",
            NoThinking: true),
        new LlamaCppModel("Gemma 4 E4B it (Q4_K_M)", "google_gemma-4-E4B-it-Q4_K_M.gguf", "5.4 GB",
            "https://huggingface.co/bartowski/google_gemma-4-E4B-it-GGUF/resolve/main/google_gemma-4-E4B-it-Q4_K_M.gguf",
            NoThinking: true),
        new LlamaCppModel("Gemma 4 E4B it (Q8_0)", "google_gemma-4-E4B-it-Q8_0.gguf", "8.0 GB",
            "https://huggingface.co/bartowski/google_gemma-4-E4B-it-GGUF/resolve/main/google_gemma-4-E4B-it-Q8_0.gguf",
            NoThinking: true),
        new LlamaCppModel("Gemma 4 12B it (Q4_K_M)", "gemma-4-12B-it-Q4_K_M.gguf", "7.6 GB",
            "https://huggingface.co/bartowski/gemma-4-12B-it-GGUF/resolve/main/gemma-4-12B-it-Q4_K_M.gguf",
            NoThinking: true),

        // Different families for second opinions. Llama 3.1 is the strongest English
        // proofreader of its size; Phi-4 mini is the small/fast option.
        // All use their embedded chat templates.
        new LlamaCppModel("Llama 3.1 8B Instruct (Q4_K_M)", "Meta-Llama-3.1-8B-Instruct-Q4_K_M.gguf", "4.9 GB",
            "https://huggingface.co/bartowski/Meta-Llama-3.1-8B-Instruct-GGUF/resolve/main/Meta-Llama-3.1-8B-Instruct-Q4_K_M.gguf"),
        new LlamaCppModel("Phi-4 mini 3.8B (Q4_K_M)", "microsoft_Phi-4-mini-instruct-Q4_K_M.gguf", "2.5 GB",
            "https://huggingface.co/bartowski/microsoft_Phi-4-mini-instruct-GGUF/resolve/main/microsoft_Phi-4-mini-instruct-Q4_K_M.gguf"),

        // PrismML Bonsai 27B, 1-bit (Q1_0, upstream llama.cpp - the ternary Q2_0/PQ2_0/PTQ1_0 files
        // need PrismML's fork). English only: tested 2026-10-11 on b11425 with the review protocol it
        // found 8/8 planted English errors with no false edits (Qwen 3.5 4B: 7/8), but only 2/6 in
        // Danish, and its translations were poor - hence review only, not in TranslateModels.
        // NoThinking is required: with thinking on a 15-line batch took ~400 s. ~12 tok/s on an M4.
        new LlamaCppModel("Bonsai 27B 1-bit (Q1_0) - English only", "Bonsai-27B-Q1_0.gguf", "3.8 GB",
            "https://huggingface.co/prism-ml/Bonsai-27B-gguf/resolve/main/Bonsai-27B-Q1_0.gguf",
            NoThinking: true),

        // EuroLLM 2512 - all 24 official EU languages (incl. Danish/Swedish/Norwegian/Finnish, which
        // the Hy-MT2 family lacks entirely), so these are the best picks for proofreading European
        // subtitles. Replaces the older EuroLLM-9B-Instruct entry; anyone who already downloaded that
        // one still sees it, as a custom entry from the models folder. The 9B quant comes from
        // mradermacher (no bartowski build exists), hence the dotted file name. The 22B is the largest
        // model in this list: IQ4_XS is the biggest quant that still fits ~12 GB - Q4_K_M is 13.7 GB.
        new LlamaCppModel("EuroLLM 9B Instruct 2512 (Q4_K_M)", "EuroLLM-9B-Instruct-2512.Q4_K_M.gguf", "5.6 GB",
            "https://huggingface.co/mradermacher/EuroLLM-9B-Instruct-2512-GGUF/resolve/main/EuroLLM-9B-Instruct-2512.Q4_K_M.gguf"),
        new LlamaCppModel("EuroLLM 22B Instruct 2512 (IQ4_XS)", "utter-project_EuroLLM-22B-Instruct-2512-IQ4_XS.gguf", "12.3 GB",
            "https://huggingface.co/bartowski/utter-project_EuroLLM-22B-Instruct-2512-GGUF/resolve/main/utter-project_EuroLLM-22B-Instruct-2512-IQ4_XS.gguf"),

        // Granite 4.1 (IBM, 2026) - Apache-2.0, dense, 128k context, 12 languages, and tuned for
        // structured output, which is what the review protocol asks for (strict JSON). Downloaded
        // straight from IBM's own GGUF repo rather than a third-party quanter. Embedded chat template.
        new LlamaCppModel("Granite 4.1 8B (Q4_K_M)", "granite-4.1-8b-Q4_K_M.gguf", "5.3 GB",
            "https://huggingface.co/ibm-granite/granite-4.1-8b-GGUF/resolve/main/granite-4.1-8b-Q4_K_M.gguf"),
    };

    /// <summary>
    /// The curated OCR vision models, <b>ordered best-first on subtitle images</b> - the order is
    /// not cosmetic. The first entry is what the OCR/Video OCR/batch-convert dropdowns preselect
    /// when nothing is saved, and the headless callers (seconv, batch convert) fall back to the
    /// first *installed* entry, so a weaker model placed early wins over a better one that happens
    /// to sit later in the list. Keep new models in measured rank, not in the order they were added.
    /// Ranked 2026-08-25 on llama.cpp b10625 with SE's own flags, prompt and square-pad
    /// preprocessing over a 14-image EN/DE/FR/ES/IT/RU/ZH/JA corpus (music cues, SDH hash cues,
    /// italics, video-frame burn-ins, small/low-res), scoring recognition separately from line-break
    /// preservation: GLM-OCR 13/14 exact at 2.5 s/image and the only one that keeps every line break
    /// and every music note; PaddleOCR-VL 12/14 recognized (0.32% char error) but merges two-line
    /// subtitles; HunyuanOCR 12/14 recognized (0.6%) with the same merging; LightOnOCR 9/14
    /// recognized (2.53%) at 18.5 s/image - weakest and ~7x slower, hence last.
    /// </summary>
    // DeepSeek-OCR-2 was evaluated for this list and rejected (2026-08-26): through
    // mainline llama.cpp with the OCR prompt it captions the image ("The image displays a
    // scene from...") instead of extracting text - 2/24 subtitle lines exact at 11 s/image
    // on a burned-in video clip where GLM-OCR scored 21/24 at 4 s. Its strong results come
    // from CrispEmbed's fork, which drives the model's own crop/prompt modes, so that is
    // where SE offers it.
    public static readonly IReadOnlyList<LlamaCppModel> OcrModels = new[]
    {
        new LlamaCppModel("GLM-OCR 0.9B (Q8_0)", "GLM-OCR-Q8_0.gguf", "1.4 GB",
            "https://huggingface.co/ggml-org/GLM-OCR-GGUF/resolve/main/GLM-OCR-Q8_0.gguf",
            MmprojFileName: "mmproj-GLM-OCR-Q8_0.gguf",
            MmprojUrl: "https://huggingface.co/ggml-org/GLM-OCR-GGUF/resolve/main/mmproj-GLM-OCR-Q8_0.gguf"),
        // Liquid AI's general vision model (3B), not an OCR specialist. Measured 2026-09-05 on the
        // same 14-image corpus: 0.00% character error - flawless across all eight scripts, italics,
        // busy video backgrounds and small text, where GLM-OCR misread one Russian word - and with
        // its own prompt 13/14 exact (its one miss over-splits a line; it never merges). Second,
        // not first, because it is 2.5x the download (2.9 GB + 0.6 GB mmproj) and ~2.5x slower per
        // image. It follows instructions literally, so it needs the PromptTemplate below: under the
        // shared prompt it prefixes every answer with the line count it was asked to identify.
        new LlamaCppModel("LFM2.5-VL 3B (Q8_0)", "LFM2.5-VL-3B-Q8_0.gguf", "3.5 GB",
            "https://huggingface.co/LiquidAI/LFM2.5-VL-3B-GGUF/resolve/main/LFM2.5-VL-3B-Q8_0.gguf",
            MmprojFileName: "mmproj-LFM2.5-VL-3B-Q8_0.gguf",
            MmprojUrl: "https://huggingface.co/LiquidAI/LFM2.5-VL-3B-GGUF/resolve/main/mmproj-LFM2.5-VL-3B-Q8_0.gguf",
            PromptTemplate: "Transcribe the subtitle in the image exactly as written, one output line per line of text in the image, keeping symbols such as ♪. Output only the text. The language is {language}."),
        // PaddlePaddle's official llama.cpp package - 109 languages (NaViT + ERNIE-4.5).
        new LlamaCppModel("PaddleOCR-VL 1.6", "PaddleOCR-VL-1.6-GGUF.gguf", "1.8 GB",
            "https://huggingface.co/PaddlePaddle/PaddleOCR-VL-1.6-GGUF/resolve/main/PaddleOCR-VL-1.6-GGUF.gguf",
            MmprojFileName: "PaddleOCR-VL-1.6-GGUF-mmproj.gguf",
            MmprojUrl: "https://huggingface.co/PaddlePaddle/PaddleOCR-VL-1.6-GGUF/resolve/main/PaddleOCR-VL-1.6-GGUF-mmproj.gguf"),
        // HunyuanOCR 1.5 (Tencent, ~1B). Was the fastest of the list when added (~1.6x GLM-OCR per
        // image on b10310); on b10625 that lead is gone - re-measured 2026-08-25 at 3.4 s/image
        // against GLM-OCR's 2.5 s, and it merges two-line subtitles into one line on 4 of 14
        // images. Verified 2026-08-15 on the pinned b10310 build, 9-image
        // EN/DE/FR/ES/IT/ZH/JA/RU subtitle corpus: recognition itself exact in every script,
        // but two formatting quirks keep it from being the default: it sporadically prefixes a
        // markdown "# " heading (1/9 images; immune to prompt wording, identical at bf16, and
        // unstrippable because genuine SDH "# lyrics" hash cues - which it preserves verbatim -
        // look the same), and it silently drops ♪ note marks. GLM-OCR gets both right.
        // Q8_0 only: bf16 measured identical on the same corpus while twice the size.
        new LlamaCppModel("HunyuanOCR 1.5 (Q8_0)", "HunyuanOCR-Q8_0.gguf", "1.3 GB",
            "https://huggingface.co/ggml-org/HunyuanOCR-GGUF/resolve/main/HunyuanOCR-Q8_0.gguf",
            MmprojFileName: "mmproj-HunyuanOCR-Q8_0.gguf",
            MmprojUrl: "https://huggingface.co/ggml-org/HunyuanOCR-GGUF/resolve/main/mmproj-HunyuanOCR-Q8_0.gguf"),
        // Last on purpose: the weakest and by far the slowest of the four on subtitle images
        // (9/14 recognized, 2.53% character error, 18.5 s/image against GLM-OCR's 2.5 s). It loses
        // line breaks, drops ♪ note marks, misreads Cyrillic ё as е and Chinese 的, and wraps
        // Japanese output in ``` fences. Kept for users who already rely on it.
        new LlamaCppModel("LightOnOCR 1B (Q8_0)", "LightOnOCR-1B-1025-Q8_0.gguf", "1.2 GB",
            "https://huggingface.co/ggml-org/LightOnOCR-1B-1025-GGUF/resolve/main/LightOnOCR-1B-1025-Q8_0.gguf",
            MmprojFileName: "mmproj-LightOnOCR-1B-1025-Q8_0.gguf",
            MmprojUrl: "https://huggingface.co/ggml-org/LightOnOCR-1B-1025-GGUF/resolve/main/mmproj-LightOnOCR-1B-1025-Q8_0.gguf"),
    };

}}