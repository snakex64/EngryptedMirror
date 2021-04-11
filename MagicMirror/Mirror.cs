using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using DokanNet;
using FileAccess = DokanNet.FileAccess;

namespace MagicMirror
{
    public class Mirror : IDokanOperations
    {
        private readonly MirrorBackend Backend;

        public Mirror(MirrorBackend mirrorBackend)
        {
            Backend = mirrorBackend;
        }


        #region Implementation of IDokanOperations

        public NtStatus CreateFile(string fileName, FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, IDokanFileInfo info)
        {
            if (info.IsDirectory)
                return Backend.CreateFolder(fileName, access, share, mode, options, attributes, info);
            else
            {
                var status = Backend.CreateFile(fileName, access, share, mode, options, attributes, out var context, out var isDirectory);
                info.Context = context;
                info.IsDirectory = isDirectory;

                return status;
            }
        }

        public void Cleanup(string fileName, IDokanFileInfo info)
        {
            Backend.Cleanup(fileName, info);
        }

        public void CloseFile(string fileName, IDokanFileInfo info)
        {
            Backend.CloseFile(fileName, info);
        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
        {
            return Backend.ReadFile(fileName, buffer, out bytesRead, offset, info.Context);
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, IDokanFileInfo info)
        {
            return Backend.WriteFile(fileName, buffer, out bytesWritten, offset, info);
        }

        public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
        {
            return Backend.FlushFileBuffers(fileName, info);
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
        {
            return Backend.GetFileInformation(fileName, out fileInfo);
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            return Backend.FindFiles(fileName, out files, info);
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info)
        {
            return Backend.SetFileAttributes(fileName, attributes, info);
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, IDokanFileInfo info)
        {
            return Backend.SetFileTime(fileName, creationTime, lastAccessTime, lastWriteTime, info);
        }

        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
        {
            return Backend.DeleteFile(fileName, info, true);
        }

        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
        {
            return Backend.DeleteDirectory(fileName, info, true);
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            return Backend.MoveFile(oldName, newName, replace, info);
        }

        public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
        {
            return Backend.SetEndOfFile(fileName, length, info);
        }

        public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
        {
            return Backend.SetAllocationSize(fileName, length, info);
        }

        public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            return Backend.LockFile(fileName, offset, length, info);
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            return Backend.UnlockFile(fileName, offset, length, info);
        }

        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, IDokanFileInfo info)
        {
            return Backend.GetDiskFreeSpace(out freeBytesAvailable, out totalNumberOfBytes, out totalNumberOfFreeBytes, info);
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features,
            out string fileSystemName, out uint maximumComponentLength, IDokanFileInfo info)
        {
            volumeLabel = "DOKAN";
            fileSystemName = "NTFS";
            maximumComponentLength = 256;

            features = FileSystemFeatures.CasePreservedNames | FileSystemFeatures.CaseSensitiveSearch |
                       FileSystemFeatures.PersistentAcls | FileSystemFeatures.SupportsRemoteStorage |
                       FileSystemFeatures.UnicodeOnDisk;

            return DokanResult.Success;
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
        {
            return Backend.GetFileSecurity(fileName, out security, sections, info);
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
        {
            return Backend.SetFileSecurity(fileName, security, sections, info);
        }

        public NtStatus Mounted(IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus Unmounted(IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus FindStreams(string fileName, IntPtr enumContext, out string streamName, out long streamSize, IDokanFileInfo info)
        {
            return Backend.FindStreams(fileName, enumContext, out streamName, out streamSize, info);
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
        {
            return Backend.FindStreams(fileName, out streams, info);
        }


        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, IDokanFileInfo info)
        {
            return Backend.FindFilesWithPattern(fileName, searchPattern, out files, info);
        }

        #endregion Implementation of IDokanOperations
    }
}
