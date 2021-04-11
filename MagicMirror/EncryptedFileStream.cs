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

        private string Path { get; }

        public EncryptedFileStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options, Options encryptionOptions)
        {
            Path = path;
            EncryptionOptions = encryptionOptions;

            var realMode = mode;
            if (mode == FileMode.Append)
                realMode = FileMode.OpenOrCreate;
            var realAccess = access;
            if (access == FileAccess.Write)
                realAccess = FileAccess.ReadWrite;

            OriginalFileStream = new FileStream(path, realMode, realAccess, share, bufferSize, options);

            if (access == FileAccess.Read && InitializeReadStreamFromCache(path) && DecryptedStream != null)
            {
                DecryptedStream.Position = 0;
            }
            else
            {
                DecryptedStream = new MemoryStream(bufferSize);
                if (mode != FileMode.CreateNew && mode != FileMode.Create && mode != FileMode.Truncate && access == FileAccess.Read)
                {
                    Encryptor.DecryptContent(OriginalFileStream, DecryptedStream, encryptionOptions.PasswordKey);
                    DecryptedStream.Position = 0;
                    ExistingStreams[path] = (((MemoryStream)DecryptedStream).ToArray(), DateTime.Now);
                }
                else
                    ExistingStreams.TryRemove(path, out var _);

                DecryptedStream.Position = 0;
            }

            if (mode == FileMode.Append)
                DecryptedStream.Seek(0, SeekOrigin.End);

        }

        public static void ClearCache()
        {
            foreach (var cache in ExistingStreams.ToList())
            {
                lock (cache.Value.Content)
                {
                    if (DateTime.Now - cache.Value.LastUse > TimeSpan.FromSeconds(2))
                        ExistingStreams.TryRemove(cache);
                }
            }
        }


        private static System.Collections.Concurrent.ConcurrentDictionary<string, (byte[] Content, DateTime LastUse)> ExistingStreams = new System.Collections.Concurrent.ConcurrentDictionary<string, (byte[], DateTime)>();
        private bool InitializeReadStreamFromCache(string path)
        {
            if (ExistingStreams.TryGetValue(path, out var cache))
            {
                lock (cache.Content)
                {
                    DecryptedStream = new MemoryStream(cache.Content);
                    ExistingStreams[path] = (cache.Content, DateTime.Now);
                }

                return true;
            }
            return false;
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
