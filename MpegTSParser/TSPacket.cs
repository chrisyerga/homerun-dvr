using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HdHomerunLib.MpegTSParser
{
    /// <summary>
    /// Class representing a 188-byte MPEG transport stream packet. This class handles parsing of
    /// the transport stream header portion of the packet that is consistent for all packet types.
    /// </summary>
    public class TSPacket
    {
        /// <summary>
        /// Constructs a TSPacket from a data buffer.
        /// </summary>
        /// <remarks>The class doesn't make a copy of the data, just holds a reference to the
        /// same buffer. So the buffer shouldn't be modified or reused.</remarks>
        /// <param name="data">188 byte array of bytes representing an MPEG transport stream
        /// packet. The byte array must be exactly 188 bytes.</param>
        public TSPacket(byte[] data)
        {
            // Validate length of data buffer
            if (data.Length != ParseUtils.MPEG_TS_PACKET_SIZE)
            {
                throw new ArgumentException("data should be 188 bytes in length");
            }

            // Don't copy the data -- just take a reference to it.
            this.data = data;
        }

        /// <summary>
        /// Constructs a TSPacket from a portion of an existing buffer. This constructor is
        /// used when TSPackets must be concstructed from a large buffer of data containing multiple
        /// transport stream packets.
        /// </summary>
        /// <param name="data">Buffer containing transport stream packets</param>
        /// <param name="offset">Offset within the buffer to the beginning of a MPEG trnasport stream
        /// packet. The buffer must contain 188 bytes of data beyond this offset, as this much data
        /// will be extracted from the buffer.</param>
        public TSPacket(byte[] data, int offset)
        {
            // Allocate a data buffer and copy the packet into it.
            this.data = new byte[ParseUtils.MPEG_TS_PACKET_SIZE];
            Buffer.BlockCopy(data, offset, this.data, 0, ParseUtils.MPEG_TS_PACKET_SIZE);
        }

        /// <summary>
        /// Parse the packet data. This method must be called before any of the other properties are
        /// used.
        /// </summary>
        public void ParseData()
        {
            // Sync byte must be present
		    if (data[0] != ParseUtils.MPEG_SYNC_BYTE)
		    {
			    throw new FormatException("Invalid MPEG sync byte");
            }

            // Check for transport error
            if ((data[1] & 0x80) != 0)
            {
                throw new FormatException("Transport error indicator set. Packet contents invalid.");
            }

		    // Extract PID
            pid = (uint)(((data[1] & 0x1F) << 8) | ((ushort)data[2] & 0xFF));

		    // PUS indicator
		    payloadUnitStart = (data[1] & 0x40) != 0;

		    // Various other fields
		    TSC = (data[3] >> 6) & 3;
            AFC = (data[3] >> 4) & 3;
            CC = data[3] & 15;

		    // Determine location of payload data
            payloadOffset = 4;
            if ((AFC & 2) != 0)
            {
                payloadOffset += (data[4]) + 1;
            }

		    // Validate offset
		    if (payloadOffset < 4 || payloadOffset > 188)
		    {
                throw new FormatException(string.Format("Payload offset {0} out of range", payloadOffset));
		    }

		    // Adjust by pointer if needed
		    if (payloadUnitStart)
		    {
                // This packet contains the beginning of a chunk of data. The next byte is the pointer
                // offset which indicates where in the payload the data begins. This allows the packet
                // to contain trailing bytes for the remainder of the previous data section, and the 
                // pointer offset points beyond it to the first byte of the section that starts in this
                // packet
                int pointer = data[payloadOffset];

                // Is there trailing data for the previous section in here as well?
                trailingPayloadLength = pointer;
                if (pointer != 0)
                {
                    // Yes -- capture that data.
                    trailingPayloadOffset = payloadOffset + 1;
                }

                // Update payload offset
			    payloadOffset += pointer+1;
		    }

		    // Validate offset
		    if (payloadOffset < 4 || payloadOffset > 188)
		    {
                throw new FormatException(string.Format("Payload offset {0} out of range", payloadOffset));
            }

		    // Set payload pointer
		    payloadLength = 188 - payloadOffset;
        }

        /// <summary>
        /// True if the packet is a null packet
        /// </summary>
        public bool IsNullPacket
        {
            get { return PID == ParseUtils.MPEG_NULL_PID; }
        }

        /// <summary>
        /// Returns the PID for this packet
        /// </summary>
        public uint PID { get { return pid; } }

        /// <summary>
        /// Returns true if the MPEG PUSI flag is set.
        /// </summary>
        public bool PayloadUnitStart { get { return payloadUnitStart; } }

        /// <summary>
        /// Length of the data payload portion of the packet. This is the portion of the packet
        /// not including the header and adaptation fields. If the packet contains multiple
        /// payloads (payload unit start set and the payoad pointer non-zero) then this contains
        /// the newly-beginning payload. In this case, the trailing payload for the previous
        /// section is available in the trailingPayloadXXX members.
        /// </summary>
        public int PayloadLength { get { return payloadLength; } }

        /// <summary>
        /// Returns true if there are multiple payloads in this packet. If true then the
        /// TrailingPayload property can be used to retrieve the trailing portion of
        /// the previous section.
        /// </summary>
        public bool HasTrailingPayload
        {
            get { return trailingPayloadLength > 0; }
        }

        /// <summary>
        /// Returns a byte array containing the main payload contained in the packet.
        /// </summary>
        public byte[] Payload
        {
            get
            {
                byte[] result = new byte[payloadLength];
                
                Buffer.BlockCopy(data, payloadOffset, result, 0, payloadLength);
                return result;
            }
        }

        /// <summary>
        /// If multiple payloads are present, this returns a buffer containing the trailing payload of the
        /// previous section
        /// </summary>
        public byte[] TrailingPayload
        {
            get
            {
                byte[] result = new byte[trailingPayloadLength];

                Buffer.BlockCopy(data, trailingPayloadOffset, result, 0, trailingPayloadLength);
                return result;
            }
        }

        /// <summary>
        /// Returns continuity counter for this packet.
        /// </summary>
        public int ContinuityCounter
        {
            get { return CC; }
        }

        //-----------------------------------------------------------------------------------------
        
        /// <summary>
        /// Packet data. This contains the full 188 byte MPEG packet.
        /// </summary>
        private byte[] data;

        /// <summary>
        /// The pid for this packet
        /// </summary>
        private uint pid;

        /// <summary>
        /// Length of the data payload portion of the packet. This is the portion of the packet
        /// not including the header and adaptation fields. If the packet contains multiple
        /// payloads (payload unit start set and the payoad pointer non-zero) then this contains
        /// the newly-beginning payload. In this case, the trailing payload for the previous
        /// section is available in the trailingPayloadXXX members.
        /// </summary>
        private int payloadLength;

        /// <summary>
        /// Offset into data[] of the payload.
        /// </summary>
        private int payloadOffset;

        /// <summary>
        /// If non-zero, then this packet contains multiple payloads -- one is the start of the
        /// next section and the this is the remainder of the previous section.
        /// </summary>
        private int trailingPayloadLength;

        /// <summary>
        /// If present, the offset in data[] to the previous payload remainder.
        /// </summary>
        private int trailingPayloadOffset;

        /// <summary>
        /// MPEG PUSI flag indicating this packet contains the beginning of a payload unit (PES
        /// packet or table section)
        /// </summary>
        private bool payloadUnitStart;

        /// <summary>
        /// MPEG transport scrambling control flags.
        /// </summary>
        private int TSC;

        /// <summary>
        /// MPEG adaptation field control flags.
        /// </summary>
        private int AFC;

        /// <summary>
        /// Continuity counter
        /// </summary>
        private int CC;
    }
}
