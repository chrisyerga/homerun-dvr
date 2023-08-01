using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HdHomerunLib.MpegTSParser.Tables
{
    public abstract class TableParser
    {
        public TableParser()
        {
        }

        /// <summary>
        /// Sub-classes must override this to provide the code to parse the specifics
        /// of their table. By the time this method is called, the generic table header
        /// will have already been parsed by this base class.
        /// </summary>
        /// <param name="data">Table data</param>
        /// <param name="offset">Offset into table data where section begins</param>
        public abstract void ParseTableSection(byte[] data, int offset);

        public void Parse(uint PID, byte[] data)
        {
            // Save PID we found this table in
            pid = PID;

            // Table type
            int offset = 0;
            tableId = data[offset];
            ++offset;

            // Table section syntax (usually true meaning long form)
            sectionSyntax = (data[1] & 0x80) != 0;

            // Table section length
            sectionLength = ParseUtils.GetUShort(data, offset) & 0xFFF;
            offset += 2;

            // Is this a long form section?
            if (sectionSyntax)
            {
                // Yes -- parse the additional header fields

                // Table ID Extension
                tableIdExtension = ParseUtils.GetUShort(data, offset);
                offset += 2;

                // Table version
                versionNumber = (data[offset] >> 1) & 0x1F;

                // The despised (and deprecated in ATSC) current/next indicator
                currentNext = (data[offset] & 1) != 0;
                ++offset;

                // Section number and last section number
                sectionNumber = data[offset];
                ++offset;
                lastSectionNumber = data[offset];
                ++offset;
            }

            // Header is parsed. Now parse the section data.
            ParseTableSection(data, offset);
        }

        public int TableId
        {
            get { return tableId; }
        }

        public bool SectionSyntax
        {
            get { return sectionSyntax; }
        }

        public int SectionLength
        {
            get { return sectionLength; }
        }

        public int TableIdExtension
        {
            get
            {
                if (sectionSyntax)
                {
                    return tableIdExtension;
                }
                else
                {
                    throw new InvalidOperationException("TableIDExtension not present due to short-form syntax");
                }
            }
        }

        public int VersionNumber
        {
            get
            {
                if (sectionSyntax)
                {
                    return versionNumber;
                }
                else
                {
                    throw new InvalidOperationException("VersionNumber not present due to short-form syntax");
                }
            }
        }

        public int SectionNumber
        {
            get
            {
                if (sectionSyntax)
                {
                    return sectionNumber;
                }
                else
                {
                    throw new InvalidOperationException("SectionNumber not present due to short-form syntax");
                }
            }
        }

        public int LastSectionNumber
        {
            get
            {
                if (sectionSyntax)
                {
                    return lastSectionNumber;
                }
                else
                {
                    throw new InvalidOperationException("LastSectionNumber not present due to short-form syntax");
                }
            }
        }

        uint pid;
        int tableId;
        bool sectionSyntax;
        int sectionLength;
        int tableIdExtension;
        int versionNumber;
        bool currentNext;
        int sectionNumber;
        int lastSectionNumber;
    }
}
