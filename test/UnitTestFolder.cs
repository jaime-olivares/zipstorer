using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test
{
    [TestClass]
    public class UnitTestFolder
    {
        const string sampleFile = "sample3.zip";

        [ClassInitialize]
        public static void Initialize(TestContext test)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        // [TestMethod]
        public void Folder_Test()
        {
            File.Delete(sampleFile);
            using (ZipStorer zip = ZipStorer.Create(sampleFile))
            {
                zip.AddDirectory(ZipStorer.Compression.Deflate, "/some/folder", null);
            }
        }
    }
}
