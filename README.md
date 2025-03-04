# ZipStorer
A Pure C# class for storing files in Zip format

[![NuGet](https://img.shields.io/nuget/v/ZipStorer.svg)](https://www.nuget.org/packages/ZipStorer/)
![downloads](https://img.shields.io/nuget/dt/ZipStorer)
![github](https://img.shields.io/github/stars/jaime-olivares/ZipStorer?style=flat&color=yellow)
![build](https://github.com/jaime-olivares/ZipStorer/actions/workflows/main.yml/badge.svg?branch=master)

ZipStorer is a minimalistic cross-platform .net class to create Zip files and store/retrieve files to/from it, by using the Deflate algorithm. No other compression methods supported.

## :warning: Breaking changes

- Since version 4.1, as part of a major reorganization of the source code, `ZipFileEntry` is no longer a nested class inside `ZipStorer`.
Therefore, you should not prefix it with `ZipStorer.` anymore.

## Advantages and usage
ZipStorer has the following advantages:

* No Interop calls, increments portability to Mono and other non-Windows platforms
* Async methods for storing and extracting files 
* Support for Zip64 (file sizes > 4GB) 
* Support for UTF8 and CP 437 Encodings
* Available as a [nuget package](https://www.nuget.org/packages/ZipStorer/)

## Using the code
The ZipStorer class is the unique one needed to create the zip file. It contains a nested structure *(ZipFileEntry)* for collecting each directory entry. The class has been declared inside the System.IO.Compression namespace. 

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
public void AddFile(ZipStorer.Compress method, string pathname, string filenameInZip, string comment);
public void AddStream(ZipStorer.Compress method, string filenameInZip, Stream source, DateTime modTime, string comment);
````
    
The first method allows adding an existing file to the storage. The first argument receives the compression method; it can be *Store* or *Deflate* enum values. The second argument admits the physical path name, the third one allows to change the path or file name to be stored in the Zip, and the last argument inserts a comment in the storage. Notice that the folder path in the *pathname* argument is not saved in the Zip file. Use the *filenameInZip* argument instead to specify the folder path and filename. It can be expressed with both slashes or backslashes.

The second method allows adding data from any kind of stream object derived from the *System.IO.Stream class*. Internally, the first method opens a *FileStream* and calls the second method.

Finally, it is required to close the storage with the *Close()* method. This will save the central directory information too. Alternatively, the *Dispose()* method can be used.

## Extracting stored files
For extracting a file, the zip directory shall be read first, by using the *ReadCentralDir()* method, and then the *ExtractFile()* method, like in the following minimal sample code:

````csharp
// Open an existing zip file for reading
ZipStorer zip = ZipStorer.Open(@"c:\data\sample.zip", FileAccess.Read);

// Read the central directory collection
List<ZipFileEntry> dir = zip.ReadCentralDir();

// Look for the desired file
foreach (ZipFileEntry entry in dir)
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
List<ZipFileEntry> removeList = new List<ZipFileEntry>();

foreach (object sel in listBox4.SelectedItems)
{
    removeList.Add((ZipFileEntry)sel);
}

ZipStorer.RemoveEntries(ref zip, removeList);
````

## File and stream usage
The current release of ZipStorer supports both files and streams for creating and opening a zip storage. Several methods are overloaded for this dual support. The advantage of file-oriented methods is simplicity, since those methods will open or create files internally. On the other hand, stream-oriented methods are more flexible by allowing to manage zip storages in streams different than files. File-oriented methods will invoke internally to equivalent stream-oriented methods. Notice that not all streams will apply, because the library requires the streams to be randomly accessed (CanSeek = true). The RemoveEntries method will work only if the zip storage is a file.

````csharp
    // File-oriented methods:
    public static ZipStorer Create(string filename, string comment);
    public static ZipStorer Open(string filename, FileAccess access);
    public ZipFileEntry AddFile(Compression method, string pathname, string filenameInZip, string comment);
    public bool ExtractFile(ZipFileEntry zfe, string filename);
    public static bool RemoveEntries(ref ZipStorer zip, List<zipfileentry> zfes);

    // Stream-oriented methods:
    public static ZipStorer Create(Stream stream, string comment, bool leaveOpen);
    public static ZipStorer Open(Stream stream, FileAccess access, bool leaveOpen);
    public ZipFileEntry AddStream(Compression method, string filenameInZip, Stream source, DateTime modTime, string comment);
    public bool ExtractFile(ZipFileEntry zfe, Stream stream);

    // Async methods
    public async Task<ZipFileEntry> AddStreamAsync(Compression method, string filenameInZip, Stream source, DateTime modTime, string comment)
    public async Task<bool> ExtractFileAsync(ZipFileEntry zfe, Stream stream);
````

The *leaveOpen* argument will prevent the stream to be closed after completing the generation of the zip package.

## Filename encoding
Traditionally, the ZIP format supported DOS encoding system (a.k.a. IBM Code Page 437) for filenames in header records, which is a serious limitation for using non-occidental and even some occidental characters. Since 2007, the ZIP format specification was improved to support Unicode's UTF-8 encoding system.

ZipStorer class detects UTF-8 encoding by reading the proper flag in each file's header information. For enforcing filenames to be encoded with UTF-8 system, set the *EncodeUTF8* member of ZipStorer class to true. All new filenames added will be encoded with UTF8. Notice this doesn't affect stored file contents at all. Also be aware that Windows Explorer's embedded Zip format facility does not recognize well the UTF-8 encoding system, as WinZip or WinRAR do.

## Zip64 compatibility
This library has been compatible with native macOS zip support for large files. 
However, it hasn't been compatible with the native Windows zip tools and it rather required the use of third party tools like WinZip or similar.
At this moment, the support for Zip64 on Windows is in beta test. If you want to test it, look for the latest beta in nuget.
