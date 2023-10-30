// ZipStorer, by Jaime Olivares
// Website: http://github.com/jaime-olivares/zipstorer



namespace ZipStorer
{
    /// <summary>
    /// Compression method enumeration
    /// </summary>
    public enum CompressionType : ushort 
    { 
        /// <summary>Uncompressed storage</summary> 
        Store = 0, 
        /// <summary>Deflate compression method</summary>
        Deflate = 8 
    }
}
