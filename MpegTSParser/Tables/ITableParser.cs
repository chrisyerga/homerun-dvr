using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HdHomerunLib.MpegTSParser.Tables
{
    public interface ITableParser
    {
        void Parse(byte[] buffer);
    }
}
