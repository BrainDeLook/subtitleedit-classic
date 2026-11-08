using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Core.VobSub;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Nikse.SubtitleEdit.Core.ContainerFormats.Mp4.Boxes
{
    public class Stbl : Box
    {
        public Stsd Stsd { get; set; }
        public List<SubPicture> SubPictures;
        public ulong StszSampleCount;
        public ulong TimeScale { get; set; }
        private readonly Mdia _mdia;
        public List<uint> SampleSizes;
        public List<uint> Discardable;
        private const int MaxRunLengthEntries=5_000_000;
        public List<int> Ctts {get;set;}
        public List<uint> Ssts { get; set; }
        public List<SampleToChunkMap> Stsc { get; set; }
        public List<ulong> ChunkOffsets;
        public List<Paragraph> Paragraphs;
        public List<Paragraph> GetParagraphs() => Paragraphs;

        private List<Cea608.CcData> _cea608CcData = new List<Cea608.CcData>();

        public Stbl(Stream fs, ulong maximumLength, ulong timeScale, string handlerType, Mdia mdia)
        {
            TimeScale = timeScale;
            _mdia = mdia;
            Position = (ulong)fs.Position;
            Ssts = new List<uint>();
            Ctts = new List<int>();
            Discardable=new List<uint>();
            Stsc = new List<SampleToChunkMap>();
            SampleSizes = new List<uint>();
            ChunkOffsets = new List<ulong>();
            SubPictures = new List<SubPicture>();
            while (fs.Position < (long)maximumLength)
            {
                if (!InitializeSizeAndName(fs))
                {
                    return;
                }

                if (Name == "stsd") // Sample Description Box
                {
                    Stsd = new Stsd(fs, Position);
                }
                else if (Name == "stco") // 32-bit - chunk offset
                {
                    if (handlerType != "soun")
                    {
                        Buffer = ReadPayload(fs, Size - 4);
                        int version = Buffer[0];
                        var totalEntries = GetUInt(4);

                        // Entry 0 is GetUInt(8), i.e. bytes 8..11 - the helper wants the LAST byte
                        // of the first entry (the other call sites pass it correctly). With 8 the
                        // clamp allowed one entry too many and a short/truncated stco threw.
                        var entries = ClampEntries(totalEntries, 11, 4);
                        ChunkOffsets.Capacity = ChunkOffsets.Count + entries;
                        for (var i = 0; i < entries; i++)
                        {
                            var offset = GetUInt(8 + i * 4);
                            ChunkOffsets.Add(offset);
                        }
                    }
                }
                else if (Name == "co64") // 64-bit
                {
                    if (handlerType != "soun")
                    {
                        Buffer = ReadPayload(fs, Size - 4);
                        int version = Buffer[0];
                        var totalEntries = GetUInt(4);

                        // Entry 0 is GetUInt64(8), i.e. bytes 8..15 - see the stco note above.
                        var entries = ClampEntries(totalEntries, 15, 8);
                        ChunkOffsets.Capacity = ChunkOffsets.Count + entries;
                        for (var i = 0; i < entries; i++)
                        {
                            var offset = GetUInt64(8 + i * 8);
                            ChunkOffsets.Add(offset);
                        }
                    }
                }
                else if (Name == "stsz") // sample sizes
                {
                    Buffer = ReadPayload(fs, Size - 4);
                    int version = Buffer[0];
                    var uniformSizeOfEachSample = GetUInt(4);
                    var numberOfSampleSizes = GetUInt(8);
                    StszSampleCount = numberOfSampleSizes;

                    if (uniformSizeOfEachSample != 0)
                    {
                        // A non-zero sample_size means every sample has that size and no
                        // entry table follows (ISO/IEC 14496-12 8.7.3.2). The table read
                        // below would run off the end of the box, leaving SampleSizes
                        // empty and silently yielding no subtitles for such a track.
                        var count = (int)Math.Min(numberOfSampleSizes, MaxRunLengthEntries);
                        SampleSizes.Capacity = count;
                        for (var i = 0; i < count; i++)
                        {
                            SampleSizes.Add(uniformSizeOfEachSample);
                        }
                    }
                    else
                    {
                        var entries = ClampEntries(numberOfSampleSizes, 15, 4);
                        SampleSizes.Capacity = entries;
                        for (var i = 0; i < entries; i++)
                        {
                            SampleSizes.Add(GetUInt(12 + i * 4));
                        }
                    }
                }
                else if (Name == "stz2") // compact sample sizes
                {
                    // ISO/IEC 14496-12 8.7.3.3: same information as "stsz", with the entries
                    // packed into 4, 8 or 16 bits each instead of a full word. Bento4's
                    // mp4compact rewrites a file this way, and without this the track came out
                    // with no sample sizes at all, i.e. no subtitles.
                    Buffer = ReadPayload(fs, Size - 4);
                    var fieldSize = Buffer[7]; // 3 bytes reserved, then the field size
                    var sampleCount = GetUInt(8);
                    StszSampleCount = sampleCount;

                    if (fieldSize == 4 || fieldSize == 8 || fieldSize == 16)
                    {
                        var available = Buffer.Length - 12;
                        var maxEntries = fieldSize == 4 ? available * 2 : available / (fieldSize / 8);
                        var entries = (int)Math.Min(Math.Min(sampleCount, (uint)maxEntries), MaxRunLengthEntries);
                        SampleSizes.Capacity = entries;
                        for (var i = 0; i < entries; i++)
                        {
                            switch (fieldSize)
                            {
                                case 4:
                                    var b = Buffer[12 + i / 2];
                                    SampleSizes.Add((uint)(i % 2 == 0 ? b >> 4 : b & 0x0F));
                                    break;
                                case 8:
                                    SampleSizes.Add(Buffer[12 + i]);
                                    break;
                                default:
                                    SampleSizes.Add((uint)GetWord(12 + i * 2));
                                    break;
                            }
                        }
                    }
                }
                else if (Name == "stts") // sample table time to sample map
                {
                    //https://developer.apple.com/library/mac/#documentation/QuickTime/QTFF/QTFFChap2/qtff2.html#//apple_ref/doc/uid/TP40000939-CH204-SW1

                    Buffer = ReadPayload(fs, Size - 4);
                    int version = Buffer[0];
                    var numberOfSampleTimes = GetUInt(4);
                    var entries = ClampEntries(numberOfSampleTimes, 15, 8);

                    // Cheap pre-pass over the run lengths: the expansion below can reach
                    // millions of entries, and growing there from nothing means ~20 array
                    // doublings and copies of a multi-MB array.
                    Ssts.Capacity = SumSampleCounts(entries, 8, 8);
                    for (var i = 0; i < entries; i++)
                    {
                        var sampleCount = GetUInt(8 + i * 8);
                        var sampleDelta = GetUInt(12 + i * 8);
                        for (var j = 0; j < sampleCount && Ssts.Count < MaxRunLengthEntries; j++)
                        {
                            Ssts.Add(sampleDelta);
                        }

                        if (Ssts.Count >= MaxRunLengthEntries)
                        {
                            break;
                        }
                    }
                }
                else if (Name == "ctts") // composition time offset (PTS = DTS + offset); needed when B-frames make storage order differ from display order
                {
                    Buffer = ReadPayload(fs, Size - 4);
                    int version = Buffer[0];
                    var numberOfEntries = GetUInt(4);
                    var entries = ClampEntries(numberOfEntries, 15, 8);
                    Ctts.Capacity = SumSampleCounts(entries, 8, 8);
                    for (var i = 0; i < entries; i++)
                    {
                        var sampleCount = GetUInt(8 + i * 8);
                        var offsetRaw = GetUInt(12 + i * 8);
                        var sampleOffset = version == 1 ? unchecked((int)offsetRaw) : (int)offsetRaw;
                        for (var j = 0; j < sampleCount && Ctts.Count < MaxRunLengthEntries; j++)
                        {
                            Ctts.Add(sampleOffset);
                        }

                        if (Ctts.Count >= MaxRunLengthEntries)
                        {
                            break;
                        }
                    }
                }
                else if (Name == "stsc") // sample table sample to chunk map
                {
                    Buffer = ReadPayload(fs, Size - 4);
                    int version = Buffer[0];
                    var numberOfSampleTimes = GetUInt(4);
                    var entries = ClampEntries(numberOfSampleTimes, 20, 12);
                    Stsc.Capacity = entries;
                    for (var i = 0; i < entries; i++)
                    {
                        var firstChunk = GetUInt(8 + i * 12);
                        var samplesPerChunk = GetUInt(12 + i * 12);
                        var sampleDescriptionIndex = GetUInt(16 + i * 12);
                        Stsc.Add(new SampleToChunkMap { FirstChunk = firstChunk, SamplesPerChunk = samplesPerChunk, SampleDescriptionIndex = sampleDescriptionIndex });
                    }
                }

                fs.Seek((long)Position, SeekOrigin.Begin);
            }

            Discardable.AddRange(Enumerable.Repeat(0u,SampleSizes.Count));
            if(handlerType!="soun")Paragraphs=GetParagraphs(fs,handlerType);
        }

        private int ClampEntries(uint declaredEntries, int lastByteOfFirstEntry, int stride)
        {
            if (Buffer.Length <= lastByteOfFirstEntry)
            {
                return 0;
            }

            var fits = (Buffer.Length - lastByteOfFirstEntry + stride - 1) / stride;
            return declaredEntries < (uint)fits ? (int)declaredEntries : fits;
        }

        /// <summary>
        /// Total of the run-length sample counts, so the run-length expansion can allocate once.
        /// </summary>
        private int SumSampleCounts(int entries, int firstCountOffset, int stride)
        {
            ulong total = 0;
            for (var i = 0; i < entries; i++)
            {
                total += GetUInt(firstCountOffset + i * stride);
                if (total >= MaxRunLengthEntries)
                {
                    return MaxRunLengthEntries;
                }
            }

            return (int)total;
        }

        private List<Paragraph> GetParagraphs(Stream fs, string handlerType)
        {
            var paragraphs = new List<Paragraph>();
            uint samplesPerChunk = 1;
            var max = ChunkOffsets.Count;
            var index = 0;
            double totalTime = 0;
            var stscLookup = Stsc.GroupBy(p=>p.FirstChunk).ToDictionary(g=>g.Key,g=>g.First());
            for (var chunkIndex = 0; chunkIndex < max; chunkIndex++)
            {
                if (stscLookup.TryGetValue((uint)chunkIndex + 1, out var newSamplesPerChunk))
                {
                    samplesPerChunk = newSamplesPerChunk.SamplesPerChunk;
                }

                var chunkOffset = ChunkOffsets[chunkIndex];
                for (var i = 0; i < samplesPerChunk; i++)
                {
                    var p=new Paragraph();
                    if (index >= SampleSizes.Count || index >= Ssts.Count)
                    {
                        return paragraphs;
                    }

                    var sampleSize = SampleSizes[index];
                    var sampleTime = Ssts[index];
                    var before = totalTime;
                    totalTime += sampleTime / (double)TimeScale;

                    if (sampleSize > 2)
                    {
                        p.StartTime.TotalSeconds = before;

                        if (handlerType == "vide")
                        {
                            if (Discardable[index] == 1)
                            {
                                //TODO: cea 608 or 708 cc? What is the content?
                            }
                        }
                        else if (handlerType == "clcp" && Stsd?.Name == "c608")
                        {
                            //TODO: decode cea 608... but what is the content?

                            //var ccData = GetCcDataHelper.GetCcData(fs, chunkOffset + 4, sampleSize);

                            //var fieldData = new List<CcData>();
                            //// var seiData = GetCcDataHelper.GetSeiData(fs, chunkOffset , chunkOffset + sampleSize);

                            //var seiData = new byte[sampleSize];
                            //fs.Seek((long)chunkOffset, SeekOrigin.Begin);
                            //fs.Read(seiData, 0, seiData.Length);
                            //GetCcDataHelper.ParseCcDataFromSei(seiData, fieldData);

                            //if (fieldData.Count > 0)
                            //{
                            //    _cea608CcData.AddRange(fieldData);
                            //}
                        }
                        else
                        {
                            fs.Seek((long)chunkOffset, SeekOrigin.Begin);
                            var buffer = new byte[2];
                            fs.Read(buffer, 0, buffer.Length);
                            var textSize = (uint)GetWord(buffer, 0);

                            if (textSize > 0)
                            {
                                if (handlerType == "subp") // VobSub created with Mp4Box
                                {
                                    if (textSize > 100)
                                    {
                                        buffer = new byte[textSize + 2];
                                        fs.Seek((long)chunkOffset, SeekOrigin.Begin);
                                        fs.Read(buffer, 0, buffer.Length);
                                        SubPictures.Add(new SubPicture(buffer)); // TODO: Where is palette?
                                        paragraphs.Add(p);
                                    }
                                }
                                else
                                {
                                    buffer = new byte[textSize];
                                    fs.Read(buffer, 0, buffer.Length);
                                    p.Text = GetString(buffer, 0, (int)textSize).TrimEnd();

                                    if (_mdia.IsClosedCaption)
                                    {
                                        p.Text = MakeScenaristText(buffer);
                                    }
                                }
                            }
                        }
                    }

                    p.EndTime.TotalSeconds = totalTime;
                    index++;chunkOffset+=sampleSize;
                    if(!string.IsNullOrEmpty(p.Text))paragraphs.Add(p);
                }
            }

            return paragraphs;
        }

        private static string MakeScenaristText(byte[] buffer)
        {
            var sb = new StringBuilder();
            for (var j = 8; j < buffer.Length - 3; j++)
            {
                var h = buffer[j].ToString("X2").ToLowerInvariant();
                if (h.Length < 2)
                {
                    h = "0" + h;
                }

                sb.Append(h);
                if (j % 2 == 1)
                {
                    sb.Append(' ');
                }
            }

            var hex = sb.ToString();
            var errorCount = 0;
            var text = ScenaristClosedCaptions.GetSccText(hex, ref errorCount);
            if (text.StartsWith('n') && text.Length > 1)
            {
                text = "<i>" + text.Substring(1) + "</i>";
            }

            if (text.StartsWith("-n", StringComparison.Ordinal))
            {
                text = text.Remove(0, 2);
            }

            if (text.StartsWith("-N", StringComparison.Ordinal))
            {
                text = text.Remove(0, 2);
            }

            if (text.StartsWith('-') && !text.Contains(Environment.NewLine + "-"))
            {
                text = text.Remove(0, 1);
            }

            return text;
        }
    }
}
