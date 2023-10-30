// ZipStorer, by Jaime Olivares
// Website: http://github.com/jaime-olivares/zipstorer

using System;



namespace ZipStorer
{
    /// <summary>
    /// Represents an entry in Zip file directory
    /// </summary>
    public class ZipFileEntry
    {
        /// <summary>Compression method</summary>
        public CompressionType Method { get; set; }

        /// <summary>Full path and filename as stored in Zip</summary>
        public string FilenameInZip { get; set; }

        /// <summary>Original file size</summary>
        public long FileSize { get; set; }

        /// <summary>Compressed file size</summary>
        public long CompressedSize { get; set; }

        /// <summary>Offset of header information inside Zip storage</summary>
        public long HeaderOffset { get; set; }

        /// <summary>Offset of file inside Zip storage</summary>
        public long FileOffset { get; set; }

        /// <summary>Size of header information</summary>
        public uint HeaderSize { get; set; }

        /// <summary>32-bit checksum of entire file</summary>
        public uint Crc32 { get; set; }

        /// <summary>Last modification time of file</summary>
        public DateTime ModifyTime { get; set; }

        /// <summary>Creation time of file</summary>
        public DateTime CreationTime { get; set; }

        /// <summary>Last access time of file</summary>
        public DateTime AccessTime { get; set; }

        /// <summary>User comment for file</summary>
        public string Comment { get; set; }

        /// <summary>True if UTF8 encoding for filename and comments, false if default (CP 437)</summary>
        public bool EncodeUTF8 { get; set; }

        /// <summary>Overriden method</summary>
        /// <returns>Filename in Zip</returns>
        public override string ToString()
        {
            return FilenameInZip;
        }
    }
}
