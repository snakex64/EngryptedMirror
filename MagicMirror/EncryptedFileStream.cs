using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MagicMirror
{
    public class EncryptedFileStream : Stream, IDisposable
    {
        public class Options
        {
            public string PasswordKey { get; set; }

            public Options(string passwordKey)
            {
                PasswordKey = passwordKey;
            }
        }

        public FileStream OriginalFileStream { get; }

        private bool MustWriteToDisk { get; set; } = false;

        public override bool CanRead => DecryptedStream.CanRead;

        public override bool CanSeek => DecryptedStream.CanSeek;

        public override bool CanWrite => OriginalFileStream.CanWrite;

        public override long Length => DecryptedStream.Length;

        public override long Position 
        {
            get => DecryptedStream.Position;
            set
            {
                DecryptedStream.Position = value;
            }
        }

        private Stream DecryptedStream { get; set; }

        public Options EncryptionOptions { get; }

        public EncryptedFileStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options, Options encryptionOptions)
        {
            EncryptionOptions = encryptionOptions;

            var realMode = mode;
            if (mode == FileMode.Append)
                realMode = FileMode.OpenOrCreate;
            var realAccess = access;
            if (access == FileAccess.Write)
                realAccess = FileAccess.ReadWrite;

            OriginalFileStream = new FileStream(path, realMode, realAccess, share, bufferSize, options);

            DecryptedStream = new MemoryStream();
            Encryptor.DecryptContent(OriginalFileStream, DecryptedStream, encryptionOptions.PasswordKey);
            DecryptedStream.Position = 0;

            if (mode == FileMode.Append)
                DecryptedStream.Seek(0, SeekOrigin.End);

        }

        public override void Flush()
        {
            if (MustWriteToDisk)
            {
                MustWriteToDisk = false;

                OriginalFileStream.Position = 0;
                DecryptedStream.Position = 0;

                OriginalFileStream.Position = 0;
                Encryptor.EncryptContent(DecryptedStream, OriginalFileStream, EncryptionOptions.PasswordKey);

                OriginalFileStream.SetLength(OriginalFileStream.Position);
                OriginalFileStream.Flush();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return DecryptedStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return DecryptedStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            DecryptedStream.SetLength(value);
            MustWriteToDisk = true;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            MustWriteToDisk = true;

            DecryptedStream.Write(buffer, offset, count);
        }

        private bool IsDisposed = false;
        void IDisposable.Dispose()
        {
            if (IsDisposed)
                return;
            try
            {
                Flush();
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                OriginalFileStream.Close();
                IsDisposed = true;
            }
        }
    }
}
