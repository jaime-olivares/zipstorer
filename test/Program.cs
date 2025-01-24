using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
        const string sampleFile5 = "sample5.zip";
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

        [TestMethod]
        public void Zip64_Test()
        {
            var dir = Path.Combine(Environment.CurrentDirectory, "SampleFiles5");
            //if (new DriveInfo(dir).AvailableFreeSpace < ((long)12390 * 1024 * 1024)) throw new Exception("Not enough disk space (16.1 GB) for test!");
            dir = "E:\\ZZZ\\SampleFiles5";
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            if (Directory.Exists(dir + "_2")) Directory.Delete(dir + "_2", true);
            Directory.CreateDirectory(dir);
            Directory.CreateDirectory(dir + "_2");
            File.Delete(Path.Combine(dir, "..", sampleFile5));

            // generate three test files
            // standard text 
            File.WriteAllBytes(Path.Combine(dir, "File1.txt"), buffer);
            var txtBuffer = new byte[65538];
            using (var mem = new MemoryStream(txtBuffer))
            using (var bw = new BinaryWriter(mem, Encoding.ASCII))
            {
                for (int i = 0; i < 5958; i++)
                {
                    bw.Write(Encoding.ASCII.GetBytes("1234567890\n"));
                }
            }
            // one larger than 0xFFFFFFFF and one with 0xFFFFFFFE bytes
            for (int n = 2; n <= 3; n++)
            {
                using (var fs = new FileStream(Path.Combine(dir, $"File{n}.txt"), FileMode.Create))
                {
                    for (var i = 0; i < (n == 2 ? 66000 : 65534); i++)
                    {
                        fs.Write(txtBuffer, 0, txtBuffer.Length);
                    }
                    if (n == 3) fs.Write(txtBuffer, 0, 2);
                }
            }

            // zip them
            using (ZipStorer zip = ZipStorer.Create(Path.Combine(dir, "..", sampleFile5)))
            {
                zip.AddFile(ZipStorer.Compression.Deflate, Path.Combine(dir, "File1.txt"), "File1.txt");  // normal file
                zip.AddFile(ZipStorer.Compression.Deflate, Path.Combine(dir, "File2.txt"), "File2.txt");  // Zip64 file size, normal compressed size and offset
                zip.AddFile(ZipStorer.Compression.Store, Path.Combine(dir, "File3.txt"), "File3.txt");    // normal file size and offset
                zip.AddFile(ZipStorer.Compression.Deflate, Path.Combine(dir, "File2.txt"), "File4.txt");  // Zip64 file size and offset
            }

            // unzip and compare them
            using (ZipStorer zip = ZipStorer.Open(Path.Combine(dir, "..", sampleFile5), FileAccess.Read))
            {
                var entries = zip.ReadCentralDir();
                for (var n = 0; n < 4; n++)
                {
                    zip.ExtractFile(entries[n], Path.Combine(dir + "_2", entries[n].FilenameInZip));
                    using (var fs1 = new FileStream(Path.Combine(dir, entries[n == 3 ? 1 : n].FilenameInZip), FileMode.Open))
                    using (var fs2 = new FileStream(Path.Combine(dir + "_2", entries[n].FilenameInZip), FileMode.Open))
                    {
                        Assert.IsTrue(StreamsAreEqual(fs1, fs2));
                    }
                    File.Delete(Path.Combine(dir + "_2", entries[n].FilenameInZip));
                }
            }
        }

        private bool StreamsAreEqual(Stream s1, Stream s2)
        {
            if (s1.Length != s2.Length) return false;

            var bytes1 = new byte[65536];
            var bytes2 = new byte[65536];
            long bytesLeft = s1.Length;
            while (bytesLeft > 0)
            {
                var bytesRead1 = s1.Read(bytes1, 0, bytes1.Length);
                var bytesRead2 = s2.Read(bytes2, 0, bytes2.Length);
                if (!bytes1.SequenceEqual(bytes2)) return false;
                bytesLeft -= bytesRead1;
            }
            return true;
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
