using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HdHomerunLib.MpegTSParser.Tables;

namespace HdHomerunLib.MpegTSParser
{
    /// <summary>
    /// Given an MPEG transport stream, this will demultiplex packets and accumulate them into
    /// usable sized chunks of data. Elementary streams accumulate into PES packets, PIDs containing
    /// tables accumulate into Table Sections, etc.
    /// <para>
    /// Clients of the demux register to be sent completed payload sections by PID. There is also
    /// a generic event that is fired whenever any payload for any PID is available.
    /// </para>
    /// </summary>
    public class TSDemux
    {
        /// <summary>
        /// Delegate for handling a demuxed payload section (PES Packet, Table Section, etc.)
        /// </summary>
        /// <param name="PID">PID on which this payload was carried</param>
        /// <param name="data">Buffer containing the data</param>
        public delegate void PayloadSectionComplete(uint PID, byte[] data);

        /// <summary>
        /// Event fired whenever any complete payload section has been demuxed. Typically
        /// clients will register for payloads on individual PIDs rather than use this
        /// event. But this is provided as a catch-all.
        /// </summary>
        public event PayloadSectionComplete PayloadSectionCompleteEvent;

        /// <summary>
        /// Constructor.
        /// </summary>
	    public TSDemux(TransportStreamParser tsParser)
	    {
            pidAccumulators = new Dictionary<uint, PidAccumulator>();
            pidDataHandlers = new Dictionary<uint, PayloadSectionComplete>();
            this.tsParser = tsParser;
	    }

        /// <summary>
        /// Register a delegate to be called whenever a complete payload section is
        /// available on the given PID.
        /// </summary>
        /// <param name="PID">PID to monitor</param>
        /// <param name="handler">Delegate to be called when the payload section is available</param>
        public void RegisterPIDDataHandler(uint PID, PayloadSectionComplete handler)
        {
            pidDataHandlers.Add(PID, handler);
        }

        /// <summary>
        /// Register an ITableParser interface to be called whenever a complete payload
        /// section is available on the given PID. This is the same functionality as
        /// the other overload for RegisterPIDDataHandler(), but is a convenience provided
        /// for subclasses of TableParser.
        /// </summary>
        /// <param name="PID">PID to monitor</param>
        /// <param name="parser">ITableParser interface to handle the Table Section</param>
        public void RegisterPIDDataHandler(uint PID, TableParser parser)
        {
            pidDataHandlers.Add(PID,
                delegate(uint pid, byte[] data)
                {
                    parser.Parse(pid, data);
                });
        }

        /// <summary>
        /// Unregister a handler for a PID.
        /// </summary>
        /// <param name="PID"></param>
        public void UnregisterPIDDataHandler(uint PID)
        {
            pidDataHandlers.Remove(PID);
        }

        public int ContinuityErrors
        {
            get { return ccerrs; }
        }

        public int TotalPacketsSeen
        {
            get { return totalPacketsSeen; }
        }

        /// <summary>
        /// Takes the next MPEG transport stream packet as input to the demux. This is intended to
        /// be called by TransportStreamParser and not by public clients.
        /// </summary>
        /// <param name="Packet">Parsed TSPacket</param>
        internal void AcceptPacket(TSPacket Packet)
        {
            ++totalPacketsSeen;
#if false
            if ((totalPacketsSeen % 1000) == 0)
            {
                DumpStats();
            }
#endif

            // If it's a null packet don't do anything with it
            if (Packet.IsNullPacket)
            {
                return;
            }

            // Grab the accumulator for this packet's PID if present
            PidAccumulator accumulator = null;
            pidAccumulators.TryGetValue(Packet.PID, out accumulator);

            // Are we already accumulating packets for this PID?
            if (accumulator == null)
            {
                // Nope -- create an accumulator to use
                pidAccumulators[Packet.PID] = new PidAccumulator(Packet);
                accumulator = pidAccumulators[Packet.PID];

                accumulator.SectionCompleteEvent += delegate(uint pid, byte[] data)
                {
                    if (PayloadSectionCompleteEvent != null)
                    {
                        PayloadSectionCompleteEvent(pid, data);
                    }

                    PayloadSectionComplete dataHandler = null;
                    pidDataHandlers.TryGetValue(pid, out dataHandler);
                    if (dataHandler != null)
                    {
                        dataHandler(pid, data);
                    }
                };

                accumulator.ContinuityErrorEvent += delegate(uint pid, int cc1, int cc2)
                {
                    ccerrs++;
#if false
                    System.Console.WriteLine("Continuity error on PID {0} [{1}->{2}]. {3} errors in {4} packets ({5}%)",
                        pid, cc1, cc2, ccerrs, totalPacketsSeen, (double)ccerrs / (double)totalPacketsSeen * 100.0);
#endif
                };
            }

            // Send the packet to the accumulator
            accumulator.AddPacket(Packet);
        }

	    void DumpStats()
	    {
            System.Console.WriteLine("\n================ Section Accumulator Stats ===============");
            foreach (uint pid in pidAccumulators.Keys)
		    {
                PidAccumulator accumulator = pidAccumulators[pid];
                System.Console.WriteLine("PID {0}: packetCount={1}, byteCount={2}", 
                    pid, accumulator.PacketsAccumulated, accumulator.BytesAccumulated);
		    }
	    }

        /// <summary>
        /// Packet accumulators for each PID in the transport stream
        /// </summary>
        private Dictionary<uint, PidAccumulator> pidAccumulators;

        /// <summary>
        /// Payload section complete handlers for each PID a client has expressed interest in
        /// </summary>
        private Dictionary<uint, PayloadSectionComplete> pidDataHandlers;

        /// <summary>
        /// Reference to the TransportStreamParser that owns this demux
        /// </summary>
        private TransportStreamParser tsParser;

        /// <summary>
        /// Packet statistics for the transport stream
        /// </summary>
    	int totalPacketsSeen;
        int ccerrs;
    }
};


