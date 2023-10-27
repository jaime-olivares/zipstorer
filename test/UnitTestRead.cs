using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test;

[TestClass]
public class UnitTestRead
{
    private const string SampleFile = "sample.zip";

    private readonly DateTime _baseDate = new(2019,1,1);

    [TestMethod]
    public void OpenReadTest()
    {
        using var zip = ZipStorer.ZipStorer.Open(SampleFile, FileAccess.Read);
    }

    [TestMethod]
    public void ReadCentralDirTest()
    {
        using var zip = ZipStorer.ZipStorer.Open(SampleFile, FileAccess.Read);

        var dir = zip.ReadCentralDir();
        Assert.AreEqual(dir.Count, 10);
    }

    [TestMethod]
    public void ExtractFolderTest()
    {
        using var zip = ZipStorer.ZipStorer.Open(SampleFile, FileAccess.Read);

        var dir = zip.ReadCentralDir();
        Assert.IsFalse(dir.Count == 0);
        byte[] output = zip.ExtractFile(dir[0]);

        Assert.AreEqual(output.Length, 0);
    }

    [TestMethod]
    public void ExtractFileTest()
    {
        using var zip = ZipStorer.ZipStorer.Open(SampleFile, FileAccess.Read);

        var dir = zip.ReadCentralDir();
        Assert.IsFalse(dir.Count == 0);

        byte[] output = zip.ExtractFile(dir[4]);
        Assert.IsFalse(output.Length == 0);
    }

    [TestMethod]
    public void ReadModifyTimeTest()
    {
        using var zip = ZipStorer.ZipStorer.Open(SampleFile, FileAccess.Read);

        var dir = zip.ReadCentralDir();
        Assert.IsTrue(dir[0].ModifyTime > _baseDate);
    }

    [TestMethod]
    public void ReadAccessTimeTest()
    {
        using var zip = ZipStorer.ZipStorer.Open(SampleFile, FileAccess.Read);

        var dir = zip.ReadCentralDir();
        Assert.IsTrue(dir[0].AccessTime > DateTime.Today);
    }

    [TestMethod]
    public void ReadCreationTimeTest()
    {
        using var zip = ZipStorer.ZipStorer.Open(SampleFile, FileAccess.Read);

        var dir = zip.ReadCentralDir();
        Assert.IsTrue(dir[0].CreationTime > _baseDate);
        Assert.IsTrue(dir[0].CreationTime <= dir[0].ModifyTime);
    }
}