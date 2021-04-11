using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace MagicMirror
{
    public class Encryptor
    {
        /// <summary>
        /// Creates a random salt that will be used to encrypt your file. This method is required on FileEncrypt.
        /// </summary>
        /// <returns></returns>
        private static byte[] GenerateRandomSalt()
        {
            var data = new byte[32];

            using (var rng = new RNGCryptoServiceProvider())
            {
                for (var i = 0; i < 10; i++)
                {
                    // Fille the buffer with the generated data
                    rng.GetBytes(data);
                }
            }

            return data;
        }
        internal static Stream GenerateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
        internal static void EncryptContent(Stream content, Stream outputStream, string password)
        {
            //generate random salt
            var salt = GenerateRandomSalt();

            //convert password string to byte arrray
            var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);

            //Set Rijndael symmetric encryption algorithm
            var AES = new RijndaelManaged
            {
                KeySize = 256,
                BlockSize = 128,
                Padding = PaddingMode.PKCS7
            };

            //http://stackoverflow.com/questions/2659214/why-do-i-need-to-use-the-rfc2898derivebytes-class-in-net-instead-of-directly
            //"What it does is repeatedly hash the user password along with the salt." High iteration counts.
            var key = new Rfc2898DeriveBytes(passwordBytes, salt, 50000);
            AES.Key = key.GetBytes(AES.KeySize / 8);
            AES.IV = key.GetBytes(AES.BlockSize / 8);


            // write salt to the begining of the output file, so in this case can be random every time
            outputStream.Write(salt, 0, salt.Length);

            var cs = new CryptoStream(outputStream, AES.CreateEncryptor(), CryptoStreamMode.Write, true);

            //create a buffer (1mb) so only this amount will allocate in the memory and not the whole file
            var buffer = new byte[1048576];
            int read;

            try
            {
                while ((read = content.Read(buffer, 0, buffer.Length)) > 0)
                    cs.Write(buffer, 0, read);
            }
            finally
            {
                cs.Close();
            }
        }


        internal static void DecryptContent(Stream contentStream, Stream outputStream, string password)
        {
            var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
            var salt = new byte[32];

            contentStream.Read(salt, 0, salt.Length);

            var AES = new RijndaelManaged
            {
                KeySize = 256,
                BlockSize = 128
            };
            var key = new Rfc2898DeriveBytes(passwordBytes, salt, 50000);
            AES.Key = key.GetBytes(AES.KeySize / 8);
            AES.IV = key.GetBytes(AES.BlockSize / 8);
            AES.Padding = PaddingMode.PKCS7;

            var cs = new CryptoStream(contentStream, AES.CreateDecryptor(), CryptoStreamMode.Read, true);

            int read;
            var buffer = new byte[1048576];

            while ((read = cs.Read(buffer, 0, buffer.Length)) > 0)
                outputStream.Write(buffer, 0, read);

            cs.Close();
        }
    }
}
