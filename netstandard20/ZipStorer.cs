// ZipStorer, by Jaime Olivares
// Website: http://github.com/jaime-olivares/zipstorer

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;



namespace ZipStorer
{
    /// <summary>
    /// Unique class for compression/decompression file. Represents a Zip file.
    /// </summary>
    public class ZipStorer : IDisposable
    {
        // 64Kb, the size of the comment field in a ZIP file.
        private const int SixtyFourK = 65535;

        #region Public Properties
        /// <summary>True if UTF8 encoding for filename and comments, false if default (CP 437)</summary>
        public bool EncodeUtf8 { get; set; }

        /// <summary>Force deflate algorithm even if it inflates the stored file. Off by default.</summary>
        public bool ForceDeflating { get; set; }

        /// <summary>Default to 512Kb buffer size.</summary>
        public static int BufferSize { get; set; } = 512 * 1024;
        #endregion

        #region Private fields
        // List of files to store
        private readonly List<ZipFileEntry> _files = new List<ZipFileEntry>();

        // filename of storage file
        private string _fileName;

        // Stream object of storage file
        private Stream _zipFileStream;

        // General comment
        private string _comment = string.Empty;

        // Central dir image
        private byte[] _centralDirImage;

        // Existing files in zip
        private long _existingFiles;

        // File access for Open method
        private FileAccess _access;

        // leave the stream open after the ZipStorer object is disposed
        private bool _leaveOpen;

        // Dispose control
        private bool _isDisposed;

        // Static CRC32 Table
        private readonly uint[] _crcTable;

        // Default filename encoder
        private readonly Encoding _defaultEncoding;
#endregion

#region Public methods
        /// <summary>
        /// Set up dependencies. Use factory methods.
        /// </summary>
        private ZipStorer()
        {
            _defaultEncoding = CodePagesEncodingProvider.Instance.GetEncoding(437);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Generate CRC32 table
            _crcTable = new uint[256];
            for (int i = 0; i < _crcTable.Length; i++)
            {
                uint c = (uint)i;
                for (int j = 0; j < 8; j++)
                {
                    if ((c & 1) != 0)
                        c = 3988292384 ^ (c >> 1);
                    else
                        c >>= 1;
                }

                _crcTable[i] = c;
            }
        }

        /// <summary>
        /// Method to create a new storage file
        /// </summary>
        /// <param name="filePath">Full path of Zip file to create</param>
        /// <param name="comment">General comment for Zip file</param>
        /// <returns>A valid ZipStorer object</returns>
        public static ZipStorer Create(string filePath, string comment = null)
        {
            var fullPath = Path.GetFullPath(filePath);
            Stream stream = new FileStream(fullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, BufferSize, FileOptions.Asynchronous);

            ZipStorer zip = Create(stream, comment);
            zip._fileName = fullPath;

            return zip;
        }

        /// <summary>
        /// Method to create a new zip storage in a stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="comment"></param>
        /// <param name="leaveOpen">true to leave the stream open after the ZipStorer object is disposed; otherwise, false (default).</param>
        /// <returns>A valid ZipStorer object</returns>
        public static ZipStorer Create(Stream stream, string comment = null, bool leaveOpen = false)
        {
            ZipStorer zip = new ZipStorer()
            {
                _comment = comment ?? string.Empty,
                _zipFileStream = stream,
                _access = FileAccess.Write,
                _leaveOpen = leaveOpen
            };

            return zip;
        }

        /// <summary>
        /// Method to open an existing storage file
        /// </summary>
        /// <param name="filename">Full path of Zip file to open</param>
        /// <param name="access">File access mode as used in FileStream constructor</param>
        /// <returns>A valid ZipStorer object</returns>
        public static ZipStorer Open(string filename, FileAccess access)
        {
            Stream stream = null;
            ZipStorer zip = null;
            try
            {
                stream = new FileStream(filename, FileMode.Open, access == FileAccess.Read ? FileAccess.Read : FileAccess.ReadWrite, FileShare.None, BufferSize, FileOptions.Asynchronous);

                zip = Open(stream, access);
                zip._fileName = filename;
            }
            catch //(Exception e)
            {
                stream?.Dispose();
                zip?.Dispose();

                throw;
            }

            return zip;
        }

        /// <summary>
        /// Method to open an existing storage from stream
        /// </summary>
        /// <param name="stream">Already opened stream with zip contents</param>
        /// <param name="access">File access mode for stream operations</param>
        /// <param name="leaveOpen">true to leave the stream open after the ZipStorer object is disposed; otherwise, false (default).</param>
        /// <returns>A valid ZipStorer object</returns>
        public static ZipStorer Open(Stream stream, FileAccess access, bool leaveOpen = false)
        {
            if (!stream.CanSeek && access != FileAccess.Read)
                throw new InvalidOperationException("Stream cannot seek");

            var zip = new ZipStorer()
            {
                _zipFileStream = stream,
                _access = access,
                _leaveOpen = leaveOpen
            };

            if (zip.ReadFileInfo())
                return zip;
            
            if (!leaveOpen)
                zip.Close();
            
            throw new InvalidDataException("Corrupt ZIP: End of central directory record signature not found.");
        }

        /// <summary>
        /// Add full contents of a file into the Zip storage
        /// </summary>
        /// <param name="method">Compression method</param>
        /// <param name="pathname">Full path of file to add to Zip storage</param>
        /// <param name="filenameInZip">filename and path as desired in Zip directory</param>
        /// <param name="comment">Comment for stored file</param>        
        public ZipFileEntry AddFile(CompressionType method, string pathname, string filenameInZip, string comment = null)
        {
            if (_access == FileAccess.Read)
                throw new InvalidOperationException("Writing is not allowed");

            var fullPath = Path.GetFullPath(pathname);
            if (fullPath == _fileName) return null;

            using (var stream = new FileStream(pathname, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize, FileOptions.Asynchronous))
            {
                return Task.Run(() =>  AddStreamAsync(method, filenameInZip, stream, File.GetLastWriteTime(pathname), comment)).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Add full contents of a file into the Zip storage
        /// </summary>
        /// <param name="method">Compression method</param>
        /// <param name="pathname">Full path of file to add to Zip storage</param>
        /// <param name="filenameInZip">filename and path as desired in Zip directory</param>
        /// <param name="comment">Comment for stored file</param>        
        public async Task<ZipFileEntry> AddFileAsync(CompressionType method, string pathname, string filenameInZip, string comment = null)
        {
            if (_access == FileAccess.Read)
                throw new InvalidOperationException("Writing is not allowed");

            var fullPath = Path.GetFullPath(pathname);
            if (fullPath == _fileName) return null;

            using (var stream = new FileStream(pathname, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous))
            {
                return await AddStreamAsync(method, filenameInZip, stream, File.GetLastWriteTime(pathname), comment);
            }
        }

        /// <summary>
        /// Add full contents of a stream into the Zip storage
        /// </summary>
        /// <param name="method">Compression method</param>
        /// <param name="filenameInZip">filename and path as desired in Zip directory</param>
        /// <param name="source">Stream object containing the data to store in Zip</param>
        /// <param name="modTime">Modification time of the data to store</param>
        /// <param name="comment">Comment for stored file</param>
        public async Task<ZipFileEntry>AddStreamAsync(CompressionType method, string filenameInZip, Stream source, DateTime modTime, string comment = null)
        {
            if (_access == FileAccess.Read)
                throw new InvalidOperationException("Writing is not allowed");

            // Prepare the file info
            var zfe = new ZipFileEntry()
            {
                Method = method,
                EncodeUTF8 = EncodeUtf8,
                FilenameInZip = NormalizedFilename(filenameInZip),
                Comment = comment ?? string.Empty,
                Crc32 = 0,  // to be updated later
                HeaderOffset = (uint)_zipFileStream.Position,  // offset within file of the start of this local record
                CreationTime = modTime,
                ModifyTime = modTime,
                AccessTime = modTime
            };

            // Write local header
            WriteLocalHeader(zfe);
            zfe.FileOffset = (uint)_zipFileStream.Position;

            // Write file to zip (store)
            await StoreAsync(zfe, source);
            await source.FlushAsync();
            source.Close();

            UpdateCrcAndSizes(zfe);

            _files.Add(zfe);

            return zfe;
        }

        /// <summary>
        /// Add full contents of a directory into the Zip storage
        /// </summary>
        /// <param name="method">Compression method</param>
        /// <param name="pathname">Full path of directory to add to Zip storage</param>
        /// <param name="comment">Comment for stored directory</param>
        public void AddDirectory(CompressionType method, string pathname, string comment = null)
        {
            if (_access == FileAccess.Read)
                throw new InvalidOperationException("Writing is not allowed");

            // Process the list of files found in the directory.
            var fileEntries = Directory.EnumerateFiles(pathname, "*", SearchOption.AllDirectories);

            foreach (string fileName in fileEntries)
            {
                // Don't zip yourself.
                var fullPath = Path.GetFullPath(fileName);
                if (fullPath == _fileName) continue;

                AddFile(method, fullPath, fileName, comment);
            }
        }

        /// <summary>
        /// Add full contents of a directory into the Zip storage
        /// </summary>
        /// <param name="method">Compression method</param>
        /// <param name="pathname">Full path of directory to add to Zip storage</param>
        /// <param name="comment">Comment for stored directory</param>
        public async Task AddDirectoryAsync(CompressionType method, string pathname, string comment = null)
        {
            if (_access == FileAccess.Read)
                throw new InvalidOperationException("Writing is not allowed");

            // Process the list of files found in the directory.
            var fileEntries = Directory.EnumerateFiles(pathname, "*", SearchOption.AllDirectories);

            foreach (string fileName in fileEntries)
            {
                // Don't zip yourself.
                var fullPath = Path.GetFullPath(fileName);
                if (fullPath == _fileName) continue;

                await AddFileAsync(method, fullPath, fileName, comment);
            }
        }

        /// <summary>
        /// Updates central directory (if pertinent) and close the Zip storage
        /// </summary>
        /// <remarks>This is a required step, unless automatic dispose is used</remarks>
        public void Close()
        {
            if (_access != FileAccess.Read && _files.Count > 0)
            {
                long centralOffset = _zipFileStream.Position;
                long centralSize = 0;

                if (_centralDirImage != null)
                    _zipFileStream.Write(_centralDirImage, 0, _centralDirImage.Length);

                foreach (var fileEntry in _files)
                {
                    long pos = _zipFileStream.Position;
                    WriteCentralDirRecord(fileEntry);
                    centralSize += _zipFileStream.Position - pos;
                }

                if (_centralDirImage != null)
                    WriteEndRecord(centralSize + (uint)_centralDirImage.Length, centralOffset);
                else
                    WriteEndRecord(centralSize, centralOffset);
            }

            if (_zipFileStream != null && !_leaveOpen)
            {
                _zipFileStream.Flush();
                _zipFileStream.Dispose();
                _zipFileStream = null;
            }
        }

        /// <summary>
        /// Read all the file records in the central directory 
        /// </summary>
        /// <returns>List of all entries in directory</returns>
        public List<ZipFileEntry> ReadCentralDir()
        {
            if (_centralDirImage == null)
                if (ReadFileInfo() == false || _centralDirImage == null)
                    throw new InvalidOperationException("Central directory currently does not exist");

            var result = new List<ZipFileEntry>();

            for (int pointer = 0; pointer < _centralDirImage.Length; )
            {
                uint signature = BitConverter.ToUInt32(_centralDirImage, pointer);
                if (signature != 0x02014b50)
                    break;

                bool encodeUtf8 = (BitConverter.ToUInt16(_centralDirImage, pointer + 8) & 0x0800) != 0;
                ushort method = BitConverter.ToUInt16(_centralDirImage, pointer + 10);
                uint modifyTime = BitConverter.ToUInt32(_centralDirImage, pointer + 12);
                uint crc32 = BitConverter.ToUInt32(_centralDirImage, pointer + 16);
                long compressedSize = BitConverter.ToUInt32(_centralDirImage, pointer + 20);
                long fileSize = BitConverter.ToUInt32(_centralDirImage, pointer + 24);
                ushort filenameSize = BitConverter.ToUInt16(_centralDirImage, pointer + 28);
                ushort extraSize = BitConverter.ToUInt16(_centralDirImage, pointer + 30);
                ushort commentSize = BitConverter.ToUInt16(_centralDirImage, pointer + 32);
                uint headerOffset = BitConverter.ToUInt32(_centralDirImage, pointer + 42);
                uint headerSize = (uint)( 46 + filenameSize + extraSize + commentSize);
                DateTime modifyTimeDt = DosTimeToDateTime(modifyTime) ?? DateTime.Now;

                var encoder = encodeUtf8 ? Encoding.UTF8 : _defaultEncoding;

                var zfe = new ZipFileEntry()
                {
                    Method = (CompressionType)method,
                    FilenameInZip = encoder.GetString(_centralDirImage, pointer + 46, filenameSize),
                    FileOffset = GetFileOffset(headerOffset),
                    FileSize = fileSize,
                    CompressedSize = compressedSize,
                    HeaderOffset = headerOffset,
                    HeaderSize = headerSize,
                    Crc32 = crc32,
                    ModifyTime = modifyTimeDt,
                    CreationTime = modifyTimeDt,
                    AccessTime = DateTime.Now,
                };

                if (commentSize > 0)
                    zfe.Comment = encoder.GetString(_centralDirImage, pointer + 46 + filenameSize + extraSize, commentSize);

                if (extraSize > 0)
                {
                    ReadExtraInfo(_centralDirImage, pointer + 46 + filenameSize, zfe);
                }

                result.Add(zfe);
                pointer += (46 + filenameSize + extraSize + commentSize);
            }

            return result;
        }

        /// <summary>
        /// Copy the contents of a stored file into a physical file
        /// </summary>
        /// <param name="zfe">Entry information of file to extract</param>
        /// <param name="filename">Name of file to store uncompressed data</param>
        /// <returns>True if success, false if not.</returns>
        /// <remarks>Unique compression methods are Store and Deflate</remarks>
        public bool ExtractFile(ZipFileEntry zfe, string filename)
        {
            // Make sure the parent directory exist
            string path = Path.GetDirectoryName(filename);

            if (path != null && !Directory.Exists(path))
                Directory.CreateDirectory(path);

            // Check if it is a directory. If so, do nothing.
            if (Directory.Exists(filename))
                return true;

            bool result;
            using(var output = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.Read, BufferSize, FileOptions.Asynchronous))
            {
                result = ExtractFile(zfe, output);
            }
                
            if (result)
            {
                File.SetCreationTime(filename, zfe.CreationTime);
                File.SetLastWriteTime(filename, zfe.ModifyTime);
                File.SetLastAccessTime(filename, zfe.AccessTime);
            }
            
            return result;
        }

        /// <summary>
        /// Copy the contents of a stored file into an opened stream
        /// </summary>
        /// <remarks>Same parameters and return value as ExtractFileAsync</remarks>
        public bool ExtractFile(ZipFileEntry zfe, Stream stream)
        {
            return Task.Run(() => ExtractFileAsync(zfe, stream)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Copy the contents of a stored file into an opened stream
        /// </summary>
        /// <param name="zfe">Entry information of file to extract</param>
        /// <param name="stream">Stream to store the uncompressed data</param>
        /// <returns>True if success, false if not.</returns>
        /// <remarks>Unique compression methods are Store and Deflate</remarks>
        public async Task<bool> ExtractFileAsync(ZipFileEntry zfe, Stream stream)
        {
            if (!stream.CanWrite)
                throw new InvalidOperationException("Stream cannot be written");

            // check signature
            byte[] signature = new byte[4];
            _zipFileStream.Seek(zfe.HeaderOffset, SeekOrigin.Begin);

            _ = await _zipFileStream.ReadAsync(signature, 0, 4);

            if (BitConverter.ToUInt32(signature, 0) != 0x04034b50)
                return false;

            // Select input stream for inflating or just reading
            Stream inStream;

            switch (zfe.Method)
            {
                case CompressionType.Store:
                    inStream = _zipFileStream;
                    break;
                case CompressionType.Deflate:
                    inStream = new DeflateStream(_zipFileStream, CompressionMode.Decompress, true);
                    break;
                default:
                    return false;
            }

            // Buffered copy
            byte[] buffer = new byte[SixtyFourK];
            _zipFileStream.Seek(zfe.FileOffset, SeekOrigin.Begin);
            long bytesPending = zfe.FileSize;

            while (bytesPending > 0)
            {
                int bytesRead = await inStream.ReadAsync(buffer, 0, (int)Math.Min(bytesPending, buffer.Length));
                await stream.WriteAsync(buffer, 0, bytesRead);

                bytesPending -= (uint)bytesRead;
            }

            await stream.FlushAsync();

            if (zfe.Method == CompressionType.Deflate)
                inStream.Dispose();

            return true;
        }

        /// <summary>
        /// Copy the contents of a stored file into a byte array
        /// </summary>
        /// <param name="zfe">Entry information of file to extract</param>
        /// <returns>Byte array with uncompressed data or null when failed.</returns>
        /// <remarks>Unique compression methods are Store and Deflate</remarks>
        public byte[] ExtractFile(ZipFileEntry zfe)
        {
            using (var ms = new MemoryStream())
            {
                return ExtractFile(zfe, ms) ? ms.ToArray() : null;
            }
        } 
        
        /// <summary>
        /// Extracts full contents to a folder in the destination named after the zip file.
        /// </summary>
        /// <param name="archivePath">Full path to the zip file you want to extract.</param>
        /// <param name="extractDestination">The destination where the output folder will be created. Defaults to CWD.</param>
        /// <param name="cancel">Cancels the operation.</param>
        /// <returns>The directory that was created.</returns>
        /// <exception cref="IOException">Happens when a file attempts to unzip itself outside the extraction destination.</exception>
        public static async Task<DirectoryInfo> ExtractToFolder(string archivePath, string extractDestination = "", CancellationToken cancel = default)
        {
            if (string.IsNullOrEmpty(extractDestination)) extractDestination = Directory.GetParent(archivePath)?.FullName ?? ".\\";

            DirectoryInfo di;
            using (var zip = Open(archivePath, FileAccess.Read))
            {
                // This will throw if the zip file is invalid
                var dir = zip.ReadCentralDir();

                string destinationDirectoryPath = Path.Combine(extractDestination, Path.GetFileNameWithoutExtension(archivePath));
                di = Directory.CreateDirectory(destinationDirectoryPath);
                foreach (var entry in dir)
                {
                    var completeFileName = Path.GetFullPath(Path.Combine(di.FullName, entry.FilenameInZip));
                    if (!completeFileName.StartsWith(di.FullName, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new IOException(
                            "Trying to extract file outside of destination directory. See this link for more info: https://snyk.io/research/zip-slip-vulnerability");
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(completeFileName) ?? string.Empty);
                    using (Stream destination = new FileStream(completeFileName, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true))
                    {
                        await zip.ExtractFileAsync(entry, destination);
                        await destination.FlushAsync(cancel);
                    }
                }

                zip.Close();
            }

            return di;
        }

        /// <summary>
        /// Removes one of many files in storage. It creates a new Zip file.
        /// </summary>
        /// <param name="zip">Reference to the current Zip object</param>
        /// <param name="zfes">List of Entries to remove from storage</param>
        /// <returns>True if success, false if not</returns>
        /// <remarks>This method only works for storage of type FileStream</remarks>
        public static bool RemoveEntries(ref ZipStorer zip, List<ZipFileEntry> zfes)
        {
            if (!(zip._zipFileStream is FileStream))
                throw new InvalidOperationException("RemoveEntries is allowed just over streams of type FileStream");

            //Get full list of entries
            var fullList = zip.ReadCentralDir();

            //In order to delete we need to create a copy of the zip file excluding the selected items
            var tempZipName = Path.GetTempFileName();
            var tempEntryName = Path.GetTempFileName();

            try
            {
                var tempZip = Create(tempZipName, string.Empty);

                foreach (ZipFileEntry zfe in fullList)
                {
                    if (!zfes.Contains(zfe))
                    {
                        if (zip.ExtractFile(zfe, tempEntryName))
                        {
                            tempZip.AddFile(zfe.Method, tempEntryName, zfe.FilenameInZip, zfe.Comment);
                        }
                    }
                }

                zip.Close();
                tempZip.Close();

                // UNDONE: _fileName might be null. Remove\Add would be safer and faster than replacing the whole file.
                File.Delete(zip._fileName);
                File.Move(tempZipName, zip._fileName);

                zip = Open(zip._fileName, zip._access);
            }
            catch
            {
                return false;
            }
            finally
            {
                if (File.Exists(tempZipName))
                    File.Delete(tempZipName);
                if (File.Exists(tempEntryName))
                    File.Delete(tempEntryName);
            }

            return true;
        }
#endregion

#region Private methods
        // Calculate the file offset by reading the corresponding local header
        private uint GetFileOffset(uint headerOffset)
        {
            byte[] buffer = new byte[2];

            _zipFileStream.Seek(headerOffset + 26L, SeekOrigin.Begin);
            _ = _zipFileStream.Read(buffer, 0, buffer.Length);
            ushort filenameSize = BitConverter.ToUInt16(buffer, 0);

            _ = _zipFileStream.Read(buffer, 0, buffer.Length);
            ushort extraSize = BitConverter.ToUInt16(buffer, 0);

            return 30u + filenameSize + extraSize + headerOffset;
        }

        /* Local file header:
            local file header signature     4 bytes  (0x04034b50)
            version needed to extract       2 bytes
            general purpose bit flag        2 bytes
            compression method              2 bytes
            last mod file time              2 bytes
            last mod file date              2 bytes
            crc-32                          4 bytes
            compressed size                 4 bytes
            uncompressed size               4 bytes
            filename length                 2 bytes
            extra field length              2 bytes

            filename (variable size)
            extra field (variable size)
        */
        private void WriteLocalHeader(ZipFileEntry zfe)
        {
            long pos = _zipFileStream.Position;
            Encoding encoder = zfe.EncodeUTF8 ? Encoding.UTF8 : _defaultEncoding;
            byte[] encodedFilename = encoder.GetBytes(zfe.FilenameInZip);
            byte[] extraInfo = CreateExtraInfo(zfe);

            _zipFileStream.Write(new byte[] { 80, 75, 3, 4, 20, 0}, 0, 6); // No extra header
            _zipFileStream.Write(BitConverter.GetBytes((ushort)(zfe.EncodeUTF8 ? 0x0800 : 0)), 0, 2); // filename and comment encoding 
            _zipFileStream.Write(BitConverter.GetBytes((ushort)zfe.Method), 0, 2);  // zipping method
            _zipFileStream.Write(BitConverter.GetBytes(DateTimeToDosTime(zfe.ModifyTime)), 0, 4); // zipping date and time
            _zipFileStream.Write(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0, 12); // unused CRC, un/compressed size, updated later
            _zipFileStream.Write(BitConverter.GetBytes((ushort)encodedFilename.Length), 0, 2); // filename length
            _zipFileStream.Write(BitConverter.GetBytes((ushort)extraInfo.Length), 0, 2); // extra length

            _zipFileStream.Write(encodedFilename, 0, encodedFilename.Length);
            _zipFileStream.Write(extraInfo, 0, extraInfo.Length);

            zfe.HeaderSize = (uint)(_zipFileStream.Position - pos);
        }

        /* Central directory's File header:
            central file header signature   4 bytes  (0x02014b50)
            version made by                 2 bytes
            version needed to extract       2 bytes
            general purpose bit flag        2 bytes
            compression method              2 bytes
            last mod file time              2 bytes
            last mod file date              2 bytes
            crc-32                          4 bytes
            compressed size                 4 bytes
            uncompressed size               4 bytes
            filename length                 2 bytes
            extra field length              2 bytes
            file comment length             2 bytes
            disk number start               2 bytes
            internal file attributes        2 bytes
            external file attributes        4 bytes
            relative offset of local header 4 bytes

            filename (variable size)
            extra field (variable size)
            file comment (variable size)
        */
        private void WriteCentralDirRecord(ZipFileEntry zfe)
        {
            Encoding encoder = zfe.EncodeUTF8 ? Encoding.UTF8 : _defaultEncoding;
            byte[] encodedFilename = encoder.GetBytes(zfe.FilenameInZip);
            byte[] encodedComment = encoder.GetBytes(zfe.Comment);
            byte[] extraInfo = CreateExtraInfo(zfe);

            _zipFileStream.Write(new byte[] { 80, 75, 1, 2, 23, 0xB, 20, 0 }, 0, 8);
            _zipFileStream.Write(BitConverter.GetBytes((ushort)(zfe.EncodeUTF8 ? 0x0800 : 0)), 0, 2); // filename and comment encoding 
            _zipFileStream.Write(BitConverter.GetBytes((ushort)zfe.Method), 0, 2);  // zipping method
            _zipFileStream.Write(BitConverter.GetBytes(DateTimeToDosTime(zfe.ModifyTime)), 0, 4);  // zipping date and time
            _zipFileStream.Write(BitConverter.GetBytes(zfe.Crc32), 0, 4); // file CRC
            _zipFileStream.Write(BitConverter.GetBytes(Get32BitSize(zfe.CompressedSize)), 0, 4); // compressed file size
            _zipFileStream.Write(BitConverter.GetBytes(Get32BitSize(zfe.FileSize)), 0, 4); // uncompressed file size
            _zipFileStream.Write(BitConverter.GetBytes((ushort)encodedFilename.Length), 0, 2); // filename in zip
            _zipFileStream.Write(BitConverter.GetBytes((ushort)extraInfo.Length), 0, 2); // extra length
            _zipFileStream.Write(BitConverter.GetBytes((ushort)encodedComment.Length), 0, 2);

            _zipFileStream.Write(BitConverter.GetBytes((ushort)0), 0, 2); // disk=0
            _zipFileStream.Write(BitConverter.GetBytes((ushort)0), 0, 2); // file type: binary
            _zipFileStream.Write(BitConverter.GetBytes((ushort)0), 0, 2); // Internal file attributes
            _zipFileStream.Write(BitConverter.GetBytes((ushort)0x8100), 0, 2); // External file attributes (normal/readable)
            _zipFileStream.Write(BitConverter.GetBytes(Get32BitSize(zfe.HeaderOffset)), 0, 4);  // Offset of header

            _zipFileStream.Write(encodedFilename, 0, encodedFilename.Length);
            _zipFileStream.Write(extraInfo, 0, extraInfo.Length);
            _zipFileStream.Write(encodedComment, 0, encodedComment.Length);
        }

        private static uint Get32BitSize(long size)
        {
            return size >= 0xFFFFFFFF ? 0xFFFFFFFF : (uint)size;
        }

        /* 
        Zip64 end of central directory record
            zip64 end of central dir 
            signature                       4 bytes  (0x06064b50)
            size of zip64 end of central
            directory record                8 bytes
            version made by                 2 bytes
            version needed to extract       2 bytes
            number of this disk             4 bytes
            number of the disk with the 
            start of the central directory  4 bytes
            total number of entries in the
            central directory on this disk  8 bytes
            total number of entries in the
            central directory               8 bytes
            size of the central directory   8 bytes
            offset of start of central
            directory with respect to
            the starting disk number        8 bytes
            zip64 extensible data sector    (variable size)        
        
        Zip64 end of central directory locator

            zip64 end of central dir locator 
            signature                       4 bytes  (0x07064b50)
            number of the disk with the
            start of the zip64 end of 
            central directory               4 bytes
            relative offset of the zip64
            end of central directory record 8 bytes
            total number of disks           4 bytes

        End of central dir record:
            end of central dir signature    4 bytes  (0x06054b50)
            number of this disk             2 bytes
            number of the disk with the
            start of the central directory  2 bytes
            total number of entries in
            the central dir on this disk    2 bytes
            total number of entries in
            the central dir                 2 bytes
            size of the central directory   4 bytes
            offset of start of central
            directory with respect to
            the starting disk number        4 bytes
            zipfile comment length          2 bytes
            zipfile comment (variable size)
        */
        private void WriteEndRecord(long size, long offset)
        {
            long dirOffset = _zipFileStream.Length;

            // Zip64 end of central directory record
            _zipFileStream.Position = dirOffset;
            _zipFileStream.Write(new byte[] { 80, 75, 6, 6 }, 0, 4);
            _zipFileStream.Write(BitConverter.GetBytes((long)44), 0, 8); // size of zip64 end of central directory
            _zipFileStream.Write(BitConverter.GetBytes((ushort)45), 0, 2); // version made by
            _zipFileStream.Write(BitConverter.GetBytes((ushort)45), 0, 2); // version needed to extract 
            _zipFileStream.Write(BitConverter.GetBytes((uint)0), 0, 4); // current disk
            _zipFileStream.Write(BitConverter.GetBytes((uint)0), 0, 4); // start of central directory 
            _zipFileStream.Write(BitConverter.GetBytes(_files.Count + _existingFiles), 0, 8); // total number of entries in the central directory in disk
            _zipFileStream.Write(BitConverter.GetBytes(_files.Count + _existingFiles), 0, 8); // total number of entries in the central directory
            _zipFileStream.Write(BitConverter.GetBytes(size), 0, 8); // size of the central directory
            _zipFileStream.Write(BitConverter.GetBytes(offset), 0, 8); // offset of start of central directory with respect to the starting disk number

            // Zip64 end of central directory locator
            _zipFileStream.Write(new byte[] { 80, 75, 6, 7 }, 0, 4);
            _zipFileStream.Write(BitConverter.GetBytes((uint)0), 0, 4); // number of the disk 
            _zipFileStream.Write(BitConverter.GetBytes(dirOffset), 0, 8); // relative offset of the zip64 end of central directory record
            _zipFileStream.Write(BitConverter.GetBytes((uint)1), 0, 4); // total number of disks 

            var encoder = EncodeUtf8 ? Encoding.UTF8 : _defaultEncoding;
            byte[] encodedComment = encoder.GetBytes(_comment);

            _zipFileStream.Write(new byte[] { 80, 75, 5, 6, 0, 0, 0, 0 }, 0, 8);
            _zipFileStream.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, 0, 12);
            _zipFileStream.Write(BitConverter.GetBytes((ushort)encodedComment.Length), 0, 2);
            _zipFileStream.Write(encodedComment, 0, encodedComment.Length);
        }

        // Copies all the source file into the zip storage
        private async Task StoreAsync(ZipFileEntry zfe, Stream source)
        {
            while (true)
            {
                byte[] buffer = new byte[16384];
                uint totalRead = 0;

                long posStart = _zipFileStream.Position;
                long sourceStart = source.CanSeek ? source.Position : 0;

                var outStream = zfe.Method == CompressionType.Store ? 
                    _zipFileStream : 
                    new DeflateStream(_zipFileStream, CompressionMode.Compress, true);

                try
                {
                    zfe.Crc32 = 0 ^ 0xffffffff;

                    int bytesRead;
                    do
                    {
                        bytesRead = await source.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead > 0) await outStream.WriteAsync(buffer, 0, bytesRead);

                        for (uint i = 0; i < bytesRead; i++)
                        {
                            zfe.Crc32 = _crcTable[(zfe.Crc32 ^ buffer[i]) & 0xFF] ^ (zfe.Crc32 >> 8);
                        }

                        totalRead += (uint)bytesRead;
                    } while (bytesRead > 0);

                    await outStream.FlushAsync();
                }
                finally
                {
                    if (zfe.Method == CompressionType.Deflate) outStream.Dispose();
                }

                zfe.Crc32 ^= 0xFFFFFFFF;
                zfe.FileSize = totalRead;
                zfe.CompressedSize = (uint)(_zipFileStream.Position - posStart);

                // Verify for real compression
                if (zfe.Method == CompressionType.Deflate && !ForceDeflating && source.CanSeek && zfe.CompressedSize > zfe.FileSize)
                {
                    // Start operation again with Store algorithm
                    zfe.Method = CompressionType.Store;
                    _zipFileStream.Position = posStart;
                    _zipFileStream.SetLength(posStart);
                    source.Position = sourceStart;

                    continue;
                }

                break;
            }
        }

        /* DOS Date and time:
            MS-DOS date. The date is a packed value with the following format. Bits Description 
                0-4 Day of the month (131) 
                5-8 Month (1 = January, 2 = February, and so on) 
                9-15 Year offset from 1980 (add 1980 to get actual year) 
            MS-DOS time. The time is a packed value with the following format. Bits Description 
                0-4 Second divided by 2 
                5-10 Minute (059) 
                11-15 Hour (023 on a 24-hour clock) 
        */
        private static uint DateTimeToDosTime(DateTime dt)
        {
            return (uint)(
                (dt.Second / 2) | (dt.Minute << 5) | (dt.Hour << 11) | 
                (dt.Day<<16) | (dt.Month << 21) | ((dt.Year - 1980) << 25));
        }

        private static DateTime? DosTimeToDateTime(uint dt)
        {
            int year = (int)(dt >> 25) + 1980;
            int month = (int)(dt >> 21) & 15;
            int day = (int)(dt >> 16) & 31;
            int hours = (int)(dt >> 11) & 31;
            int minutes = (int)(dt >> 5) & 63;
            int seconds = (int)(dt & 31) * 2;

            if (month == 0 || day == 0 || year >= 2107)
                return DateTime.Now;

            return new DateTime(year, month, day, hours, minutes, seconds);
        }

        private static byte[] CreateExtraInfo(ZipFileEntry zfe)
        {
            byte[] buffer = new byte[36+36];
            BitConverter.GetBytes((ushort)0x0001).CopyTo(buffer, 0); // ZIP64 Information
            BitConverter.GetBytes((ushort)32).CopyTo(buffer, 2); // Length
            BitConverter.GetBytes((ushort)1).CopyTo(buffer, 8); // Tag 1
            BitConverter.GetBytes((ushort)24).CopyTo(buffer, 10); // Size 1
            BitConverter.GetBytes(zfe.FileSize).CopyTo(buffer, 12); // MTime
            BitConverter.GetBytes(zfe.CompressedSize).CopyTo(buffer, 20); // ATime
            BitConverter.GetBytes(zfe.HeaderOffset).CopyTo(buffer, 28); // CTime

            BitConverter.GetBytes((ushort)0x000A).CopyTo(buffer, 36); // NTFS FileTime
            BitConverter.GetBytes((ushort)32).CopyTo(buffer, 38); // Length
            BitConverter.GetBytes((ushort)1).CopyTo(buffer, 44); // Tag 1
            BitConverter.GetBytes((ushort)24).CopyTo(buffer, 46); // Size 1
            BitConverter.GetBytes(zfe.ModifyTime.ToFileTime()).CopyTo(buffer, 48); // MTime
            BitConverter.GetBytes(zfe.AccessTime.ToFileTime()).CopyTo(buffer, 56); // ATime
            BitConverter.GetBytes(zfe.CreationTime.ToFileTime()).CopyTo(buffer, 64); // CTime

            return buffer;
        }

        private static void ReadExtraInfo(byte[] buffer, int offset, ZipFileEntry zfe)
        {
            if (buffer.Length < 4)
                return;

            int pos = offset;
            while (pos < buffer.Length - 4)
            {
                uint extraId = BitConverter.ToUInt16(buffer, pos);
                uint length = BitConverter.ToUInt16(buffer, pos+2);

                uint size;
                uint tag;
                switch (extraId)
                {
                    case 0x0001:  // ZIP64 Information
                    {
                        tag = BitConverter.ToUInt16(buffer, pos + 8);
                        size = BitConverter.ToUInt16(buffer, pos + 10);

                        if (tag == 1 && size >= 24)
                        {
                            if (zfe.FileSize == 0xFFFFFFFF)
                                zfe.FileSize = BitConverter.ToInt64(buffer, pos+12);
                            if (zfe.CompressedSize == 0xFFFFFFFF)
                                zfe.CompressedSize = BitConverter.ToInt64(buffer, pos+20);
                            if (zfe.HeaderOffset == 0xFFFFFFFF)
                                zfe.HeaderOffset = BitConverter.ToInt64(buffer, pos+28);
                        }

                        break;
                    }
                    case 0x000A:  // NTFS FileTime
                    {
                        tag = BitConverter.ToUInt16(buffer, pos + 8);
                        size = BitConverter.ToUInt16(buffer, pos + 10);

                        if (tag == 1 && size == 24)
                        {
                            zfe.ModifyTime = DateTime.FromFileTime(BitConverter.ToInt64(buffer, pos+12));
                            zfe.AccessTime = DateTime.FromFileTime(BitConverter.ToInt64(buffer, pos+20));
                            zfe.CreationTime = DateTime.FromFileTime(BitConverter.ToInt64(buffer, pos+28));
                        }

                        break;
                    }
                }

                pos += (int)length + 4;
            }        
        }

        /* CRC32 algorithm
          The 'magic number' for the CRC is 0xdebb20e3.  
          The proper CRC pre and post conditioning is used, meaning that the CRC register is
          pre-conditioned with all ones (a starting value of 0xffffffff) and the value is post-conditioned by
          taking the one's complement of the CRC residual.
          If bit 3 of the general purpose flag is set, this field is set to zero in the local header and the correct
          value is put in the data descriptor and in the central directory.
        */
        private void UpdateCrcAndSizes(ZipFileEntry zfe)
        {
            long lastPos = _zipFileStream.Position;  // remember position

            _zipFileStream.Position = zfe.HeaderOffset + 8;
            _zipFileStream.Write(BitConverter.GetBytes((ushort)zfe.Method), 0, 2);  // zipping method

            _zipFileStream.Position = zfe.HeaderOffset + 14;
            _zipFileStream.Write(BitConverter.GetBytes(zfe.Crc32), 0, 4);  // Update CRC
            _zipFileStream.Write(BitConverter.GetBytes(Get32BitSize(zfe.CompressedSize)), 0, 4);  // Compressed size
            _zipFileStream.Write(BitConverter.GetBytes(Get32BitSize(zfe.FileSize)), 0, 4);  // Uncompressed size

            _zipFileStream.Position = lastPos;  // restore position
        }

        // Replaces backslashes with slashes to store in zip header
        private static string NormalizedFilename(string filename)
        {
            string normalizedFilename = filename.Replace('\\', '/');

            int pos = normalizedFilename.IndexOf(':');
            if (pos >= 0)
                normalizedFilename = normalizedFilename.Remove(0, pos + 1);

            return normalizedFilename.Trim('/');
        }

        // Reads the end-of-central-directory record
        private bool ReadFileInfo()
        {
            if (_zipFileStream.Length < 22)
                return false;

            _zipFileStream.Seek(0, SeekOrigin.Begin);
            
            var br = new BinaryReader(_zipFileStream);
            uint headerSig = br.ReadUInt32();
            if (headerSig != 0x04034b50)
            {
                // not PK.. signature header
                return false;
            }
            
            var end = _zipFileStream.Seek(-17, SeekOrigin.End);

            try
            {
                do
                {
                    _zipFileStream.Seek(-5, SeekOrigin.Current);
                    uint sig = br.ReadUInt32();

                    if (sig == 0x06054b50) // It is central dir
                    {
                        long dirPosition = _zipFileStream.Position - 4;

                        _zipFileStream.Seek(6, SeekOrigin.Current);

                        long entries = br.ReadUInt16();
                        long centralSize = br.ReadInt32();
                        long centralDirOffset = br.ReadUInt32();
                        ushort commentSize = br.ReadUInt16();

                        long commentPosition = _zipFileStream.Position;

                        if (centralDirOffset == 0xffffffff) // It is a Zip64 file
                        {
                            _zipFileStream.Position = dirPosition - 20;

                            sig = br.ReadUInt32();

                            if (sig != 0x07064b50) // Not a Zip64 central dir locator
                                return false;

                            _zipFileStream.Seek(4, SeekOrigin.Current);

                            long dir64Position = br.ReadInt64();
                            _zipFileStream.Position = dir64Position;

                            sig = br.ReadUInt32();

                            if (sig != 0x06064b50) // Not a Zip64 central dir record
                                return false;

                            _zipFileStream.Seek(28, SeekOrigin.Current);
                            entries = br.ReadInt64();
                            centralSize = br.ReadInt64();
                            centralDirOffset = br.ReadInt64();
                        }

                        // check if comment field is the very last data in file
                        if (commentPosition + commentSize != _zipFileStream.Length)
                            return false;

                        // Copy entire central directory to a memory buffer
                        _existingFiles = entries;
                        _centralDirImage = new byte[centralSize];
                        _zipFileStream.Seek(centralDirOffset, SeekOrigin.Begin);
                        int actualSize = _zipFileStream.Read(_centralDirImage, 0, (int)centralSize);
                        if (actualSize < centralSize)
                            return false;

                        // Leave the pointer at the beginning of central dir, to append new files
                        _zipFileStream.Seek(centralDirOffset, SeekOrigin.Begin);

                        return true;
                    }
                } while (_zipFileStream.Position > end - SixtyFourK);
            }
            catch
            {
                //br.Dispose(); // This would close the underlying stream. So don't.
            }

            return false;
        }
#endregion

#region IDisposable implementation
        /// <inheritdoc />
        public void Dispose() 
        {
            Dispose(true);

            GC.SuppressFinalize(this);      
        }

        /// <summary>
        /// Closes the Zip file stream
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                    Close();
 
                _isDisposed = true;   
            }
        }        
#endregion
    }
}
