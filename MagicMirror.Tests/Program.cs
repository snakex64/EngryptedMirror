using System;
using System.IO;
using System.Net;

namespace MagicMirror.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            //NetworkCredential theNetworkCredential = new NetworkCredential("RandomUser", "password", "DESKTOP-4R99T3V");
            //
            //CredentialCache theNetCache = new CredentialCache();
            //theNetCache.Add(new Uri(@"\\DESKTOP-4R99T3V"), "Basic", theNetworkCredential);


            //var t = File.Create(@"smb://UserName:password@DESKTOP-4R99T3V/Server/test1.txt");
            var t = File.Create(@"\\DESKTOP-4R99T3V\Server\test2.txt");
        }
    }
}
