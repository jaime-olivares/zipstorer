using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// Used for debugging purposes only
// Set GenerateProgramFile to false in test.csproj

namespace Test
{
    public class Program
    {
        static void Main()
        {
            var test = new UnitTestWrite();
            test.Initialize();
            test.AddStreamDate_Test();
        }
    }
}