using System;

namespace GZipTest
{
    /// <summary>
    /// Консольное приложение для поблочного сжатия и распаковки файлов с
    /// помощью System.IO.Compression.GzipStream. 
    /// Аргументы: 
    ///     args[0] - compress/decompress
    ///     args[1] - Путь к исходному файлу
    ///     args[2] - Путь к результирующему файлу
    /// </summary>

    class Program
    {
        private static Zipper z = null;

        static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Console.CancelKeyPress += new ConsoleCancelEventHandler(CancelKeyPress);

            try
            {
                var arguments = ParseArgs(args);

                ArgumentsChecker.CheckSourceFile(arguments.SourceFilePath);
                ArgumentsChecker.CheckDestinationFile(arguments.DestinationFilePath);

                switch (arguments.TaskType)
                {
                    case TaskType.Unknown:
                        throw new Exception("Unknown task type");
                    case TaskType.Compress:
                        z = new Compressor(arguments.SourceFilePath, arguments.DestinationFilePath);
                        break;
                    case TaskType.Decompress:
                        z = new Decompressor(arguments.SourceFilePath, arguments.DestinationFilePath);
                        break;
                }

                if (z != null)
                    z.Start();

                if (z.IsError)
                {
                    Console.WriteLine(z.ErrorMessage);
                    return 1;
                }

                if (z.IsCancelled)
                {
                    Console.WriteLine("Cancelled by user");
                    return 1;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadKey();
                return 1;
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = (Exception)e.ExceptionObject;
        }

        private static Arguments ParseArgs(string[] args)
        {
            // args[0] - compress/decompress
            // args[1] - Путь к исходному файлу
            // args[2] - Путь к результирующему файлу

            if (args.Length != 3)
                throw new Exception("Wrong arguments");

            var result = new Arguments();

            result.TaskType = ParseTaskType(args[0]);

            result.SourceFilePath = args[1];

            result.DestinationFilePath = args[2];

            return result;
        }

        private static TaskType ParseTaskType(string taskTypeArgument)
        {
            switch (taskTypeArgument)
            {
                case "compress":
                    return TaskType.Compress;
                case "decompress":
                    return TaskType.Decompress;
                default:
                    return TaskType.Unknown;
            }
        }

        static void CancelKeyPress(object sender, ConsoleCancelEventArgs _args)
        {
            if (_args.SpecialKey == ConsoleSpecialKey.ControlC)
            {
                Console.WriteLine("\nCancelling...");
                _args.Cancel = true;
                z.Cancel();
            }
        }
    }
}
