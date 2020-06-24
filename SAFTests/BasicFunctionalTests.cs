using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using SAF;
using Xunit.Abstractions;
using System.Threading;
using System.Diagnostics.Tracing;
using Microsoft.VisualBasic.FileIO;

namespace SAFTests
{
    public class TemporaryFilesFixture : IDisposable
    {
        public string TempFolderPath { get; private set; }
        private FileInfo readOnlyFileInfo;

        public TemporaryFilesFixture()
        {
            TempFolderPath = Path.Combine(Path.GetTempPath(), "SAFTemporaryTestFolders");

            /* create a folder structure:
                testRoot/
                    file1a.txt
                    dir1a/
                        file2a.txt
                    dir1b/
                        dir2a/
                            file3a.txt
                            file3b.txt
                        dir2b/
            */

            var file1aPath = Path.Combine(TempFolderPath, "file1a.txt");
            var dir1aPath = Path.Combine(TempFolderPath, "dir1a");
            var dir1bPath = Path.Combine(TempFolderPath, "dir1b");

            var file2aPath = Path.Combine(dir1aPath, "file2a.txt");
            var dir2aPath = Path.Combine(dir1bPath, "dir2a");
            var dir2bPath = Path.Combine(dir1bPath, "dir2b");

            var file3aPath = Path.Combine(dir2aPath, "file3a.txt");
            var file3bPath = Path.Combine(dir2aPath, "file3b.txt");


            // Create all directories
            (new List<string> { dir1aPath, dir1bPath, dir2aPath, dir2bPath }).ForEach(x => Directory.CreateDirectory(x));

            // Write text to files
            File.WriteAllText(file1aPath, "This is file 1a, directly under the root directory.");
            File.WriteAllText(file2aPath, "This is file 2a, under directory 1a. It exists at depth 2 (under a dir at depth 1).");
            File.WriteAllText(file3aPath, "This is file 3a, under directory 2a. It exists at depth 3.");
            File.WriteAllText(file3bPath, "This is file 3b, also under directory 2a. It exists at depth 3.");

            // make file 2a read only, as a sanity check that read only files work
            readOnlyFileInfo = new FileInfo(file2aPath);
            readOnlyFileInfo.IsReadOnly = true;


        }

        public void Dispose()
        {
            if (Directory.Exists(TempFolderPath))
            {
                readOnlyFileInfo.IsReadOnly = false;
                Directory.Delete(TempFolderPath, true);
            }
        }
    }

    public class BasicFunctionalTests : IClassFixture<TemporaryFilesFixture>, IDisposable
    {
        TemporaryFilesFixture fixture;
        ITestOutputHelper outputHelper;
        string extractPath;
        string sourcePath;
        string archivePath;
        public string RelativeDir { get { return "relativeDir"; } }

        public BasicFunctionalTests(TemporaryFilesFixture fixture, ITestOutputHelper helper)
        {
            this.fixture = fixture;
            this.outputHelper = helper;
            sourcePath = fixture.TempFolderPath;
            extractPath = Path.Combine(Path.GetTempPath(), "SAFExtractTestPath");
            archivePath = Path.Combine(Path.GetTempPath(), "SAFTextArchive.saf");

            Directory.CreateDirectory(extractPath);
        }

        private void WriteArchiveAndReadBack()
        {
            // write the contents of the files to the record
            using (var fs = new FileStream(archivePath, FileMode.Create))
            {
                var archive = new SAFArchive(fs, SAFMode.Write, sourcePath, true);
                archive.WriteDirectoryToArchive();
            }

            // read them back.
            using (var fs = new FileStream(archivePath, FileMode.Open))
            {
                var archive = new SAFArchive(fs, SAFMode.Read, extractPath);
                archive.ReadAllEntriesToDisk();
            }
        }

        [Theory]
        [InlineData("file1a.txt")]
        [InlineData("dir1a/file2a.txt")]
        [InlineData("dir1b/dir2a/file3a.txt")]
        [InlineData("dir1b/dir2a/file3b.txt")]
        [InlineData("dir1b\\dir2a\\file3b.txt")]
        public void CheckFileExists(string targetFile)
        {
            WriteArchiveAndReadBack();

            Assert.True(File.Exists(Path.Combine(extractPath, targetFile)), $"File {targetFile} is missing from the extracted directory.");
        }

        [Theory]
        [InlineData("file1a.txt", "This is file 1a, directly under the root directory.")]
        [InlineData("dir1a/file2a.txt", "This is file 2a, under directory 1a. It exists at depth 2 (under a dir at depth 1).")]
        public void CheckFileContents(string targetFile, string expectedText)
        {
            WriteArchiveAndReadBack();

            var targetPath = Path.Combine(extractPath, targetFile);
            var targetText = File.ReadAllText(targetPath).Trim();

            Assert.Equal(targetText, expectedText);
        }

        [Fact]
        public void VerifyChecksumOperation()
        {
            // create the archive normally
            using (var fs = new FileStream(archivePath, FileMode.Create))
            {
                var archive = new SAFArchive(fs, SAFMode.Write, sourcePath, true);
                archive.WriteDirectoryToArchive();
            }

            // tamper with the archive. 
            using (var fs = new FileStream(archivePath, FileMode.Open))
            {
                var archiveToTamperWith = new SAFArchive(fs, SAFMode.Read, extractPath);

                // find a file entry
                while (true)
                {
                    var header = archiveToTamperWith.ReadNextHeader();
                    if (header.Type == SAFEntryType.File) break; // we don't need to skip - directorys have no length.
                }

                // we're now on a file. Invert the second byte. 
                var loc = fs.Position;
                byte byteToInvert = (byte)fs.ReadByte();
                byte invertedValue = (byte)(0xFF ^ byteToInvert);

                // reset our position
                fs.Position = loc;
                fs.WriteByte(invertedValue);

                // flush
                fs.Flush();
            }

            // try to extract, this should throw an IO Error.
            using (var fs = new FileStream(archivePath, FileMode.Open))
            {
                var corruptedArchive = new SAFArchive(fs, SAFMode.Read, extractPath);
                Assert.Throws<IOException>(() => corruptedArchive.ReadAllEntriesToDisk());
            }
        }

        [Theory]
        [InlineData("file1a.txt")]
        [InlineData("dir1b/dir2a")]
        [InlineData("dir1b/dir2b")]
        public void CheckModTimes(string fileName)
        {
            WriteArchiveAndReadBack();

            var source = Path.Combine(sourcePath, fileName);
            var extracted = Path.Combine(extractPath, fileName);

            FileSystemInfo sourceInfo = Directory.Exists(source) ? (FileSystemInfo) new DirectoryInfo(source) : new FileInfo(source);
            FileSystemInfo extractedInfo = Directory.Exists(extracted) ? (FileSystemInfo) new DirectoryInfo(extracted) : new FileInfo(extracted);

            // directories will have their modtime set, but if it contains files while extracting, it will be
            // updated when writing child items. So we only test directories that have no contents
            // for mod time checking.

            // Note - directory times can theoretically match, even if directories contain items, if there was
            // so little data we managed to write faster than windows mod time granularity. It's rare, but it
            // can happen, so we only test the empty directory case here.

            if (Directory.Exists(source) && Directory.EnumerateFileSystemEntries(source).Count() > 0)
            {
                outputHelper.WriteLine($"Skipped directory modtime check for {fileName} - directory has contents.");
                return;
            }

            // files should always get the correct mod time, as should directories that are empty
            Assert.Equal(sourceInfo.LastWriteTime, extractedInfo.LastWriteTime);
        }

        [Fact]
        public void CheckFileHeader()
        {
            // write the contents of the files to the record
            using (var fs = new FileStream(archivePath, FileMode.Create))
            {
                var archive = new SAFArchive(fs, SAFMode.Write, sourcePath, true);
                archive.WriteDirectoryToArchive();

                // confirm the archive header was set correctly
                Assert.Equal(archive.SAFArchiveHeader, SAFArchive.SAF_ARCHIVE_HEADER_STRING);
            }

            // read them back.
            using (var fs = new FileStream(archivePath, FileMode.Open))
            {
                var archive = new SAFArchive(fs, SAFMode.Read, extractPath);
                archive.ReadAllEntriesToDisk();

                // confirm the header was read correctly
                Assert.Equal(archive.SAFArchiveHeader, SAFArchive.SAF_ARCHIVE_HEADER_STRING);
            }
        }

        [Theory]
        [InlineData("bar", false, '\\')]
        [InlineData("baz", true, '\\')]
        [InlineData("foo", false, '/')]
        [InlineData("cat", true, '/')]
        [InlineData("cats/", true, '/')]
        [InlineData("dogs\\", true, '\\')]

        public void TestRelativePaths(string relativePath, bool useLeadingDotSlash, char dirSeparator)
        {
            // we build all paths under the designated relative path, for easy cleanup.
            var targetPath = RelativeDir + dirSeparator + relativePath;

            // confirm the path isn't rooted
            if (Path.IsPathRooted(targetPath)) throw new ArgumentException("Path under test should be relative.");

            // if we use the leading dotslash, prepend .\ (or ./) to path (as might be fed in if paths were generated via hiting tab
            // on the command line
            if (useLeadingDotSlash) targetPath = "." + dirSeparator + targetPath;

            // build the directory structure (we can use full paths here)
            Directory.CreateDirectory(targetPath);

            // create the file target
            File.WriteAllText(targetPath + dirSeparator + "testFile.bin", "I EXIST");

            // do saf operations
            using (var fs = new FileStream(archivePath, FileMode.Create))
            {
                var archive = new SAFArchive(fs, SAFMode.Write, targetPath);
                archive.WriteDirectoryToArchive();
            }

            using (var fs = new FileStream(archivePath, FileMode.Open))
            {
                var archive = new SAFArchive(fs, SAFMode.Read, extractPath);
                archive.ReadAllEntriesToDisk();
            }

            // test that our file exists in the expected path (we fed in the target path to the archive root, so it won't show here)
            var extractedPath = Path.Combine(extractPath, "testFile.bin");

            Assert.True(File.Exists(extractedPath), "Extract path test failed.");


        }

        public void Dispose()
        {
            if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
            if (File.Exists(archivePath)) File.Delete(archivePath);

            if (Directory.Exists(RelativeDir)) Directory.Delete(RelativeDir, true);
        }
    }
}
