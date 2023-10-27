using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test
{
    [TestClass]
    public class UnitTestRead
    {
        const string sampleFile = "sample.zip";

        private readonly DateTime baseDate = new DateTime(2019,1,1);

        [ClassInitialize]
        public static void Initialize(TestContext test)
        {
            
        }

        [TestMethod]
        public void OpenRead_Test()
        {
            using (ZipStorer zip = ZipStorer.Open(sampleFile, FileAccess.Read))
            {                
            }
        }

        [TestMethod]
        public void ReadCentralDir_Test()
        {
            using (ZipStorer zip = ZipStorer.Open(sampleFile, FileAccess.Read))
            {
                var dir = zip.ReadCentralDir();
                Assert.AreEqual(dir.Count, 10);
            }
        }

        [TestMethod]
        public void ExtractFolder_Test()
        {
            using (ZipStorer zip = ZipStorer.Open(sampleFile, FileAccess.Read))
            {
                var dir = zip.ReadCentralDir();
                Assert.IsFalse(dir.Count == 0);
                zip.ExtractFile(dir[0], out byte[] output);
                Assert.AreEqual(output.Length, 0);
            }            
        }

        [TestMethod]
        public void ExtractFile_Test()
        {
            using (ZipStorer zip = ZipStorer.Open(sampleFile, FileAccess.Read))
            {
                var dir = zip.ReadCentralDir();
                Assert.IsFalse(dir.Count == 0);
                zip.ExtractFile(dir[4], out byte[] output);
                Assert.IsFalse(output.Length == 0);
            }            
        }

        [TestMethod]
        public void ReadModifyTime_Test()
        {
            using (ZipStorer zip = ZipStorer.Open(sampleFile, FileAccess.Read))
            {
                var dir = zip.ReadCentralDir();
                Assert.IsTrue(dir[0].ModifyTime > baseDate);
            }            
        }

        [TestMethod]
        public void ReadAccessTime_Test()
        {
            using (ZipStorer zip = ZipStorer.Open(sampleFile, FileAccess.Read))
            {
                var dir = zip.ReadCentralDir();
                Assert.IsTrue(dir[0].AccessTime > DateTime.Today);
            }            
        }

        [TestMethod]
        public void ReadCreationTime_Test()
        {
            using (ZipStorer zip = ZipStorer.Open(sampleFile, FileAccess.Read))
            {
                var dir = zip.ReadCentralDir();
                Assert.IsTrue(dir[0].CreationTime > baseDate);
                Assert.IsTrue(dir[0].CreationTime <= dir[0].ModifyTime);
            }            
        }
    }
}
