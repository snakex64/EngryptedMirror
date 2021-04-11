using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MagicMirror
{
    public class FilesStore
    {
        private readonly ConcurrentDictionary<string, FileStoreFile> Files = new ConcurrentDictionary<string, FileStoreFile>();

        private readonly object Lock = new object();

        private readonly MirrorBackend OnlineBackend;

        private readonly string ServerPath;

        public FilesStore(MirrorBackend onlineBackend, string serverPath)
        {
            OnlineBackend = onlineBackend;
            ServerPath = serverPath;
        }

        private string GetPath(string relativePath)
        {
            if (relativePath.StartsWith("/") || relativePath.StartsWith("\\"))
                return Path.Combine(ServerPath, relativePath[1..]);
            else
                return Path.Combine(ServerPath, relativePath);
        }

        public DokanNet.NtStatus MakeSureFileIsReady(string relativePath, [MaybeNullWhen(false)] out FileStoreFile fileStoreFile)
        {
            lock (Lock) // temporary, need to make this method multi thread, not just thread safe
            {
                if (Files.TryGetValue(relativePath, out fileStoreFile)) // we have it !
                    return DokanNet.NtStatus.Success; // nothing to do, the file should already be available locally


                var localPath = GetPath(relativePath);
                var directoryPath = Path.GetDirectoryName(localPath);

                if (directoryPath != null && !Directory.Exists(directoryPath)) // might be null for the root ?
                    Directory.CreateDirectory(directoryPath);


                var status = DownloadFileFromOnline(relativePath, out fileStoreFile);

                if (status != DokanNet.NtStatus.Success)
                    return status;

                if( fileStoreFile != null )
                    Files[relativePath] = fileStoreFile;

                return DokanNet.NtStatus.Success;
            }
        }

        private DokanNet.NtStatus DownloadFileFromOnline(string relativePath, out FileStoreFile? fileStoreFile)
        {
            // try to hit the file
            var status = OnlineBackend.CreateFile(relativePath, DokanNet.FileAccess.ReadData, FileShare.Read, FileMode.Open, FileOptions.SequentialScan, FileAttributes.Normal, out var context, out var isDirectory);

            if (status != DokanNet.NtStatus.Success || isDirectory)
            {
                fileStoreFile = null;
                return status;
            }

            status = OnlineBackend.GetFileInformation(relativePath, out var fileInformation);
            if (status != DokanNet.NtStatus.Success)
            {
                fileStoreFile = null;
                return status;
            }

            fileStoreFile = new FileStoreFile()
            {
                IsDownloaded = true,
                IsForcedDownloaded = false,
                RealFileInformation = fileInformation,
                RelativePath = relativePath
            };

            Task? writeTask = null;
            var buffer = new byte[4 * 1024]; // the default FileStream buffer is 4k
            using (var stream = File.Create(GetPath(relativePath)))
            {
                for (long i = 0; i < fileInformation.Length; i += buffer.Length)
                {
                    status = OnlineBackend.ReadFile(relativePath, buffer, out var read, i, context);
                    if (status != DokanNet.NtStatus.Success)
                        return status;

                    if (writeTask != null)
                        writeTask.Wait();

                    writeTask = stream.WriteAsync(buffer, 0, read);
                }

                if (writeTask != null)
                    writeTask.Wait();
            }

            return DokanNet.NtStatus.Success;
        }
    }
}
