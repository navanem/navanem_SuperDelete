//Copyright 2015 Marcel Nita (marcel.nita@gmail.com)
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Win32.SafeHandles;
using SuperDelete.Core.Abstractions;
using SuperDelete.Core.Interop;
using SuperDelete.Core.Models;

namespace SuperDelete.Core.Services
{
    /// <summary>
    /// The shared long-path deletion engine. This is a direct refactor of the original
    /// <c>FileDeleter</c>: the Win32 calls and recursion are preserved, but the static singleton
    /// console-logging is replaced with per-call <see cref="IProgress{T}"/> reporting, cancellation
    /// support, and an optional preview ("dry run") mode so the same code can drive both the CLI and
    /// the WPF app.
    /// </summary>
    public sealed class DeletionService : IDeletionService
    {
        private sealed class DirectoryWithAttributes
        {
            public string Directory = string.Empty;
            public NativeMethods.FileAttributes Attributes;
        }

        /// <summary>Per-call mutable state, so a single service instance is safe to reuse.</summary>
        private sealed class RunContext
        {
            public required DeletionOptions Options;
            public IProgress<DeletionProgress>? Progress;
            public CancellationToken CancellationToken;
            public int FilesProcessed;
            public int FoldersProcessed;

            public void ReportFile(string path)
            {
                FilesProcessed++;
                Progress?.Report(new DeletionProgress(path, false, FilesProcessed, FoldersProcessed, Options.PreviewOnly));
            }

            public void ReportFolder(string path)
            {
                FoldersProcessed++;
                Progress?.Report(new DeletionProgress(path, true, FilesProcessed, FoldersProcessed, Options.PreviewOnly));
            }
        }

        public DeletionResult Delete(
            string path,
            DeletionOptions options,
            IProgress<DeletionProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var context = new RunContext
            {
                Options = options,
                Progress = progress,
                CancellationToken = cancellationToken
            };

            var stopwatch = Stopwatch.StartNew();

            // Resolve to a full path up front, mirroring the original CLI behaviour.
            string fullPath = PathUtils.GetFullPath(path);

            try
            {
                // Bypassing ACLs requires enabling these privileges first. A preview never deletes,
                // so it never needs (and must not demand) administrative privileges.
                if (options.BypassAcl && !options.PreviewOnly)
                {
                    EnablePrivilege("SeBackupPrivilege");
                    EnablePrivilege("SeRestorePrivilege");
                    EnablePrivilege("SeTakeOwnershipPrivilege");
                    EnablePrivilege("SeSecurityPrivilege");
                }

                uint fileAttrs = NativeMethods.GetFileAttributesW(PathUtils.EnsureExtendedLengthPrefix(fullPath));
                if (fileAttrs == 0xFFFFFFFF)
                {
                    ThrowLastErrorException("Could not read attributes of {0}. Does the path exist?", fullPath);
                }

                if ((fileAttrs & (uint)FileAttributes.Directory) == (uint)FileAttributes.Directory)
                {
                    DeleteFolder(context, fullPath, 0);
                }
                else
                {
                    DeleteSingleFile(context, fullPath);
                }

                stopwatch.Stop();

                return options.PreviewOnly
                    ? DeletionResult.Preview(fullPath, context.FilesProcessed, context.FoldersProcessed, stopwatch.Elapsed)
                    : DeletionResult.Success(fullPath, context.FilesProcessed, context.FoldersProcessed, stopwatch.Elapsed);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                return DeletionResult.Cancelled(fullPath, context.FilesProcessed, context.FoldersProcessed, stopwatch.Elapsed);
            }
            catch (Exception e)
            {
                stopwatch.Stop();
                return DeletionResult.Failure(
                    fullPath, context.FilesProcessed, context.FoldersProcessed, stopwatch.Elapsed, e.Message, e);
            }
        }

        public Task<DeletionResult> DeleteAsync(
            string path,
            DeletionOptions options,
            IProgress<DeletionProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Delete(path, options, progress, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Enables a specified privilege. Required to perform certain administrative actions in Windows.
        /// </summary>
        private static void EnablePrivilege(string priv)
        {
            if (!NativeMethods.LookupPrivilegeValue(null, priv, out NativeMethods.LUID privLuid))
            {
                ThrowLastErrorException("Could not look up privilege {0}", priv);
            }

            if (!NativeMethods.OpenProcessToken(NativeMethods.GetCurrentProcess(), TokenAccessLevels.AdjustPrivileges, out NativeMethods.SafeAccessTokenHandle token))
            {
                ThrowLastErrorException("Could not open process token");
            }

            using (token)
            {
                NativeMethods.TOKEN_PRIVILEGE tokenpriv = new NativeMethods.TOKEN_PRIVILEGE();
                tokenpriv.PrivilegeCount = 1;
                tokenpriv.Privilege.Luid = privLuid;
                tokenpriv.Privilege.Attributes = NativeMethods.SE_PRIVILEGE_ENABLED;
                if (!NativeMethods.AdjustTokenPrivileges(token, false, ref tokenpriv, 0, IntPtr.Zero, IntPtr.Zero))
                {
                    ThrowLastErrorException("Could not adjust token for privilege {0}", priv);
                }

                int lastError = Marshal.GetLastWin32Error();
                if (lastError != 0)
                {
                    ThrowLastErrorException("Could not enable token for privilege {0}. Are you running as Administrator?", priv);
                }
            }
        }

        private void DeleteSingleFile(RunContext context, string filePath)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            context.ReportFile(filePath);

            if (context.Options.PreviewOnly)
            {
                // Dry run: count the file, but make no changes on disk.
                return;
            }

            string prefixed = PathUtils.EnsureExtendedLengthPrefix(filePath);

            if (context.Options.BypassAcl)
            {
                DeleteFileBackupSemantics(prefixed);
            }
            else
            {
                if (!NativeMethods.DeleteFileW(prefixed))
                {
                    ThrowLastErrorException("Failed to delete file {0}", prefixed);
                }
            }
        }

        /// <summary>
        /// Deletes a file using backup semantics. This bypasses ACLs if the user has administrative rights.
        /// </summary>
        private static unsafe void DeleteFileBackupSemantics(string lpFileName)
        {
            using (SafeFileHandle fileHandle = NativeMethods.CreateFile(lpFileName,
                NativeMethods.EFileAccess.DELETE,
                FileShare.None,
                IntPtr.Zero,
                FileMode.Open,
                (int)(NativeMethods.FileAttributes.DeleteOnClose | NativeMethods.FileAttributes.BackupSemantics),
                IntPtr.Zero))
            {
                if (fileHandle.IsInvalid)
                {
                    ThrowLastErrorException("Failed attempting open file {0} with backup semantics", lpFileName);
                }

                var dispositionInfo = new NativeMethods.FILE_DISPOSITION_INFORMATION();
                dispositionInfo.DeleteFile = true;

                var ioStatusBlock = new NativeMethods.IO_STATUS_BLOCK();
                int retVal = NativeMethods.NtSetInformationFile(fileHandle, ref ioStatusBlock, new IntPtr(&dispositionInfo), Marshal.SizeOf(dispositionInfo), NativeMethods.FILE_INFORMATION_CLASS.FileDispositionInformation);
                if (retVal != 0)
                {
                    ThrowLastErrorException(NativeMethods.RtlNtStatusToDosError(retVal), "Couldn't set delete disposition on {0}", lpFileName);
                }
            }
        }

        private void DeleteFolder(RunContext context, string folderPath, NativeMethods.FileAttributes parentAttributes)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var baseFolderPath = PathUtils.EnsureExtendedLengthPrefix(folderPath);
            var searchTerm = Path.Combine(baseFolderPath, "*");

            var directories = new List<DirectoryWithAttributes>();

            NativeMethods.WIN32_FIND_DATAW findInfo;
            NativeMethods.FindFileSafeHandle searchHandle = NativeMethods.FindFirstFileW(searchTerm, out findInfo);
            if (searchHandle.IsInvalid)
            {
                ThrowLastErrorException("Error locating files in {0}", searchTerm);
            }

            using (searchHandle)
            {
                do
                {
                    context.CancellationToken.ThrowIfCancellationRequested();

                    var isDirectory = ((uint)findInfo.dwFileAttributes & (uint)NativeMethods.FileAttributes.Directory) == (uint)NativeMethods.FileAttributes.Directory;
                    var fullFilePath = Path.Combine(folderPath, findInfo.cFileName);

                    if ((findInfo.dwFileAttributes & NativeMethods.FileAttributes.ReparsePoint) != 0)
                    {
                        // reparse points can be removed directly. If we attempt to follow down into the reparse
                        // point, then we start getting weird error messages when unexpected files get deleted
                        // or permissions cannot be obtained.
                        context.ReportFolder(fullFilePath);

                        if (!context.Options.PreviewOnly && !NativeMethods.RemoveDirectoryW(fullFilePath))
                        {
                            ThrowLastErrorException("Failed to remove reparse point {0}", fullFilePath);
                        }
                    }
                    else if (isDirectory)
                    {
                        if (string.Compare(findInfo.cFileName, ".", StringComparison.InvariantCultureIgnoreCase) == 0 ||
                            string.Compare(findInfo.cFileName, "..", StringComparison.InvariantCultureIgnoreCase) == 0)
                        {
                            continue;
                        }

                        directories.Add(new DirectoryWithAttributes { Directory = fullFilePath, Attributes = findInfo.dwFileAttributes });
                    }
                    else
                    {
                        if (!context.Options.PreviewOnly)
                        {
                            RemoveReadonlyAttribute(fullFilePath, findInfo.dwFileAttributes);
                        }

                        DeleteSingleFile(context, fullFilePath);
                    }

                } while (NativeMethods.FindNextFileW(searchHandle, out findInfo));
            }

            foreach (var directory in directories)
            {
                if (!context.Options.PreviewOnly)
                {
                    RemoveReadonlyAttribute(directory.Directory, directory.Attributes);
                }

                DeleteFolder(context, directory.Directory, directory.Attributes);
            }

            context.ReportFolder(folderPath);
            if (!context.Options.PreviewOnly && !NativeMethods.RemoveDirectoryW(baseFolderPath))
            {
                ThrowLastErrorException("Failed to remove directory {0}", baseFolderPath);
            }
        }

        /// <summary>
        /// Removes read only attribute from file or directory if it has one.
        /// </summary>
        private static void RemoveReadonlyAttribute(string filename, NativeMethods.FileAttributes currentAttributes)
        {
            NativeMethods.FileAttributes attributesToRemove = NativeMethods.FileAttributes.Readonly;

            if (((uint)currentAttributes & (uint)attributesToRemove) != 0)
            {
                var newAttributes = (uint)(currentAttributes & (~attributesToRemove));

                if (!NativeMethods.SetFileAttributesW(PathUtils.EnsureExtendedLengthPrefix(filename), newAttributes))
                {
                    ThrowLastErrorException("Failed to remove {0} attribute on {1}", (currentAttributes & attributesToRemove), filename);
                }
            }
        }

        private static void ThrowLastErrorException(string message, params object[] args)
        {
            ThrowLastErrorException(Marshal.GetLastWin32Error(), message, args);
        }

        private static void ThrowLastErrorException(int error, string message, params object[] args)
        {
            string errorMessage = new Win32Exception(error).Message;

            throw new Win32Exception(error, errorMessage + ". " + string.Format(message, args));
        }
    }
}
