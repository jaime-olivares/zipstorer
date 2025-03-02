using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
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
        const string sampleFile4a = "sample4a.zip";
        const string sampleFile4b = "sample4b.zip";
        const string sampleFile5 = "sample5.zip";
        const string sampleFile = "sample.zip";

        private readonly DateTime baseDate = new DateTime(2019, 1, 1);
        private readonly byte[] loremIpsum;

        public Program()
        {
            this.loremIpsum = File.ReadAllBytes("lorem_ipsum.txt");
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

            using (ZipStorer zip = ZipStorer.Create(sampleFile1)) { }
            ;
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
                Assert.IsTrue(dir[0].CompressedSize < loremIpsum.Length);
            }
        }

        [TestMethod]
        public void RemoveEntries_Test()
        {
            using (ZipStorer zip = ZipStorer.Create(sampleFile4a))
            {
                using (var mem = new MemoryStream(loremIpsum))
                {
                    zip.AddStream(ZipStorer.Compression.Deflate, "Lorem1.txt", mem, baseDate);
                }

                using (var mem = new MemoryStream(loremIpsum))
                {
                    zip.AddStream(ZipStorer.Compression.Deflate, "Lorem2.txt", mem, baseDate);
                }

                using (var mem = new MemoryStream(loremIpsum))
                {
                    zip.AddStream(ZipStorer.Compression.Deflate, "Lorem3.txt", mem, baseDate);
                }
            }

            ZipStorer zip1 = ZipStorer.Open(sampleFile4a, FileAccess.ReadWrite);
            var entries = zip1.ReadCentralDir();
            var entry = entries[1];
            ZipStorer.RemoveEntries(ref zip1, new System.Collections.Generic.List<ZipFileEntry> { entry });
            zip1.Close();

            using (ZipStorer zip = ZipStorer.Create(sampleFile4b))
            {
                using (var mem = new MemoryStream(loremIpsum))
                {
                    zip.AddStream(ZipStorer.Compression.Deflate, "Lorem1.txt", mem, baseDate);
                }

                using (var mem = new MemoryStream(loremIpsum))
                {
                    zip.AddStream(ZipStorer.Compression.Deflate, "Lorem3.txt", mem, baseDate);
                }
            }
            
            using (var stream1 = new FileStream(sampleFile4a, FileMode.Open, FileAccess.Read))
            {
                using (var stream2 = new FileStream(sampleFile4b, FileMode.Open, FileAccess.Read))
                {
                    Assert.IsTrue(streamsAreEqual(stream1, stream2));
                }
            }
        }

        // [TestMethod]
        public void Zip64_Test()
        {
            var dir = Path.Combine(Environment.CurrentDirectory, "SampleFiles5");

#if WINDOWS
            if (new DriveInfo(dir).AvailableFreeSpace < ((long)16496 * 1024 * 1024)) 
                throw new Exception("Not enough disk space (16.1 GB) for test!");
#endif

            if (Directory.Exists(dir)) 
                Directory.Delete(dir, true);

            if (Directory.Exists(dir + "_2")) 
                Directory.Delete(dir + "_2", true);

            Directory.CreateDirectory(dir);
            Directory.CreateDirectory(dir + "_2");
            File.Delete(Path.Combine(dir, "..", sampleFile5));

            // Create one test file larger than 0xFFFFFFFF and one with 0xFFFFFFFE bytes
            for (int n = 2; n <= 3; n++)
            {
                using (var fs = new FileStream(Path.Combine(dir, $"File{n}.txt"), FileMode.Create))
                {
                    int max = (int)Math.Floor(0x100000000L / (float)this.loremIpsum.Length) + (n==2 ? 1 : 0);

                    for (var i = 0; i < max; i++)
                    {
                        fs.Write(loremIpsum.AsSpan());
                    }

                    if (n == 3) 
                    {
                        int remainder = (int)(0xFFFFFFFE - fs.Length);
                        fs.Write(this.loremIpsum, 0, remainder);
                    }
                }
            }

            // zip them
            using (ZipStorer zip = ZipStorer.Create(Path.Combine(dir, "..", sampleFile5)))
            {
                zip.AddFile(ZipStorer.Compression.Deflate, Path.Combine(dir, "lorem_ipsum.txt"), "File1.txt");  // normal file
                zip.AddFile(ZipStorer.Compression.Deflate, Path.Combine(dir, "File2.txt"), "File2.txt");  // Zip64 file size, normal compressed size and offset
                zip.AddFile(ZipStorer.Compression.Store,   Path.Combine(dir, "File3.txt"), "File3.txt");  // normal file size and offset
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
                    {
                        using (var fs2 = new FileStream(Path.Combine(dir + "_2", entries[n].FilenameInZip), FileMode.Open))
                        {
                            Assert.IsTrue(streamsAreEqual(fs1, fs2));
                        }
                    }

                    File.Delete(Path.Combine(dir + "_2", entries[n].FilenameInZip));
                }
            }
        }

        private bool streamsAreEqual(Stream s1, Stream s2)
        {
            using var sha256 = SHA256.Create();
            byte[] hash1 = sha256.ComputeHash(s1);
            byte[] hash2 = sha256.ComputeHash(s2);

            return StructuralComparisons.StructuralEqualityComparer.Equals(hash1, hash2);
        }

        private void createSampleFile()
        {
            using (var mem = new MemoryStream(this.loremIpsum))
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
