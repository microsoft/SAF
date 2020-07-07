# Overview 
The Simple Archive Format, or SAF, is a simple, lightweight, extendable file archive format designed by the Surface Automation Team. It has similar functionality to tar or cpio. 

# Why another archive format?
The only file archive format built into .NET is Zip, which cannot be read efficiently from a non-seekable stream (such as a network or crypto stream).

While SAF was designed to allow for efficient reading and writing from a non-seekable stream, it is more comparable to protobuf than tar – it’s something developers can use and build upon. Headers in an archive are application specific and can be customized per entry (all we define is the base structure; applications can freely extend the header type). 

# Obtaining the Library
SAF can be compiled easily from source, and nuget.org packages are available (`Microsoft.SurfaceAutomationTeam.SAF`). 

*Note: SAF targets .NET Standard 2.1 - it cannot be used from .NET Framework 4.*

# Using the library
If you aren't extending SAF, it can be used easily via the `SAFArchive` class. Here is a simple example of writing a directory to a SAF file:

```C#
using (var fs = new FileStream(archivePath, FileMode.Create))
{
  var archive = new SAFArchive(fs, SAFMode.Write, sourcePath, true);
  archive.WriteDirectoryToArchive();
}
```

And here is how you'd read that same archive to extract the contents:

```C#
using (var fs = new FileStream(archivePath, FileMode.Open))
{
  var archive = new SAFArchive(fs, SAFMode.Read, extractPath);
  archive.ReadAllEntriesToDisk();
}
```

You can use other functions to seek through the record for specific files, or to control extraction parameters. In the default configuration, SAF also calculates MD5 checksums for all files written to the archive, and verifies them on extraction.

# SAF Basic Architecture
SAF is designed to be minimal. There is a one-line file header at the beginning of the archive ('SAF1' followed by a newline), then each entry in the archive only has three components:

  - The header size (represented as a UTF-8 string of digits, followed by a newline)
  - The header (a json object, as a set of UTF-8 bytes)
  - The payload (the file contents, or nothing for a directory)

The header is flexible, and can contain as many or as few fields as required by the application. While we provide a basic header in the library that is used by the high level `SAFArchive` class, users who want to customize the format can use their own header types. 

## Default Record Metadata
In its default configuration, when used with the included `SAFArchive` class, the format keeps the following metadata

  - Name of entry
  - Size of entry (in bytes)
  - MD5 Hash of entry (for files only)
  - Last Write Time
  - Entry Type (file or directory)

# Contributing
This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
