using System;
using System.IO;

namespace GZipTest
{
    static public class ArgumentsChecker
    {
        public static void CheckSourceFile(string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath))
                throw new Exception("Source file is not found");
        }

        public static void CheckDestinationFile(string destinationFilePath)
        {
            if (File.Exists(destinationFilePath))
                throw new Exception("Destination file already exist");
        }
    }
}
