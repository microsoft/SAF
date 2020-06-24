using System;
using System.Collections.Generic;
using System.Text;

namespace SAF
{
    public class SAFCommonHeader
    {
        public string Name { get; set; }
        public long Length { get; set; }
        public string MD5Hash { get; set; }
        public DateTime ModTime { get; set; }
        public SAFEntryType Type { get; set; }
    }

    public enum SAFEntryType
    {
        File,
        Directory
    }
}
