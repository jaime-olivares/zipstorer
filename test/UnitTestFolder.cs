using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZipStorer;

namespace Test;

[TestClass]
public class UnitTestFolder
{
    private const string SampleFile = "sample3.zip";

    [TestMethod]
    public void FolderTest()
    {
        File.Delete(SampleFile);
        
        using (var zip = ZipStorer.ZipStorer.Create(SampleFile))
        {
            zip.AddDirectory(CompressionType.Deflate, ".");
        }

        Assert.IsTrue(File.Exists(SampleFile));
        File.Delete(SampleFile);
    }

    [TestMethod]
    public async Task FolderTestAsync()
    {
        // Cleanup
        File.Delete(SampleFile);
        var outPath = new DirectoryInfo(Path.GetFileNameWithoutExtension(SampleFile));
        if (outPath.Exists ) {outPath.Delete(true);}

        // Arrange
        int numberOfFiles;
        using (var zip = ZipStorer.ZipStorer.Create(SampleFile))
        {
            await zip.AddDirectoryAsync(CompressionType.Deflate, ".");
        }

        using (var zip = ZipStorer.ZipStorer.Open(SampleFile, FileAccess.Read))
        {
            numberOfFiles = zip.ReadCentralDir().Count;
        }
        
        Console.WriteLine($"Number of files zipped: {numberOfFiles}");
        Assert.IsTrue(File.Exists(SampleFile));
        var result = await ZipStorer.ZipStorer.ExtractToFolder(SampleFile);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Exists);
        var unzipped = result.GetFiles("*", SearchOption.AllDirectories).Length;
        Console.WriteLine($"Number of files unzipped: {unzipped}");
        Assert.AreEqual(numberOfFiles, unzipped);

        File.Delete(SampleFile);
        result.Delete(true);
    }
}