using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace SAF
{
    public class SAFBase<THeader>
    {
        protected SAFMode mode;
        protected Stream baseStream;
        private StreamWriter streamWriter;
        public string SAFArchiveHeader { get; }

        public SAFBase(Stream baseStream, SAFMode mode, string ArchiveHeader = "SAF")
        {
            this.mode = mode;
            this.baseStream = baseStream;

            if (mode == SAFMode.Write)
            {
                // the header identifies this as a SAF archive (and potentially the format revision).
                if (string.IsNullOrWhiteSpace(ArchiveHeader))
                {
                    throw new ArgumentException("SAF file header string cannot be null.");
                }

                this.SAFArchiveHeader = ArchiveHeader;
                streamWriter = new StreamWriter(baseStream);

                // write the file header
                streamWriter.WriteLine(ArchiveHeader);
            }
            else
            {
                // read the file header
                this.SAFArchiveHeader = ReadNextLineUnbuffered();
            }
        }

        public THeader ReadNextHeader()
        {
            if (mode != SAFMode.Read) 
                throw new InvalidOperationException("Archive not opened for reading.");

            var headerSize = GetHeaderSize();
            if (headerSize == -1) return default;

            Span<byte> headerBytes = stackalloc byte[headerSize];
            var bytesRead = 0;
            while (bytesRead < headerSize){
                bytesRead += baseStream.Read(headerBytes.Slice(bytesRead));
            }
            
            return JsonSerializer.Deserialize<THeader>(headerBytes);
        }

        public void WriteNextHeader(THeader header)
        {
            if (mode != SAFMode.Write)
                throw new InvalidOperationException("Archive not opened for writing..");

            var headerBytes = JsonSerializer.SerializeToUtf8Bytes<THeader>(header);
            var headerSize = headerBytes.Length;
            streamWriter.WriteLine(headerSize.ToString());
            streamWriter.Flush();
            baseStream.Write(headerBytes);
        }

        private int GetHeaderSize()
        {
            var nextLine = ReadNextLineUnbuffered();
            if (nextLine == null) return -1; //end of file.
            return int.Parse(nextLine);
        }
        
        private string ReadNextLineUnbuffered()
        {
            /* 
             this method is required because stream reader performs internal buffering,
             so it will read more than potentially desired (causing the base stream position
             to be incorrect.)
             */
            var sb = new StringBuilder();
            while (true)
            {
                var nextByte = baseStream.ReadByte();
                if (nextByte == -1) return null; //hit end of stream

                sb.Append((char)nextByte);

                if ((char)nextByte == '\n') break;
            }

            return sb.ToString().Trim();
        }
    }

    public enum SAFMode {
        Read,
        Write
    }
}
