using System;
using System.Collections.Generic;
using Nikse.SubtitleEdit.Core.Common;
namespace Nikse.SubtitleEdit.Logic.ClassicFormats.TeletextSupport
{
    public sealed class ClassicTeletextPes
    {
        private readonly byte[] _dataBuffer;
        public ulong? PresentationTimestamp {get;}
        private ClassicTeletextPes(byte[] bytes,ulong pts){_dataBuffer=bytes;PresentationTimestamp=pts;}
        public static ClassicTeletextPes FromTeletextPayload(byte[] bytes,ulong pts)=>new ClassicTeletextPes(bytes,pts);
        public static bool IsTeletextPayload(byte[] bytes)=>bytes.Length>=46 && bytes[0]>=0x10 && bytes[0]<=0x1f && (bytes[1]==2 || bytes[1]==3 || bytes[1]==0xff) && bytes[2]==44;
        public List<int> PrepareTeletext()
        {
            var pages = new List<int>();
            var i = 1;
            while (i <= _dataBuffer.Length - 6)
            {
                var dataUnitId = _dataBuffer[i++];
                var dataUnitLen = _dataBuffer[i++];
                if (dataUnitId == (int)ClassicTeletext.DataUnitT.DataUnitEbuTeletextNonSubtitle || dataUnitId == (int)ClassicTeletext.DataUnitT.DataUnitEbuTeletextSubtitle)
                {
                    if (dataUnitLen == 44) // teletext payload has always size 44 bytes
                    {
                        // reverse endianness (via lookup table), ETS 300 706, chapter 7.1
                        for (var j = 0; j < dataUnitLen && i + j < _dataBuffer.Length; j++)
                        {
                            _dataBuffer[i + j] = ClassicTeletextHamming.Reverse8[_dataBuffer[i + j]];
                        }

                        if (_dataBuffer.Length > i + 43)
                        {
                            var pageNumber = ClassicTeletext.GetPageNumber(new ClassicTeletext.TeletextPacketPayload(_dataBuffer, i));
                            if (!pages.Contains(pageNumber) && pageNumber > 0)
                            {
                                pages.Add(pageNumber);
                            }
                        }
                    }
                }

                i += dataUnitLen;
            }

            return pages;
        }

        public Dictionary<int, Paragraph> GetTeletext(ClassicTeletextRunSettings teletextRunSettings, int pageNumber, int pageNumberBcd)
        {
            var timestamp = PresentationTimestamp.HasValue ? PresentationTimestamp.Value / 90 : 40;

            // do not allow timestamp to go back - treat lower timestamp as a reset/overflow
            var lastTimestamp = teletextRunSettings.GetLastTimestamp(pageNumber);
            teletextRunSettings.SetLastTimestamp(pageNumber, timestamp);
            timestamp += teletextRunSettings.GetAddTimestamp(pageNumber);
            if (lastTimestamp > 0 && lastTimestamp > timestamp)
            {
                teletextRunSettings.SetAddTimestamp(pageNumber, lastTimestamp);
            }

            // offset all time codes if first timestamp in ts file is > 1 sec
            timestamp = teletextRunSettings.SubtractStartMs(timestamp);

            if (timestamp < 40)
            {
                timestamp = 40; // ClassicTeletext.cs will subtract 40 ms (1 frame @25 fps) and this value must not be below 0
            }

            var teletextPages = new Dictionary<int, Paragraph>();
            var i = 1;
            while (i <= _dataBuffer.Length - 6)
            {
                var dataUnitId = _dataBuffer[i++];
                var dataUnitLen = _dataBuffer[i++];
                if (dataUnitId == (int)ClassicTeletext.DataUnitT.DataUnitEbuTeletextNonSubtitle || dataUnitId == (int)ClassicTeletext.DataUnitT.DataUnitEbuTeletextSubtitle)
                {
                    if (dataUnitLen == 44 && _dataBuffer.Length > i + 43) // teletext payload has always size 44 bytes
                    {
                        ClassicTeletext.ProcessTelxPacket((ClassicTeletext.DataUnitT)dataUnitId, new ClassicTeletext.TeletextPacketPayload(_dataBuffer, i), timestamp, teletextRunSettings, pageNumberBcd, pageNumber);
                    }
                }
                i += dataUnitLen;
            }

            if (teletextRunSettings.PageNumberAndParagraph.ContainsKey(pageNumber) && teletextRunSettings.PageNumberAndParagraph[pageNumber] != null)
            {
                if (teletextPages.ContainsKey(pageNumber))
                {
                    teletextPages[pageNumber] = teletextRunSettings.PageNumberAndParagraph[pageNumber];
                }
                else
                {
                    teletextPages.Add(pageNumber, teletextRunSettings.PageNumberAndParagraph[pageNumber]);
                }
            }
            teletextRunSettings.PageNumberAndParagraph.Clear();
            return teletextPages;
        }

}}
