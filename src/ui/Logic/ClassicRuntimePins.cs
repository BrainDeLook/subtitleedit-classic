using System;
using System.Linq;
using System.Security.Cryptography;

namespace Nikse.SubtitleEdit.Logic
{
    // Current download hashes from the pinned upstream v5.3.0 DownloadHashManager.
    public static class ClassicRuntimePins
    {
        public const string FfmpegUrl = "https://github.com/SubtitleEdit/support-files/releases/download/ffmpeg-v9-2/ffmpeg902.zip";
        public const string WhisperCpuUrl = "https://github.com/ggml-org/whisper.cpp/releases/download/b5454/whisper-blas-bin-x64.zip";
        public const string WhisperCudaUrl = "https://github.com/ggml-org/whisper.cpp/releases/download/b5454/whisper-bin-win-cuda-12.4.0-x64.zip";
        public static bool Approved(byte[] bytes, params string[] expected)
        { using (var sha = SHA256.Create()) { var hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); return expected.Contains(hash); } }
        public static bool ApprovedFfmpeg(byte[] bytes) => Approved(bytes, "19ccdb1d6cfd4b07a14b6a4d51fb7a24c1de4672aec1485a0d0703bb7ec5ba8b");
        public static bool ApprovedWhisper(byte[] bytes) => Approved(bytes, "b1158bd10edf92c9799149a6fc5d3e58da3f495241d6fb3423088130e88ffbf3", "afef0b881c500958921c3f5523b50e59ee2ec9b6f5cbd25b324c51ed308a957a");
    }
}
