using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HdHomerunLib.MpegTSParser
{
    class PidAccumulator
    {
        public delegate void SectionComplete(uint PID, byte[] data);

        public event SectionComplete SectionCompleteEvent;

        public delegate void ContinuityError(uint PID, int previousCC, int currentCC);
        public event ContinuityError ContinuityErrorEvent;

        public PidAccumulator(TSPacket packet)
        {
            // TODO: This should filter out packets until one with PUSI set

            // Save PID value
            pid = packet.PID;

            // Initialize state
            ResetState();
        }

        private void ResetState()
        {
            // Empty accumulated packet list
            packets.Clear();

            // Set state to waiting for payload start
            state = AccumulationState.WaitingForStart;

            // No bytes accumulated yet
            bytesAccumulated = 0;
            packetsAccumulated = 0;
        }

        private void HandlePacket_WaitingForStart(TSPacket packet)
        {
            // We've yet to find a packet containing the start of a payload. Is this one?
            if (packet.PayloadUnitStart)
            {
                // Yes. Begin accumulating packets
                if (packets.Count > 0)
                {
                    // There shouldn't be any packets in the list yet
                    throw new ApplicationException("Expected empty packet list");
                }

                // This is the first packet -- add it and initialize continuity counter
                packets.Add(packet);
                bytesAccumulated = packet.PayloadLength;
                packetsAccumulated = 1;
                continuityCounter = packet.ContinuityCounter;

                state = AccumulationState.Accumulating;
            }
        }

        public void AddPacket(TSPacket packet)
        {
            switch (state)
            {
                case AccumulationState.WaitingForStart:
                    {
                        HandlePacket_WaitingForStart(packet);
                    }
                    break;

                case AccumulationState.Accumulating:
                    {
                        // Check for missing packets.
                        int expectedCC = (continuityCounter + 1) % 16;
                        if (packet.ContinuityCounter != expectedCC)
                        {
                            // We missed packets, so we need to toss what we've accumulated
                            // and start over waiting for the beginning of a section.
                            if (ContinuityErrorEvent != null)
                            {
                                ContinuityErrorEvent(pid, continuityCounter, packet.ContinuityCounter);
                            }
                            ResetState();

                            // This packet might be the start of a payload, so try it.
                            HandlePacket_WaitingForStart(packet);
                            break;
                        }
                        continuityCounter = packet.ContinuityCounter;

                        // Is this the start of a new section?
                        if (packet.PayloadUnitStart)
                        {
                            // Yes, so we have a complete section accumulated. Create a buffer for the data.
                            byte[] trailingPayload = null;
                            if (packet.HasTrailingPayload)
                            {
                                trailingPayload = packet.TrailingPayload;
                                bytesAccumulated += trailingPayload.Length;
                            }

                            byte[] data = new byte[bytesAccumulated];
                            int offset = 0;
                            foreach (TSPacket p in packets)
                            {
                                byte[] payload = p.Payload;
                                Buffer.BlockCopy(payload, 0, data, offset, payload.Length);
                                offset += payload.Length;
                            }
                            if (trailingPayload != null)
                            {
                                Buffer.BlockCopy(trailingPayload, 0, data, offset, trailingPayload.Length);
                            }

                            // Fire the accumulated payload event
                            if (SectionCompleteEvent != null)
                            {
                                try
                                {
                                    SectionCompleteEvent(pid, data);
                                }
                                catch (Exception e)
                                {
                                    System.Console.WriteLine("Exception while handling payload on PID {0} of size {1}: {2}",
                                        pid, data.Length, e.Message);
                                }
                            }

                            // Reset accumulation state and take this packet as the first of a new section
                            ResetState();
                            HandlePacket_WaitingForStart(packet);
                        }
                        else
                        {
                            // Another packet
                            packets.Add(packet);
                            packetsAccumulated++;
                            bytesAccumulated += packet.PayloadLength;
                        }
                    }
                    break;

                case AccumulationState.Invalid:
                    {
                        throw new ApplicationException("Invalid state during AddPacket()");
                    }
                    break;
            }
        }

        public int PacketsAccumulated
        {
            get { return packetsAccumulated; }
        }

        public int BytesAccumulated
        {
            get { return bytesAccumulated; }
        }

        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// PID we're accumulating packets for
        /// </summary>
        uint pid;

        // List of parsed TSPackets we've accumulated so far on this PID.
        List<TSPacket> packets = new List<TSPacket>();

        // Previous CC seen in last packet added
        int continuityCounter;

        /// <summary>
        /// State of packet accumulation for this PID
        /// </summary>
        enum AccumulationState
        {
            Invalid,
            WaitingForStart,
            Accumulating
        };
        AccumulationState state;

        /// <summary>
        /// Bytes accumulated so far
        /// </summary>
        int bytesAccumulated;

        /// <summary>
        /// Packets accumulated so far
        /// </summary>
        int packetsAccumulated;
    }

}
