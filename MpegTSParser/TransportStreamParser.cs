using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HdHomerunLib.MpegTSParser.Tables;

namespace HdHomerunLib.MpegTSParser
{
    public class TransportStreamParser
    {
        public TransportStreamParser()
        {
            // First we need a demux
            demux = new TSDemux(this);

            // And attach PAT and PSIP parsers to it
            patParser = new PATParser(demux, this);

            psipParser = new PSIPParser(demux, this);
        }

        public void AcceptPacket(TSPacket packet)
        {
            demux.AcceptPacket(packet);
        }

        public void AcceptPAT(PATTable patTable)
        {
            int currentVersion;

            if (this.patTable == null)
            {
                currentVersion = -1;
            }
            else
            {
                currentVersion = this.patTable.Version;
            }

            if (patTable.Version != currentVersion)
            {
                // New version of the table. Did we have a previous one?
                if (this.patTable != null)
                {
                    // Yep -- unregister pid data handlers
                    // TODO: OR should we keep hold of PMTParser here and do it from a method on it?
                }

                this.patTable = patTable;
                foreach (int program in patTable.Programs)
                {
                    uint programPID = patTable.PIDForProgram(program);

                    PMTParser pmtParser = new PMTParser(demux, this, program, programPID);
                }
            }
        }
    
        public void AcceptEITLocation(int tableNumber, uint PID)
        {
            if (eitPIDs[tableNumber] != PID)
            {
                EITParser parser = new EITParser(demux, this, tableNumber, PID);
                eitPIDs[tableNumber] = PID;
            }
        }

        public void AcceptCurrentSystemTime(DateTime time)
        {
            if (time != currentSystemTime)
            {
                System.Console.WriteLine("TIME: {0}", time.ToLongTimeString());
            }

            currentSystemTime = time;
        }

        public DateTime CurrentSystemTime
        {
            get { return currentSystemTime; }
        }

        public void AcceptChannel(Channel channel)
        {
            Channel existingChannel;

            if (false == channels.TryGetValue(channel.ChannelNum, out existingChannel))
            {
                channels.Add(channel.ChannelNum, channel);
            }
        }

        public IEnumerable<Channel> Channels
        {
            get { return channels.Values; }
        }

        Dictionary<string, Channel> channels = new Dictionary<string, Channel>();
        DateTime currentSystemTime;

        public TSDemux demux;
        PATParser patParser;
        PSIPParser psipParser;

        PATTable patTable;

        uint[] eitPIDs = new uint[2048];

    }
}