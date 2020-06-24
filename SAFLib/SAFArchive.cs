using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Linq;

namespace SAF
{
    public class SAFArchive : SAFBase<SAFCommonHeader>
    {
        public const string SAF_ARCHIVE_HEADER_STRING = "SAF1";

        private string basePath;
        private int basePathLength;
        private bool calculateChecksums;

        public SAFArchive(Stream baseSteam, SAFMode mode, string basePath, bool calculateChecksums = true)
            : base(baseSteam, mode, SAF_ARCHIVE_HEADER_STRING)
        {
            this.basePath = Path.GetFullPath(basePath).Trim(Path.DirectorySeparatorChar);
            this.basePathLength = this.basePath.Length;
            this.calculateChecksums = calculateChecksums;
        }

        public void WriteEntryToArchive(FileSystemInfo info)
        {
            SAFCommonHeader header;

            // remove the base path and the directory separator
            var entryName = info.FullName.Substring(basePathLength + 1);

            // if we're a directory this is simplier
            if ((info.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
            {
                header = new SAFCommonHeader
                {
                    Name = entryName,
                    Length = 0,
                    MD5Hash = null,
                    ModTime = info.LastWriteTime,
                    Type = SAFEntryType.Directory
                };

                WriteNextHeader(header);
                return;
            }
            // we're working with a normal file

            header = new SAFCommonHeader
            {
                Name = entryName,
                Length = (new FileInfo(info.FullName)).Length,
                MD5Hash = (this.calculateChecksums) ? CalculateChecksum(info.FullName) : null,
                ModTime = info.LastWriteTime,
                Type = SAFEntryType.File
            };

            WriteNextHeader(header);

            using (var fs = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.CopyTo(baseStream);
            }
        }

        public FileSystemInfo ReadEntryToDisk()
        {
            var header = ReadNextHeader();
            if (header == null) return null;

            var targetPath = Path.Combine(basePath, header.Name);
            
            if (header.Type == SAFEntryType.Directory)
            {
                Directory.CreateDirectory(targetPath);
                Directory.SetLastWriteTime(targetPath, header.ModTime);
                return new DirectoryInfo(targetPath);
            }

            // we're dealing with a file, so read the basestream to fill
            // the new file object
            using (var fs = new FileStream(targetPath, FileMode.Create))
            {
                CopyToStreamWithSize(baseStream, fs, header.Length);
            }

            // reset mod times.
            File.SetLastWriteTime(targetPath, header.ModTime);

            // if requested, do checksum verification
            if (calculateChecksums && header.MD5Hash != null)
            {
                var checksum = CalculateChecksum(targetPath);

                if (checksum != header.MD5Hash)
                {
                    throw new IOException($"Checksum of extracted file {targetPath} does not match checksum in header.");
                }
            }

            return new FileInfo(targetPath);
        }

        public List<FileSystemInfo> ReadAllEntriesToDisk()
        {
            var extractedItems = new List<FileSystemInfo>();
            while (true)
            {
                var newInfo = ReadEntryToDisk();
                if (newInfo == null) break;

                extractedItems.Add(newInfo);
            }

            return extractedItems;
        }

        public void WriteDirectoryToArchive(string path = null)
        {
            path ??= basePath;

            if (!Directory.Exists(path))
                throw new ArgumentException($"Path {path} is not an existing directory.");

            var baseDirectoryInfo = new DirectoryInfo(path);
            var infos = baseDirectoryInfo.EnumerateFileSystemInfos("*.*", SearchOption.AllDirectories);

            foreach (var fsInfo in infos)
            {
                WriteEntryToArchive(fsInfo);
            }
        }


        private string CalculateChecksum(string filePath)
        {
            byte[] hash;

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var md5Hsah = MD5.Create())
            {
                hash = md5Hsah.ComputeHash(fs);
            }

            // convert hash to string by concat
            return string.Join("", hash.Select(x => x.ToString("x2")));
        }

        private void CopyToStreamWithSize(Stream source, Stream target, long size, int bufferSize = 4096)
        {
            Span<byte> buffer = stackalloc byte[bufferSize];
            long sizeRemaining = size;

            while (sizeRemaining > 0)
            {
                var bytesRead = source.Read(buffer.Slice(0, sizeRemaining < bufferSize ? (int) sizeRemaining : bufferSize));
                if (bytesRead == 0) throw new IOException("Unexpeceted end of stream reached.");

                target.Write(buffer.Slice(0, bytesRead));

                sizeRemaining = sizeRemaining - bytesRead;
            }
        }
    }
}
