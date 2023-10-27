// Used for debugging purposes only
// Set GenerateProgramFile to false in test.csproj

namespace Test
{
    public class Program
    {
        static void Main()
        {
            var test = new UnitTestFolder();
            UnitTestFolder.Initialize(null);
            UnitTestFolder.FolderTest();
        }
    }
}