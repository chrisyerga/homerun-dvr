using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HdHomerunLib.MpegTSParser.Tables
{
    class EITParser : TableParser
    {
        public EITParser(TSDemux demux, TransportStreamParser tsParser, int tableNumber, uint PID)
        {
            this.demux = demux;
            this.tsParser = tsParser;
            this.tableNumber = tableNumber;

            demux.RegisterPIDDataHandler(PID, this);
        }

        public override void ParseTableSection(byte[] data, int offset)
        {
            // Validate table type
		    if (TableId != (int)ParseUtils.TableType.EIT)
		    {
                throw new FormatException(string.Format("Unexpected table type of {0} while attempting to parse EIT", TableId));
		    }

            // Protocol version always zero
            if (data[offset] != 0)
            {
                throw new FormatException(string.Format("Unexpected Protocol Version of {0} while attempting to parse EIT", data[offset]));
            }
            ++offset;

            // Event count
            int eventCount = data[offset];
            ++offset;

            // Loop for each event
            for (int index=0; index<eventCount; ++index)
            {
                int eventID = ParseUtils.GetUShort(data, offset);
                offset += 2;
                eventID = eventID & 0x3FF;

                int time = ParseUtils.GetULong(data, offset);
                offset += 4;
                TimeSpan delta = TimeSpan.FromSeconds(time);
                DateTime gpsBase = new DateTime(1980, 1, 6) - TimeSpan.FromSeconds(14);
                DateTime startTime = gpsBase + delta;
                startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
                startTime = startTime.ToLocalTime();
                offset += 3; // skip duration

                int titleLength = data[offset];
                ++offset;

                string title = ParseUtils.GetMSSString(data, offset, titleLength);
                offset += titleLength;

                System.Console.WriteLine("EIT{0}: Source={1}  |  {2} - {3}", tableNumber, TableIdExtension, startTime, title);

                if ("Cheers".CompareTo(title) == 0)
                {
                    int poop = 1;
                }

                int descriptorLength = ParseUtils.GetUShort(data, offset);
                descriptorLength = descriptorLength & 0xFFF;

                // ---descriptors
                offset += descriptorLength+2;
            }
	    }

        int tableNumber;
        TSDemux demux;
        TransportStreamParser tsParser;
    }
}
