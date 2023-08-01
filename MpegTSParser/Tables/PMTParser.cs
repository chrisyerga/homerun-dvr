using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HdHomerunLib.MpegTSParser.Tables
{
    class PMTParser: TableParser
    {
        public PMTParser(TSDemux Demux, TransportStreamParser tsParser, int ProgramNumber, uint PID)
        {
            this.tsParser = tsParser;
            programNumber = ProgramNumber;
            pid = PID;
            Demux.RegisterPIDDataHandler(PID, this);
        }

        public override void ParseTableSection(byte[] data, int offset)
        {
            // Validate table type
            switch (TableId)
            {
                case 192:
                case 193: // TODO: Not sure what these are, but the streams are filled with them
                    return;

                case (int)ParseUtils.TableType.PMT:
                    break;

                default:
                    {
                        throw new FormatException(string.Format("Unexpected table type of {0} while attempting to parse PMT", TableId));
                    }
            }

            // Validate program number
            if (TableIdExtension != programNumber)
            {
                throw new FormatException(string.Format("Expected program number not present in PMT. Expected {0}, got {1}",
                    programNumber, TableIdExtension));
            }

            // PMT can only be a single section in length
            if (SectionNumber != 0 || LastSectionNumber != 0)
            {
                throw new FormatException(string.Format("Expected zero for both SectionNumber ({0}) and LastSectionNumber ({1})",
                    SectionNumber, LastSectionNumber));
            }

            pcrPID = (uint)ParseUtils.GetUShort(data, offset) & 0x1FFF;
            offset += 2;

            int programInfoLength = ParseUtils.GetUShort(data, offset) & 0x0FFF;
            offset += 2;

//            System.Console.WriteLine("==== PMT [Program #{0}] ====", programNumber);
            while (programInfoLength > 0)
            {
                int tag = data[offset++];
                int length = (int)((uint)data[offset++]);
                offset += length;
//                System.Console.WriteLine("   Descriptor tag:{0} length:{1}", tag, length);
                programInfoLength -= length + 2;
            }

            int sectionLen = SectionLength - programInfoLength - 13;
            int esIndex = 1;
            while (sectionLen > 0)
            {
                int streamType = data[offset++];
                int esPid = ParseUtils.GetUShort(data, offset) & 0x1FFF;
                offset += 2;
                int esInfoLength = ParseUtils.GetUShort(data, offset) & 0x0FFF;
                offset += 2 + esInfoLength;
//                System.Console.WriteLine("   Elementary Stream {0}: type={1}, pid={2}, infoLen={3}",
//                    esIndex++, streamType, esPid, esInfoLength);

                sectionLen -= esInfoLength + 5;
            }
        }

        TransportStreamParser tsParser;
        int programNumber;
        uint pid;
        uint pcrPID;
    }
}
