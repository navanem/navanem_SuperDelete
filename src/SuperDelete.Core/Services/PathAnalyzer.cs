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
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SuperDelete.Core.Abstractions;
using SuperDelete.Core.Interop;
using SuperDelete.Core.Models;

namespace SuperDelete.Core.Services
{
    /// <summary>
    /// Best-effort, read-only inspection of a path. Uses the same extended-length Win32 enumeration
    /// as the deletion engine, so counts are accurate for paths longer than 260 characters. It never
    /// throws for an expected condition (missing path, access denied); those are surfaced on the
    /// returned <see cref="PathAnalysis"/>.
    /// </summary>
    public sealed class PathAnalyzer : IPathAnalyzer
    {
        private sealed class Counter
        {
            public int Files;
            public int Folders;
            public bool AccessIssue;
            public string? AccessIssueDetail;
        }

        public Task<PathAnalysis> AnalyzeAsync(string path, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Analyze(path, cancellationToken), cancellationToken);
        }

        private static PathAnalysis Analyze(string path, CancellationToken cancellationToken)
        {
            string fullPath;
            try
            {
                fullPath = PathUtils.GetFullPath(path);
            }
            catch
            {
                // If we cannot even resolve it, report it as a non-existent path of the given length.
                return new PathAnalysis { FullPath = path, Exists = false, PathLength = path?.Length ?? 0 };
            }

            uint attrs = NativeMethods.GetFileAttributesW(PathUtils.EnsureExtendedLengthPrefix(fullPath));
            if (attrs == 0xFFFFFFFF)
            {
                return new PathAnalysis { FullPath = fullPath, Exists = false, PathLength = fullPath.Length };
            }

            bool isDirectory = (attrs & (uint)NativeMethods.FileAttributes.Directory) == (uint)NativeMethods.FileAttributes.Directory;
            bool isReparse = (attrs & (uint)NativeMethods.FileAttributes.ReparsePoint) != 0;
            bool isReadonly = (attrs & (uint)NativeMethods.FileAttributes.Readonly) != 0;

            if (!isDirectory)
            {
                return new PathAnalysis
                {
                    FullPath = fullPath,
                    Exists = true,
                    IsDirectory = false,
                    PathLength = fullPath.Length,
                    FileCount = 1,
                    FolderCount = 0,
                    TotalItems = 1,
                    HasAccessIssues = isReadonly,
                    AccessIssueDetail = isReadonly ? "The file is marked read-only (it will be cleared automatically before deletion)." : null
                };
            }

            var counter = new Counter();
            if (isReparse)
            {
                // A directory reparse point (junction/symlink) is removed as a single item; we do not
                // descend into it, exactly as the deletion engine does.
                counter.Folders = 1;
                counter.AccessIssue = true;
                counter.AccessIssueDetail = "The folder is a reparse point (junction/symlink). It will be removed without following its target.";
            }
            else
            {
                CountFolder(fullPath, counter, cancellationToken);
            }

            // +1 for the root folder itself.
            int totalItems = counter.Files + counter.Folders + 1;

            return new PathAnalysis
            {
                FullPath = fullPath,
                Exists = true,
                IsDirectory = true,
                PathLength = fullPath.Length,
                FileCount = counter.Files,
                FolderCount = counter.Folders,
                TotalItems = totalItems,
                HasAccessIssues = counter.AccessIssue,
                AccessIssueDetail = counter.AccessIssueDetail
            };
        }

        private static void CountFolder(string folderPath, Counter counter, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var baseFolderPath = PathUtils.EnsureExtendedLengthPrefix(folderPath);
            var searchTerm = Path.Combine(baseFolderPath, "*");

            NativeMethods.WIN32_FIND_DATAW findInfo;
            NativeMethods.FindFileSafeHandle searchHandle = NativeMethods.FindFirstFileW(searchTerm, out findInfo);
            if (searchHandle.IsInvalid)
            {
                // Record the first access problem but keep going with whatever else we can read.
                int error = Marshal.GetLastWin32Error();
                counter.AccessIssue = true;
                counter.AccessIssueDetail ??=
                    $"Could not enumerate '{PathUtils.StripExtendedLengthPrefix(folderPath)}': {new Win32Exception(error).Message}. " +
                    "Deleting may require Bypass ACL or administrator rights.";
                return;
            }

            using (searchHandle)
            {
                do
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var isDirectory = ((uint)findInfo.dwFileAttributes & (uint)NativeMethods.FileAttributes.Directory) == (uint)NativeMethods.FileAttributes.Directory;
                    var isReparse = (findInfo.dwFileAttributes & NativeMethods.FileAttributes.ReparsePoint) != 0;
                    var name = findInfo.cFileName;

                    if (isReparse)
                    {
                        counter.Folders++;
                        continue;
                    }

                    if (isDirectory)
                    {
                        if (string.Compare(name, ".", StringComparison.InvariantCultureIgnoreCase) == 0 ||
                            string.Compare(name, "..", StringComparison.InvariantCultureIgnoreCase) == 0)
                        {
                            continue;
                        }

                        counter.Folders++;
                        CountFolder(Path.Combine(folderPath, name), counter, cancellationToken);
                    }
                    else
                    {
                        counter.Files++;
                    }

                } while (NativeMethods.FindNextFileW(searchHandle, out findInfo));
            }
        }
    }
}
