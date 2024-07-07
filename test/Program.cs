using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ZipStorerTest
{
    [TestClass]
    public class Program
    {
        static void Main()
        {
            // var test = new UnitTestFolder();
            // UnitTestFolder.Initialize(null);
            // test.Folder_Test();
        }

        const string sampleFile1 = "sample1.zip";
        const string sampleFile3 = "sample3.zip";
        const string sampleFile = "sample.zip";
        private static byte[] buffer;

        private readonly DateTime baseDate = new DateTime(2019,1,1);

        public Program()
        {
            string content = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";
            buffer = Encoding.UTF8.GetBytes(content);

        }

        // [TestMethod]
        public void Folder_Test()
        {
            File.Delete(sampleFile3);

            using (ZipStorer zip = ZipStorer.Create(sampleFile3))
            {
                zip.AddDirectory(ZipStorer.Compression.Deflate, "/some/folder", null);
            }

            File.Delete(sampleFile);
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
        
        [TestMethod]
        public void CreateFile_Test()
        {
            File.Delete(sampleFile1);

            using (ZipStorer zip = ZipStorer.Create(sampleFile1)) {};
        }

        [TestMethod]
        public void AddStream_Test()
        {
            this.createSampleFile();

            var now1 = DateTime.Now;
            using (ZipStorer zip = ZipStorer.Open(sampleFile1, FileAccess.Read))
            {
                var dir = zip.ReadCentralDir();
                Assert.IsFalse(dir.Count == 0);
                Assert.IsTrue(dir[0].FilenameInZip == "Lorem.txt");
            }            
        }

        [TestMethod]
        public void AddStreamDate_Test()
        {
            var now = DateTime.Now;

            this.createSampleFile();

            using (ZipStorer zip = ZipStorer.Open(sampleFile1, FileAccess.Read))
            {
                var dir = zip.ReadCentralDir();
                Assert.IsFalse(dir.Count == 0);
                Assert.IsTrue(dir[0].CreationTime >= now, "Creation Time failed");
                Assert.IsTrue(dir[0].ModifyTime >= now, "Modify Time failed");
                Assert.IsTrue(dir[0].AccessTime >= now, "Access Time failed");
            }            
        }

        [TestMethod]
        public void Compression_Test()
        {
            var now = DateTime.Now;

            this.createSampleFile();

            using (ZipStorer zip = ZipStorer.Open(sampleFile1, FileAccess.Read))
            {
                var dir = zip.ReadCentralDir();
                Assert.IsFalse(dir.Count == 0);
                Assert.IsTrue(dir[0].Method == ZipStorer.Compression.Deflate);
                Assert.IsTrue(dir[0].CompressedSize < buffer.Length);
            }            
        }

        public void createSampleFile()
        {
            using (var mem = new MemoryStream(buffer))
            {
                File.Delete(sampleFile1);
                using (ZipStorer zip = ZipStorer.Create(sampleFile1))
                {
                    zip.AddStream(ZipStorer.Compression.Deflate, "Lorem.txt", mem, DateTime.Now);
                }
            }
        }        
    }
}
