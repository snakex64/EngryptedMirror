using DokanNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

using FileAccess = DokanNet.FileAccess;

namespace MagicMirror
{
    public class FsEncryptedMirror : MirrorBackend
    {
        private readonly string BackendPath;

        private readonly EncryptedFileStream.Options EncryptedFileStreamOptions;

        public FsEncryptedMirror(string backendPath, EncryptedFileStream.Options encryptedFileStreamOptions)
        {
            BackendPath = backendPath;
            EncryptedFileStreamOptions = encryptedFileStreamOptions;
        }

        private string GetPath(string path)
        {
            if (path.StartsWith("/") || path.StartsWith("\\"))
                return Path.Combine(BackendPath, path[1..]);
            else
                return Path.Combine(BackendPath, path);
        }


        #region CreateFolder

        public override NtStatus CreateFolder(string fileName, DokanNet.FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, IDokanFileInfo info)
        {
            var filePath = GetPath(fileName);

            try
            {
                switch (mode)
                {
                    case FileMode.Open:
                        if (!Directory.Exists(filePath))
                        {
                            try
                            {
                                if (!File.GetAttributes(filePath).HasFlag(FileAttributes.Directory))
                                    return DokanResult.NotADirectory;
                            }
                            catch (Exception)
                            {
                                return DokanResult.FileNotFound;
                            }
                            return DokanResult.PathNotFound;
                        }

                        _ = new DirectoryInfo(filePath).EnumerateFileSystemInfos().Any();

                        return DokanResult.Success;

                    case FileMode.CreateNew:

                        if (Directory.Exists(filePath))
                            return DokanResult.FileExists;

                        if (File.Exists(filePath))
                            return DokanResult.AlreadyExists;

                        Directory.CreateDirectory(GetPath(fileName));
                        return DokanResult.Success;
                }

                return DokanResult.Success; // can this even happen ?
            }
            catch (UnauthorizedAccessException)
            {
                return DokanResult.AccessDenied;
            }
        }

        #endregion

        #region CreateFile

        public override NtStatus CreateFile(string fileName, DokanNet.FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, out object? context, out bool isDirectory)
        {
            var pathExists = true;
            var pathIsDirectory = false;

            var filePath = GetPath(fileName);


            var readWriteAttributes = (access & DataAccess) == 0;


            try
            {
                pathExists = (Directory.Exists(filePath) || File.Exists(filePath));
                pathIsDirectory = pathExists ? File.GetAttributes(filePath).HasFlag(FileAttributes.Directory) : false;
            }
            catch (IOException)
            {
            }

            isDirectory = pathIsDirectory;

            switch (mode)
            {
                case FileMode.Open:

                    if (pathExists)
                    {
                        if (readWriteAttributes || pathIsDirectory)
                        {
                            if (pathIsDirectory && (access & FileAccess.Delete) == FileAccess.Delete && (access & FileAccess.Synchronize) != FileAccess.Synchronize)
                            {
                                //It is a DeleteFile request on a directory
                                context = null;
                                return DokanResult.AccessDenied;
                            }

                            context = new object(); // should I actually open it here ? and simply re-use this object later when reading ?

                            // must set it to something if you return DokanError.Success
                            return DokanResult.Success;
                        }
                    }
                    else
                    {
                        context = new object();
                        return DokanResult.FileNotFound;
                    }

                    break;

                case FileMode.CreateNew:
                    if (pathExists)
                    {
                        context = new object();
                        return DokanResult.FileExists;
                    }

                    break;

                case FileMode.Truncate:
                    if (!pathExists)
                    {
                        context = new object();
                        return DokanResult.FileNotFound;
                    }

                    break;
            }

            context = null; // set to null so we can check later in the 'catch' if the context has been set

            try
            {
                var readAccess = (access & DataWriteAccess) == 0; // check if we have any of the "write" access, if not it means we're only trying to read
                context = new EncryptedFileStream(filePath, mode, readAccess ? System.IO.FileAccess.Read : System.IO.FileAccess.ReadWrite, share, 4096, options, EncryptedFileStreamOptions);

                bool fileCreated = mode == FileMode.CreateNew || mode == FileMode.Create || (!pathExists && mode == FileMode.OpenOrCreate);
                if (fileCreated)
                {
                    FileAttributes new_attributes = attributes;
                    new_attributes |= FileAttributes.Archive; // Files are always created as Archive
                                                              // FILE_ATTRIBUTE_NORMAL is override if any other attribute is set.
                    new_attributes &= ~FileAttributes.Normal;
                    File.SetAttributes(filePath, new_attributes);
                }

                if (pathExists && (mode == FileMode.OpenOrCreate || mode == FileMode.Create))
                    return DokanResult.AlreadyExists;

                return DokanResult.Success;
            }
            catch (UnauthorizedAccessException) // don't have access rights
            {
                if (context is IDisposable disposable)
                {
                    // returning AccessDenied cleanup and close won't be called,
                    // so we have to take care of the stream now
                    disposable.Dispose();
                    context = null;
                }
                return DokanResult.AccessDenied;
            }
            catch (DirectoryNotFoundException)
            {
                return DokanResult.PathNotFound;
            }
            catch (Exception ex)
            {
                var hr = (uint)System.Runtime.InteropServices.Marshal.GetHRForException(ex);
                switch (hr)
                {
                    case 0x80070020: //Sharing violation
                        return DokanResult.SharingViolation;
                    default:
                        throw;
                }
            }
        }

        #endregion

        #region DeleteFile

        public override NtStatus DeleteFile(string fileName, IDokanFileInfo info, bool onlyPerformCheck)
        {
            var filePath = GetPath(fileName);

            if (Directory.Exists(filePath))
                return DokanResult.AccessDenied;

            if (!File.Exists(filePath))
                return DokanResult.FileNotFound;

            if (File.GetAttributes(filePath).HasFlag(FileAttributes.Directory))
                return DokanResult.AccessDenied;


            if (onlyPerformCheck)
                return DokanResult.Success; // we just check here if we could delete the file - the true deletion is in Cleanup

            try
            {
                File.Delete(GetPath(fileName));

                return DokanResult.Success;
            }
            catch (UnauthorizedAccessException)
            {
                return DokanResult.AccessDenied;
            }
        }

        #endregion

        #region DeleteDirectory

        public override NtStatus DeleteDirectory(string fileName, IDokanFileInfo info, bool onlyPerformCheck)
        {
            var filePath = GetPath(fileName);
            try
            {
                var notEmpty = Directory.EnumerateFileSystemEntries(filePath).Any();

                if (notEmpty)// if dir is not empty it can't be deleted
                    return DokanResult.DirectoryNotEmpty;

            }
            catch (Exception)
            {
                return NtStatus.Success;
            }

            if (onlyPerformCheck)
                return DokanResult.Success;

            try
            {
                Directory.Delete(filePath);

                return DokanResult.Success;
            }
            catch (UnauthorizedAccessException)
            {
                return DokanResult.AccessDenied;
            }
        }

        #endregion

        #region ReadFile

        public override NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, object context)
        {
            if (context == null) // memory mapped read
            {
                using (var stream = new EncryptedFileStream(GetPath(fileName), FileMode.Open, System.IO.FileAccess.Read, FileShare.Read, 4 * 1024, FileOptions.None, EncryptedFileStreamOptions))
                {
                    stream.Position = offset;
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                }
            }
            else // normal read
            {
                var stream = (Stream)context;
                lock (stream) //Protect from overlapped read
                {
                    stream.Position = offset;
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                }
            }
            return DokanResult.Success;
        }

        #endregion

        #region WriteFile

        public override NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, IDokanFileInfo info)
        {
            var append = offset == -1;
            if (info.Context == null)
            {
                using (var stream = new EncryptedFileStream(GetPath(fileName), append ? FileMode.Append : FileMode.Open, System.IO.FileAccess.Write, FileShare.None, 4 * 1024, FileOptions.None, EncryptedFileStreamOptions))
                {
                    if (!append) // Offset of -1 is an APPEND: https://docs.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-writefile
                    {
                        stream.Position = offset;
                    }
                    stream.Write(buffer, 0, buffer.Length);
                    bytesWritten = buffer.Length;
                }
            }
            else
            {
                var stream = (Stream)info.Context;
                lock (stream) //Protect from overlapped write
                {
                    if (append)
                    {
                        if (stream.CanSeek)
                        {
                            stream.Seek(0, SeekOrigin.End);
                        }
                        else
                        {
                            bytesWritten = 0;
                            return DokanResult.Error;
                        }
                    }
                    else
                    {
                        stream.Position = offset;
                    }
                    stream.Write(buffer, 0, buffer.Length);
                }
                bytesWritten = buffer.Length;
            }
            return DokanResult.Success;
        }

        #endregion

        #region FlushFileBuffers

        public override NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
        {
            try
            {
                ((Stream)info.Context).Flush();
                return DokanResult.Success;
            }
            catch (IOException)
            {
                return DokanResult.DiskFull;
            }
        }

        #endregion

        #region GetFileInformation

        public override NtStatus GetFileInformation(string fileName, out FileInformation fileInfo)
        {
            // may be called with info.Context == null, but usually it isn't
            var filePath = GetPath(fileName);
            FileSystemInfo finfo = new FileInfo(filePath);
            if (!finfo.Exists)
                finfo = new DirectoryInfo(filePath);

            fileInfo = new FileInformation
            {
                FileName = fileName,
                Attributes = finfo.Attributes,
                CreationTime = finfo.CreationTime,
                LastAccessTime = finfo.LastAccessTime,
                LastWriteTime = finfo.LastWriteTime,
                Length = (finfo as FileInfo)?.Length ?? 0,
            };
            return DokanResult.Success;
        }

        #endregion

        #region FindFiles

        public override NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            // This function is not called because FindFilesWithPattern is implemented
            // Return DokanResult.NotImplemented in FindFilesWithPattern to make FindFiles called
            files = FindFilesHelper(fileName, "*");

            return DokanResult.Success;
        }

        public IList<FileInformation> FindFilesHelper(string fileName, string searchPattern)
        {
            IList<FileInformation> files = new DirectoryInfo(GetPath(fileName))
                .EnumerateFileSystemInfos()
                .Where(finfo => DokanHelper.DokanIsNameInExpression(searchPattern, finfo.Name, true))
                .Select(finfo => new FileInformation
                {
                    Attributes = finfo.Attributes,
                    CreationTime = finfo.CreationTime,
                    LastAccessTime = finfo.LastAccessTime,
                    LastWriteTime = finfo.LastWriteTime,
                    Length = (finfo as FileInfo)?.Length ?? 0,
                    FileName = finfo.Name
                }).ToArray();

            return files;
        }

        #endregion

        #region SetFileAttributes

        public override NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info)
        {
            try
            {
                // MS-FSCC 2.6 File Attributes : There is no file attribute with the value 0x00000000
                // because a value of 0x00000000 in the FileAttributes field means that the file attributes for this file MUST NOT be changed when setting basic information for the file
                if (attributes != 0)
                    File.SetAttributes(GetPath(fileName), attributes);

                return DokanResult.Success;
            }
            catch (UnauthorizedAccessException)
            {
                return DokanResult.AccessDenied;
            }
            catch (FileNotFoundException)
            {
                return DokanResult.FileNotFound;
            }
            catch (DirectoryNotFoundException)
            {
                return DokanResult.PathNotFound;
            }
        }

        #endregion

        #region SetFileTime

        public override NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, IDokanFileInfo info)
        {
            try
            {
                if (info.Context is EncryptedFileStream encryptedFileStream)
                {
                    var ct = creationTime?.ToFileTime() ?? 0;
                    var lat = lastAccessTime?.ToFileTime() ?? 0;
                    var lwt = lastWriteTime?.ToFileTime() ?? 0;
                
                    if (DokanNetMirror.NativeMethods.SetFileTime(encryptedFileStream.OriginalFileStream.SafeFileHandle, ref ct, ref lat, ref lwt))
                        return DokanResult.Success;
                    throw System.Runtime.InteropServices.Marshal.GetExceptionForHR(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                }

                var filePath = GetPath(fileName);

                if (creationTime.HasValue)
                    File.SetCreationTime(filePath, creationTime.Value);

                if (lastAccessTime.HasValue)
                    File.SetLastAccessTime(filePath, lastAccessTime.Value);

                if (lastWriteTime.HasValue)
                    File.SetLastWriteTime(filePath, lastWriteTime.Value);

                return DokanResult.Success;
            }
            catch (UnauthorizedAccessException)
            {
                return DokanResult.AccessDenied;
            }
            catch (FileNotFoundException)
            {
                return DokanResult.FileNotFound;
            }
        }

        #endregion

        #region MoveFile

        public override NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            var oldpath = GetPath(oldName);
            var newPath = GetPath(newName);

            (info.Context as Stream)?.Dispose();
            info.Context = null;

            var newPathExists = info.IsDirectory ? Directory.Exists(newPath) : File.Exists(newPath);

            try
            {

                if (!newPathExists)
                {
                    info.Context = null;
                    if (info.IsDirectory)
                        Directory.Move(oldpath, newPath);
                    else
                        File.Move(oldpath, newPath);
                    return DokanResult.Success;
                }
                else if (replace)
                {
                    info.Context = null;

                    if (info.IsDirectory) //Cannot replace directory destination - See MOVEFILE_REPLACE_EXISTING
                        return DokanResult.AccessDenied;

                    File.Delete(newPath);
                    File.Move(oldpath, newPath);

                    return DokanResult.Success;
                }
            }
            catch (UnauthorizedAccessException)
            {
                return DokanResult.AccessDenied;
            }
            return DokanResult.FileExists;
        }

        #endregion

        #region SetEndOfFile

        public override NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
        {
            try
            {
                ((Stream)info.Context).SetLength(length);
                return DokanResult.Success;
            }
            catch (IOException)
            {
                return DokanResult.DiskFull;
            }
        }

        #endregion

        #region SetAllocationSize

        public override NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
        {
            try
            {
                ((Stream)info.Context).SetLength(length);
                return DokanResult.Success;
            }
            catch (IOException)
            {
                return DokanResult.DiskFull;
            }
        }

        #endregion

        #region LockFile

        public override NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
#if !NETCOREAPP1_0
            try
            {
                ((EncryptedFileStream)info.Context).OriginalFileStream.Lock(offset, length);
                return DokanResult.Success;
            }
            catch (IOException)
            {
                return DokanResult.AccessDenied;
            }
#else
// .NET Core 1.0 do not have support for FileStream.Lock
            return DokanResult.NotImplemented;
#endif
        }

        #endregion

        #region UnlockFile

        public override NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
#if !NETCOREAPP1_0
            try
            {
                ((EncryptedFileStream)info.Context).OriginalFileStream.Unlock(offset, length);
                return DokanResult.Success;
            }
            catch (IOException)
            {
                return DokanResult.AccessDenied;
            }
#else
// .NET Core 1.0 do not have support for FileStream.Unlock
            return DokanResult.NotImplemented;
#endif
        }

        #endregion

        #region GetFileSecurity

        public override NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
        {
#if !NETCOREAPP1_0
            try
            {
                var filePath = GetPath(fileName);

#pragma warning disable CA1416 // Validate platform compatibility
                if (info.IsDirectory)
                    security = new DirectoryInfo(filePath).GetAccessControl();
                else
                    security = new FileInfo(filePath).GetAccessControl();
#pragma warning restore CA1416 // Validate platform compatibility

                return DokanResult.Success;
            }
            catch (UnauthorizedAccessException)
            {
                security = null;
                return DokanResult.AccessDenied;
            }
#else
// .NET Core 1.0 do not have support for Directory.GetAccessControl
            security = null;
            return DokanResult.NotImplemented;
#endif
        }

        #endregion

        #region GetFileSecurity

        public override NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
        {
#if !NETCOREAPP1_0
            try
            {
#pragma warning disable CA1416 // Validate platform compatibility

                var filePath = GetPath(fileName);
                if (info.IsDirectory)
                    new DirectoryInfo(filePath).SetAccessControl((DirectorySecurity)security);
                else
                    new FileInfo(filePath).SetAccessControl((FileSecurity)security);

#pragma warning restore CA1416 // Validate platform compatibility

                return DokanResult.Success;
            }
            catch (UnauthorizedAccessException ex)
            {
                return DokanResult.AccessDenied;
            }
#else
// .NET Core 1.0 do not have support for Directory.SetAccessControl
            return DokanResult.NotImplemented;
#endif
        }

        #endregion

        #region Mounted

        public override NtStatus Mounted(IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        #endregion

        #region Unmounted

        public override NtStatus Unmounted(IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        #endregion

        #region FindStreams

        public override NtStatus FindStreams(string fileName, IntPtr enumContext, out string streamName, out long streamSize, IDokanFileInfo info)
        {
            streamName = string.Empty;
            streamSize = 0;
            return DokanResult.NotImplemented;
        }

        #endregion

        #region FindStreams

        public override NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
        {
            streams = new FileInformation[0];
            return DokanResult.NotImplemented;
        }

        #endregion

        #region FindFilesWithPattern

        public override NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, IDokanFileInfo info)
        {
            files = FindFilesHelper(fileName, searchPattern);

            return DokanResult.Success;
        }

        #endregion

        public override NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, IDokanFileInfo info)
        {
            var dinfo = DriveInfo.GetDrives().Single(di => string.Equals(di.RootDirectory.Name, Path.GetPathRoot(BackendPath + "\\"), StringComparison.OrdinalIgnoreCase));

            freeBytesAvailable = dinfo.TotalFreeSpace;
            totalNumberOfBytes = dinfo.TotalSize;
            totalNumberOfFreeBytes = dinfo.AvailableFreeSpace;
            return DokanResult.Success;
        }

    }
}
