# ZipStorer
A Pure C# class for storing files in Zip format

[![Financial Contributors on Open Collective](https://opencollective.com/jaime-olivares-opensource/all/badge.svg?label=financial+contributors)](https://opencollective.com/jaime-olivares-opensource) [![NuGet](https://img.shields.io/nuget/v/ZipStorer.svg)](https://www.nuget.org/packages/ZipStorer/)
[![github](https://img.shields.io/github/stars/jaime-olivares/zipstorer.svg)]()
[![Build Status](https://dev.azure.com/jaime-olivares-f/zipstorer/_apis/build/status/jaime-olivares.zipstorer?branchName=master)](https://dev.azure.com/jaime-olivares-f/zipstorer/_build/latest?definitionId=1&branchName=master)

ZipStorer is a minimalistic cross-platform .net class to create Zip files and store/retrieve files to/from it, by using the Deflate algorithm. No other compression methods supported.

## Advantages and usage
ZipStorer has the following advantages:

* It is a short and monolithic C# class that can be embedded as source code in any project (1 source file)
* No Interop calls, increments portability to Mono and other non-Windows platforms
* Can also be implemented with Mono, .NET Compact Framework and .Net Standard 2.0+
* Async methods for storing and extracting files (only for .Net Framework 4.5+ and .Net Standard 2.0+)
* `NEW:` Support for Zip64 (file sizes > 4GB) 
* UTF8 Encoding support and ePUB compatibility
* Available as a [nuget package](https://www.nuget.org/packages/ZipStorer/)

## Using the code
The ZipStorer class is the unique one needed to create the zip file. It contains a nested structure *(ZipFileEntry)* for collecting each directory entry. The class has been declared inside the System.IO namespace. 

There is no default constructor. There are two ways to construct a new ZipStorer instance, depending on specific needs: use either the *Create()* or the *Open()* static method. To create a new Zip file, use the *Create()* method like this:

````csharp
ZipStorer zip = ZipStorer.Create(filename, comment);  // file-oriented version
ZipStorer zip = ZipStorer.Create(stream, comment);  // stream-oriented version
````

It is required to specify the full path for the new zip file, or pass a valid stream, and optionally add a comment. For opening an existing zip file for appending, the *Open()* method is required, like the following:

````csharp
ZipStorer zip = ZipStorer.Open(filename, fileaccess);  // file-oriented version
ZipStorer zip = ZipStorer.Open(stream, fileaccess);  // stream-oriented version
````

Where *fileaccess* should be of type *System.IO.FileAccess* enumeration type. Also, as now ZipStorer is derived from *IDisposable* interface, the using keyword can be used to ensure proper disposing of the storage resource:

````csharp
using (ZipStorer zip = ZipStorer.Create(filename, comment))
{
    // some operations with zip object
    //
}   // automatic close operation here
````

For adding files into an opened zip storage, there are two available methods:

````csharp
public void AddFile(ZipStorer.Compress _method, string _pathname, string _filenameInZip, string _comment);
public void AddStream(ZipStorer.Compress _method, string _filenameInZip, Stream _source, DateTime _modTime, string _comment);
````
    
The first method allows adding an existing file to the storage. The first argument receives the compression method; it can be *Store* or *Deflate* enum values. The second argument admits the physical path name, the third one allows to change the path or file name to be stored in the Zip, and the last argument inserts a comment in the storage. Notice that the folder path in the *_pathname* argument is not saved in the Zip file. Use the *_filenameInZip* argument instead to specify the folder path and filename. It can be expressed with both slashes or backslashes.

The second method allows adding data from any kind of stream object derived from the *System.IO.Stream class*. Internally, the first method opens a *FileStream* and calls the second method.

Finally, it is required to close the storage with the *Close()* method. This will save the central directory information too. Alternatively, the *Dispose()* method can be used.

## Sample application
The provided sample application is a Winforms project. It will ask for files and store the path names in a *ListBox*, along with the operation type: creating or appending, and compression method. 

## Extracting stored files
For extracting a file, the zip directory shall be read first, by using the *ReadCentralDir()* method, and then the *ExtractFile()* method, like in the following minimal sample code:

````csharp
// Open an existing zip file for reading
ZipStorer zip = ZipStorer.Open(@"c:\data\sample.zip", FileAccess.Read);

// Read the central directory collection
List<ZipStorer.ZipFileEntry> dir = zip.ReadCentralDir();

// Look for the desired file
foreach (ZipStorer.ZipFileEntry entry in dir)
{
    if (Path.GetFileName(entry.FilenameInZip) == "sample.jpg")
    {
        // File found, extract it
        zip.ExtractFile(entry, @"c:\data\sample.jpg");
        break;
    }
}
zip.Close();
````

## Removal of entries
Removal of entries in a zip file is a resource-consuming task. The simplest way is to copy all non-removed files into a new zip storage. The *RemoveEntries()* static method will do this exactly and will construct the ZipStorer object again. For the sake of efficiency, *RemoveEntries()* will accept many entry references in a single call, as in the following example:

````csharp
List<ZipStorer.ZipFileEntry> removeList = new List<ZipStorer.ZipFileEntry>();

foreach (object sel in listBox4.SelectedItems)
{
    removeList.Add((ZipStorer.ZipFileEntry)sel);
}

ZipStorer.RemoveEntries(ref zip, removeList);
````

## File and stream usage
The current release of ZipStorer supports both files and streams for creating and opening a zip storage. Several methods are overloaded for this dual support. The advantage of file-oriented methods is simplicity, since those methods will open or create files internally. On the other hand, stream-oriented methods are more flexible by allowing to manage zip storages in streams different than files. File-oriented methods will invoke internally to equivalent stream-oriented methods. Notice that not all streams will apply, because the library requires the streams to be randomly accessed (CanSeek = true). The RemoveEntries method will work only if the zip storage is a file.

````csharp
    // File-oriented methods:
    public static ZipStorer Create(string _filename, string _comment);
    public static ZipStorer Open(string _filename, FileAccess _access);
    public ZipFileEntry AddFile(Compression _method, string _pathname, string _filenameInZip, string _comment);
    public bool ExtractFile(ZipFileEntry _zfe, string _filename);
    public static bool RemoveEntries(ref ZipStorer _zip, List<zipfileentry> _zfes);  // No stream-oriented equivalent

    // Stream-oriented methods:
    public static ZipStorer Create(Stream _stream, string _comment, bool _leaveOpen);
    public static ZipStorer Open(Stream _stream, FileAccess _access, bool _leaveOpen);
    public ZipFileEntry AddStream(Compression _method, string _filenameInZip, Stream _source, DateTime _modTime, string _comment);
    public bool ExtractFile(ZipFileEntry _zfe, Stream _stream);

    // Async methods (not available for .Net Framework 2.0):
    public ZipFileEntry AddStreamAsync(Compression _method, string _filenameInZip, Stream _source, DateTime _modTime, string _comment)
    public async Task<bool> ExtractFileAsync(ZipFileEntry _zfe, Stream _stream);
````

The *_leaveOpen* argument will prevent the stream to be closed after completing the generation of the zip package.

## Filename encoding
Traditionally, the ZIP format supported DOS encoding system (a.k.a. IBM Code Page 437) for filenames in header records, which is a serious limitation for using non-occidental and even some occidental characters. Since 2007, the ZIP format specification was improved to support Unicode's UTF-8 encoding system.

ZipStorer class detects UTF-8 encoding by reading the proper flag in each file's header information. For enforcing filenames to be encoded with UTF-8 system, set the *EncodeUTF8* member of ZipStorer class to true. All new filenames added will be encoded with UTF8. Notice this doesn't affect stored file contents at all. Also be aware that Windows Explorer's embedded Zip format facility does not recognize well the UTF-8 encoding system, as WinZip or WinRAR do.

## .Net Standard support
Now ZipStorer supports .Net Standard 2.0+ and hence a broad range of platforms. 

If developing with Visual Studio Code, the `csproj` file must reference the [nuget package](https://www.nuget.org/packages/ZipStorer/):
````xml
  <ItemGroup>
    <PackageReference Include="ZipStorer" Version="*" />
  </ItemGroup>
````

## Contributors

### Code Contributors

This project exists thanks to all the people who contribute. [[Contribute](CONTRIBUTING.md)].
<a href="https://github.com/jaime-olivares/zipstorer/graphs/contributors"><img src="https://opencollective.com/jaime-olivares-opensource/contributors.svg?width=890&button=false" /></a>

### Financial Contributors

Become a financial contributor and help us sustain our community. [[Contribute](https://opencollective.com/jaime-olivares-opensource/contribute)]

#### Individuals

<a href="https://opencollective.com/jaime-olivares-opensource"><img src="https://opencollective.com/jaime-olivares-opensource/individuals.svg?width=890"></a>

#### Organizations

Support this project with your organization. Your logo will show up here with a link to your website. [[Contribute](https://opencollective.com/jaime-olivares-opensource/contribute)]

<a href="https://opencollective.com/jaime-olivares-opensource/organization/0/website"><img src="https://opencollective.com/jaime-olivares-opensource/organization/0/avatar.svg"></a>
<a href="https://opencollective.com/jaime-olivares-opensource/organization/1/website"><img src="https://opencollective.com/jaime-olivares-opensource/organization/1/avatar.svg"></a>
<a href="https://opencollective.com/jaime-olivares-opensource/organization/2/website"><img src="https://opencollective.com/jaime-olivares-opensource/organization/2/avatar.svg"></a>
<a href="https://opencollective.com/jaime-olivares-opensource/organization/3/website"><img src="https://opencollective.com/jaime-olivares-opensource/organization/3/avatar.svg"></a>
<a href="https://opencollective.com/jaime-olivares-opensource/organization/4/website"><img src="https://opencollective.com/jaime-olivares-opensource/organization/4/avatar.svg"></a>
<a href="https://opencollective.com/jaime-olivares-opensource/organization/5/website"><img src="https://opencollective.com/jaime-olivares-opensource/organization/5/avatar.svg"></a>
<a href="https://opencollective.com/jaime-olivares-opensource/organization/6/website"><img src="https://opencollective.com/jaime-olivares-opensource/organization/6/avatar.svg"></a>
<a href="https://opencollective.com/jaime-olivares-opensource/organization/7/website"><img src="https://opencollective.com/jaime-olivares-opensource/organization/7/avatar.svg"></a>
<a href="https://opencollective.com/jaime-olivares-opensource/organization/8/website"><img src="https://opencollective.com/jaime-olivares-opensource/organization/8/avatar.svg"></a>
<a href="https://opencollective.com/jaime-olivares-opensource/organization/9/website"><img src="https://opencollective.com/jaime-olivares-opensource/organization/9/avatar.svg"></a>
