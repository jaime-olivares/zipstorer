using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test
{
    [TestClass]
    public class UnitTest1
    {
        [TestInitialize]
        public void Initialize()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [TestMethod]
        public void OpenRead_Test()
        {
            using (ZipStorer zip = ZipStorer.Open("sample.zip", FileAccess.Read))
            {                
            }
        }

        [TestMethod]
        public void OpenReadWrite_Test()
        {
            using (ZipStorer zip = ZipStorer.Open("sample.zip", FileAccess.ReadWrite))
            {
            }
        }
    }
}
