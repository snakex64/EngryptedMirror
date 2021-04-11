using DokanNet;
using System;
using System.IO;
using System.Security.Cryptography;

namespace MagicMirror
{
    class Program
    {
        static void Main(string[] args)
        {
            string path, password;
            if (args.Length >= 1)
                path = args[0];
            else
            {
                Console.WriteLine("Path:");
                path = Console.ReadLine();
            }

            if (args.Length == 2)
                password = args[1];
            else
            {
                Console.WriteLine("Password:");
                password = Console.ReadLine();
            }

            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(password))
                return;

            var mirror = new Mirror(new FsEncryptedMirror(path, new EncryptedFileStream.Options(password)));

            mirror.Mount(@"H:\", new DokanNet.Logging.NullLogger());
        }
    }
}
