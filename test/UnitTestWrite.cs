using System;
using System.IO;
using ZipStorer;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test;

[TestClass]
public class UnitTestWrite
{
    private const string Content = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";
    private const string SampleFile = "sample1.zip";
    private static readonly byte[] Buffer = Encoding.UTF8.GetBytes(Content);

    [TestMethod]
    public void CreateFileTest()
    {
        File.Delete(SampleFile);
        using var zip = ZipStorer.ZipStorer.Create(SampleFile);
    }

    [TestMethod]
    public async Task AddStreamTest()
    {
        await CreateSampleFile();

        using var zip = ZipStorer.ZipStorer.Open(SampleFile, FileAccess.Read);
        var dir = zip.ReadCentralDir();
        Assert.IsFalse(dir.Count == 0);
        Assert.IsTrue(dir[0].FilenameInZip == "Lorem.txt");
    }

    [TestMethod]
    public async Task AddStreamDateTest()
    {
        var now = DateTime.Now;

        await CreateSampleFile();

        using var zip = ZipStorer.ZipStorer.Open(SampleFile, FileAccess.Read);

        var dir = zip.ReadCentralDir();
        Assert.IsFalse(dir.Count == 0);
        Assert.IsTrue(dir[0].CreationTime >= now, "Creation Time failed");
        Assert.IsTrue(dir[0].ModifyTime >= now, "Modify Time failed");
        Assert.IsTrue(dir[0].AccessTime >= now, "Access Time failed");
    }

    [TestMethod]
    public async Task CompressionTest()
    {
        await CreateSampleFile();

        using var zip = ZipStorer.ZipStorer.Open(SampleFile, FileAccess.Read);
        var dir = zip.ReadCentralDir();
        Assert.IsFalse(dir.Count == 0);
        Assert.IsTrue(dir[0].Method == CompressionType.Deflate);
        Assert.IsTrue(dir[0].CompressedSize < Buffer.Length);
    }

    public static async Task CreateSampleFile()
    {
        using var mem = new MemoryStream(Buffer);
        File.Delete(SampleFile);
        using var zip = ZipStorer.ZipStorer.Create(SampleFile);
        await zip.AddStreamAsync(CompressionType.Deflate, "Lorem.txt", mem, DateTime.Now);
    }
}