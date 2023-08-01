using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HdHomerunLib.MpegTSParser.Tables
{
    public class PATTable
    {
        public PATTable(int version, int[] programs, uint[] PIDs)
        {
            this.version = version;

            programToPIDMap = new Dictionary<int, uint>(programs.Length);
            for (int index = 0; index < programs.Length; ++index)
            {
                programToPIDMap[programs[index]] = PIDs[index];
            }
        }

        public Dictionary<int, uint> PAT
        {
            get
            {
                return new Dictionary<int, uint>(programToPIDMap);
            }
        }

        public IEnumerable<int> Programs
        {
            get { return programToPIDMap.Keys; }
        }

        public uint PIDForProgram(int program)
        {
            return programToPIDMap[program];
        }

        public int Version { get { return version; } }

        private int version;
        private Dictionary<int, uint> programToPIDMap;
    }

    public class PATParser : TableParser
    {
        public PATParser(TSDemux demux, TransportStreamParser tsParser)
        {
            demux.RegisterPIDDataHandler(ParseUtils.MPEG_PID_PAT, this);
            this.demux = demux;
            this.tsParser = tsParser;
        }

        public override void ParseTableSection(byte[] data, int offset)
        {
            // Validate table type
		    if (TableId != (int)ParseUtils.TableType.PAT)
		    {
                throw new FormatException(string.Format("Unexpected table type of {0} while attempting to parse PAT", TableId));
		    }

            // Parse the PAT
		    int tableCount = (SectionLength - 9) / 4;
            int[] programs = new int[tableCount];
            uint[] PIDs = new uint[tableCount];
		    for (int index=0; index<tableCount; ++index)
		    {
                int program = ParseUtils.GetUShort(data, offset);
			    offset += 2;

                uint pid = (uint)ParseUtils.GetUShort(data, offset);
                pid = pid & 0x1FFF;
                offset += 2;

                programs[index] = program;
                PIDs[index] = pid;
//                System.Console.WriteLine("Program #{0} on PID #{1}", program, pid);
		    }

            // Construct the PAT object
            PATTable table = new PATTable(VersionNumber, programs, PIDs);

            // And give it to the parser
            tsParser.AcceptPAT(table);
	    }

        int TSID;
        TSDemux demux;
        Dictionary<int, PMTParser> pmts;
        TransportStreamParser tsParser;
    }
}
