using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HdHomerunLib.MpegTSParser
{
    public class ParseUtils
    {
        // MPEG constants -- general
        public const byte MPEG_SYNC_BYTE = 0x47;
        public const int MPEG_TS_PACKET_SIZE = 188;

        // MPEG PID constants
        public const uint MPEG_PID_PAT = 0x0000;
        public const uint MPEG_PID_CAT = 0x0001;
        public const uint MPEG_PID_TSDT = 0x0002;
        public const uint MPEG_PID_ATSC_PSIP = 0x1FFB;
        public const uint MPEG_NULL_PID = 8191;
        public const uint MAX_PID = 8191;
        public const uint MAX_PID_COUNT = 8192;

        // MPEG constants -- SI Tables
        public enum TableType
        {
            PAT = 0x00,
            CAT = 0x01,
            PMT = 0x02,

            NIT_ACTUAL = 0x40,
            NIT_OTHER = 0x41,

            SDT_ACTUAL = 0x42,
            SDT_OTHER = 0x46,

            EIT_ACTUAL_PF = 0x4E,
            EIT_OTHER_PF = 0x4F,
            EIT_ACTUAL_MIN = 0x50,
            EIT_ACTUAL_MAX = 0x5F,
            EIT_OTHER_MIN = 0x60,
            EIT_OTHER_MAX = 0x6F,
            TDT = 0x70,
            TOT = 0x73,
            SLT = 0x80,
            SLT_EIT_MIN = 0x81,
            MGT = 0xC7,             // Master Guide Table
            TVCT = 0xC8,            // Terrestrial Virtual Channel Table
            CVCT = 0xC9,            // Cable Virtual Channel Table
            RRT = 0xCA,             // Rating Region Table
            EIT = 0xCB,             // Event Information Table
            ETT = 0xCC,             // Extended Text Table
            STT = 0xCD,             // System Time Table
            SLT_EIT_MAX = 0xFE
        }

        // Extract big-endian ushort from byte buffer
        public static int GetUShort(byte[] buffer, int offset)
        {
            uint result = (uint)(buffer[offset]) << 8;
            result |= (uint)(buffer[offset + 1]);

            return (int)result;
        }

        // Extract big-endian ulong from byte buffer
        public static int GetULong(byte[] buffer, int offset)
        {
            uint result = (uint)(buffer[offset]) << 24;

            result |= (uint)(buffer[offset + 1]) << 16;
            result |= (uint)(buffer[offset + 2]) << 8;
            result |= (uint)(buffer[offset + 3]);

            return (int)result;
        }

        public static string GetMSSString(byte[] data, int offset, int length)
        {
            if (length <= 6)
            {
                // Not sure of the point -- no text data
                return "";
            }

            // Number of strings
            if (data[offset] != 1)
            {
                throw new NotImplementedException("GetMSSString() can't handle multiple strings");
            }

            // Skip number of strings and ISO language code
            offset += 4;

            // Number of segments
            if (data[offset] != 1)
            {
                throw new NotImplementedException("GetMSSString() can't handle multiple segments");
            }
            ++offset;

            // Compression Type
            if (data[offset] != 0)
            {
                throw new NotImplementedException("GetMSSString() can't handle compression");
            }
            ++offset;

            // Mode (encoding)
            if (data[offset] != 0)
            {
                throw new NotImplementedException("GetMSSString() can't handle encodings other than Latin-1");
            }
            ++offset;

            // Get string
            int strLen = data[offset];
            ++offset;

            StringBuilder b = new StringBuilder();
            for (int index = 0; index < strLen; ++index)
            {
                b.Append((char)data[offset]);
                ++offset;
            }
            return b.ToString();
        }

        public static DateTime GetSystemTime(byte[] data, int offset)
        {
            int time = ParseUtils.GetULong(data, offset);
            TimeSpan delta = TimeSpan.FromSeconds(time);
            DateTime gpsBase = new DateTime(1980, 1, 6) - TimeSpan.FromSeconds(14);
            DateTime result = gpsBase + delta;

            result = DateTime.SpecifyKind(result, DateTimeKind.Utc);
            result = result.ToLocalTime();

            return result;
        }
    }
}
