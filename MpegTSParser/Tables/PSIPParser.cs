using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HdHomerunLib.MpegTSParser.Tables
{
    public class Channel
    {
        public Channel(int TSID, int ProgramNumber, int ChannelNumMajor, int ChannelNumMinor, int SourceID, string name)
        {
            tsid = TSID;
            programNumber = ProgramNumber;
            channelMajorNumber = ChannelNumMajor;
            channelMinorNumber = ChannelNumMinor;
            sourceID = SourceID;
            callsign = name;
        }

        public int TSID { get { return tsid; }}
        public int ProgramNumber { get { return programNumber; }}
        public int ChannelNumMajor { get { return channelMajorNumber; } }
        public int ChannelNumMinor { get { return channelMinorNumber; } }
        public int SourceID { get { return sourceID; } }
        public string Callsign { get { return callsign; } }
        public string ChannelNum { get { return channelMajorNumber.ToString() + "-" + channelMinorNumber; } }

        // Info gotten from virtual channel table
        int tsid;
        int programNumber;
        int channelMajorNumber;
        int channelMinorNumber;
        int sourceID;
        string callsign;

        // Info correlated later after channel scan
        int qamFreq;
        List<uint> esPIDs;
    }

    public class PSIPParser : TableParser
    {
        public PSIPParser(TSDemux demux, TransportStreamParser tsParser)
        {
            demux.RegisterPIDDataHandler(ParseUtils.MPEG_PID_ATSC_PSIP, this);
            this.tsParser = tsParser;
        }

        public bool PSIPresent
        {
            get
            {
                return foundMGT;
            }
        }

        public override void ParseTableSection(byte[] data, int offset)
        {
            switch (TableId)
            {
                case (int)ParseUtils.TableType.MGT:
                    ParseMGT(data, offset);
                    foundMGT = true;
                    break;

                case (int)ParseUtils.TableType.TVCT:
                    ParseTVCT(data, offset);
                    break;

                case (int)ParseUtils.TableType.CVCT:
                    ParseTVCT(data, offset);
                    break;

                case (int)ParseUtils.TableType.RRT:
                    break;

                case (int)ParseUtils.TableType.STT:
                    ParseSTT(data, offset);
                    break;

                default:
                    System.Console.WriteLine("Unexpected PSIP table type [{0}] on PID 0x1FFB", TableId);
                    break;
            }
        }

        public void ParseSTT(byte[] data, int offset)
        {
            // Validate table type
            if (TableId != (int)ParseUtils.TableType.STT)
            {
                throw new FormatException(string.Format("Unexpected table type of {0} while attempting to parse ATSC STT", data[0]));
            }
            
            // Protocol version should be 0
            int atscProtocolVersion = data[offset++];
            if (atscProtocolVersion != 0)
            {
                throw new FormatException(string.Format("ATSC protocol version {0} instead of 0x0000 in MGT", atscProtocolVersion));
            }

            // TableIDExtension is 0 for STT as well
            if (TableIdExtension != 0)
            {
                throw new FormatException(string.Format("Invalid table_id_extension of {0} in ATSC STT", TableIdExtension));
            }

            // Parse the current time
            DateTime systemTime = ParseUtils.GetSystemTime(data, offset);
            offset += 4;
            tsParser.AcceptCurrentSystemTime(systemTime);

            // TODO: Remainder is related to DST transitions. Figure that out someday
        }


        public void ParseTVCT(byte[] data, int offset)
        {
            if ((TableId != (int)ParseUtils.TableType.TVCT) && (TableId != (int)ParseUtils.TableType.CVCT))
            {
                throw new FormatException(string.Format("Unexpected table type of {0} while attempting to parse ATSC TVCT/CVCT", data[0]));
            }

            int atscProtocolVersion = data[offset++];
            if (atscProtocolVersion != 0)
            {
                throw new FormatException(string.Format("ATSC protocol version {0} instead of 0x0000 in TVCT", atscProtocolVersion));
            }

            int channelCount = data[offset++];

            for (int index = 0; index < channelCount; ++index)
            {
                // Channel name
                StringBuilder callsignBuilder = new StringBuilder();
                for (int charIndex = 0; charIndex < 7; ++charIndex)
                {
                    callsignBuilder.Append((char)ParseUtils.GetUShort(data, offset));
                    offset += 2;
                }

                // Channel number
                int majorNumber = ParseUtils.GetUShort(data, offset);
                majorNumber = (majorNumber >> 2) & 0x3FF;
                offset += 2;
                int minorNumber = data[offset];
                offset++;
                
                // Stuff we don't care about
                int modulationMode = data[offset++];
                int carrierFreq = ParseUtils.GetULong(data, offset);
                offset += 4;
                int channelTSID = ParseUtils.GetUShort(data, offset);
                offset += 2;
                int programNumber = ParseUtils.GetUShort(data, offset);
                offset += 2;

                // Skip a bunch of binary flags
                offset += 2;

                // Source ID
                int sourceID = ParseUtils.GetUShort(data, offset);
                offset += 2;

                // Descriptor loop
                int descriptorLength = ParseUtils.GetUShort(data, offset);
                offset += 2;
                descriptorLength = descriptorLength & 0x3FF;

                // ---- parse descriptors ----

                offset += descriptorLength;

                Channel channel = new Channel(channelTSID, programNumber, majorNumber, minorNumber, sourceID, callsignBuilder.ToString());
                tsParser.AcceptChannel(channel);
                System.Console.WriteLine(" {0} | {1}-{2} : {3}", sourceID, majorNumber, minorNumber, callsignBuilder.ToString());
            }
        }



        public void ParseMGT(byte[] buffer, int offset)
        {
            if (TableId != (int)ParseUtils.TableType.MGT)
            {
                throw new FormatException(string.Format("Unexpected table type of {0} while attempting to parse ATSC MGT", buffer[0]));
            }

            int atscProtocolVersion = buffer[offset++];
            if (atscProtocolVersion != 0)
            {
                throw new FormatException(string.Format("ATSC protocol version {0} instead of 0x0000 in MGT", atscProtocolVersion));
            }

            int tableCount = ParseUtils.GetUShort(buffer, offset);
            offset += 2;

            for (int index = 0; index < tableCount; ++index)
            {
                int tableType = ParseUtils.GetUShort(buffer, offset);
                offset += 2;

                int pid = ParseUtils.GetUShort(buffer, offset);
                pid = pid & 0x1FFF;
                offset += 2;

                int ttVersion = buffer[offset] & 0x1F;
                offset++;

                int byteCount = ParseUtils.GetULong(buffer, offset);
                offset += 4;

                int descriptorLength = ParseUtils.GetUShort(buffer, offset);
                descriptorLength = descriptorLength & 0x0FFF;
                offset += 2;
#if false
                System.Console.WriteLine("MGT table reference #{0}, type={1}, pid={2}, length={3}, descriptorLength={4}",
                    index, tableType, pid, byteCount, descriptorLength);
#endif
                if (tableType > 0x100 && tableType < 0x180)
                {
                    tsParser.AcceptEITLocation(tableType - 0x100, (uint)pid);
                }

                if (tableType > 0x200 && tableType < 0x280)
                {
                    System.Console.WriteLine("###### FOUND ETT on PID {0} ######################################", pid);
                }
            }
        }

        bool foundMGT = false;
        TransportStreamParser tsParser;
    }
}
