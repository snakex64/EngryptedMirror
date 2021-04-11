using DokanNet;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace MagicMirror
{
    class Program
    {
        public static string GetPassword()
        {
            string pwd = "";
            while (true)
            {
                ConsoleKeyInfo i = Console.ReadKey(true);
                if (i.Key == ConsoleKey.Enter)
                {
                    break;
                }
                else if (i.Key == ConsoleKey.Backspace)
                {
                    if (pwd.Length > 0)
                    {
                        pwd = pwd[..^1];
                        Console.Write("\b \b");
                    }
                }
                else if (i.KeyChar != '\u0000') // KeyChar == '\u0000' if the key pressed does not correspond to a printable character, e.g. F1, Pause-Break, etc
                {
                    pwd = pwd + i.KeyChar;
                    Console.Write("*");
                }
            }
            return pwd;
        }
        static void Main(string[] args)
        {
            string letter, path, password;
            if (args.Length >= 1)
                letter = args[0];
            else
            {
                Console.WriteLine("letter:");
                letter = Console.ReadLine();
            }
            if (args.Length >= 2)
                path = args[1];
            else
            {
                Console.WriteLine("Path:");
                path = Console.ReadLine();
            }

            if (args.Length == 3)
                password = args[2];
            else
            {
                Console.WriteLine("Password:");
                password = GetPassword();
            }

            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(password))
                return;

            new Thread(CacheThread).Start();

            var mirror = new Mirror(new FsEncryptedMirror(path, new EncryptedFileStream.Options(password)));

            mirror.Mount(@$"{letter}:\", new DokanNet.Logging.NullLogger());
        }

        static void CacheThread()
        {
            while(true)
            {
                Thread.Sleep(2000);

                try
                {
                    EncryptedFileStream.ClearCache();
                }
                catch(Exception)
                {
                }
            }
        }
    }
}
