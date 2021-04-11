using DokanNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

using FileAccess = DokanNet.FileAccess;

namespace MagicMirror
{
    public abstract class MirrorBackend
    {
        public static readonly FileAccess DataAccess = FileAccess.ReadData | FileAccess.WriteData | FileAccess.AppendData |
                                              FileAccess.Execute |
                                              FileAccess.GenericExecute | FileAccess.GenericWrite |
                                              FileAccess.GenericRead;

        public static readonly FileAccess DataWriteAccess = FileAccess.WriteData | FileAccess.AppendData |
                                                   FileAccess.Delete |
                                                   FileAccess.GenericWrite;
        public abstract NtStatus CreateFolder(string fileName, FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, IDokanFileInfo info);

        public abstract NtStatus CreateFile(string fileName, FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, out object context, out bool isDirectory);

        public virtual void Cleanup(string fileName, IDokanFileInfo info)
        {
            (info.Context as IDisposable)?.Dispose();
            info.Context = null;

            if (info.DeleteOnClose)
            {
                if (info.IsDirectory)
                    DeleteDirectory(fileName, info, false);
                else
                    DeleteFile(fileName, info, false);
            }
        }

        public virtual void CloseFile(string fileName, IDokanFileInfo info)
        {
            Cleanup(fileName, info);
        }

        public abstract NtStatus DeleteFile(string fileName, IDokanFileInfo info, bool onlyPerformCheck);

        public abstract NtStatus DeleteDirectory(string fileName, IDokanFileInfo info, bool onlyPerformCheck);

        public abstract NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, object context);

        public abstract NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, IDokanFileInfo info);

        public abstract NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info);

        public abstract NtStatus GetFileInformation(string fileName, out FileInformation fileInfo);

        public abstract NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info);

        public abstract NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info);

        public abstract NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, IDokanFileInfo info);

        public abstract NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info);

        public abstract NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info);

        public abstract NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info);

        public abstract NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info);

        public abstract NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info);

        public abstract NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info);

        public abstract NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info);

        public abstract NtStatus Mounted(IDokanFileInfo info);

        public abstract NtStatus Unmounted(IDokanFileInfo info);

        public abstract NtStatus FindStreams(string fileName, IntPtr enumContext, out string streamName, out long streamSize, IDokanFileInfo info);

        public abstract NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info);

        public abstract NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, IDokanFileInfo info);

        public abstract NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, IDokanFileInfo info);

    }
}
