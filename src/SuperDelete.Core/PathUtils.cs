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
using System.Runtime.InteropServices;
using System.Text;
using SuperDelete.Core.Interop;

namespace SuperDelete.Core
{
    /// <summary>
    /// Path helpers shared by the CLI and the UI. The extended-length prefix and the
    /// <see cref="GetFullPath"/> logic are preserved verbatim from the original tool so that
    /// long-path handling behaves identically.
    /// </summary>
    public static class PathUtils
    {
        /// <summary>The Windows extended-length path prefix (<c>\\?\</c>) that unlocks &gt;260 char paths.</summary>
        public const string ExtendedLengthPrefix = "\\\\?\\";

        /// <summary>
        /// Resolves a possibly-relative path to an absolute one, because the long filename Win32 API
        /// does not accept relative paths. Absolute paths are returned untouched.
        /// </summary>
        public static string GetFullPath(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            // check to see if this is an absolute path, if so, we don't need to do anything
            // starts with \\blahblah or is in the form x:\blahblah
            if (path.StartsWith(@"\\") || (path.Length >= 3 && path.Substring(1, 2) == @":\"))
            {
                return path;
            }

            // resolve to absolute path to avoid confusion since long filename API won't accept relative paths
            StringBuilder fullName = new StringBuilder(32768);

            if (NativeMethods.GetFullPathNameW(path, fullName.MaxCapacity, fullName, IntPtr.Zero) == 0)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error,
                    new Win32Exception(error).Message +
                    $". Could not convert relative to absolute path. Try specifying absolute path. {path} ");
            }

            return fullName.ToString();
        }

        /// <summary>
        /// Ensures the extended-length prefix is present so the Unicode Win32 calls accept long paths.
        /// </summary>
        public static string EnsureExtendedLengthPrefix(string fileName)
        {
            if (fileName.StartsWith(ExtendedLengthPrefix))
            {
                return fileName;
            }

            return $"{ExtendedLengthPrefix}{fileName}";
        }

        /// <summary>
        /// Removes the extended-length prefix for display purposes, so users see a familiar path.
        /// </summary>
        public static string StripExtendedLengthPrefix(string fileName)
        {
            return fileName.StartsWith(ExtendedLengthPrefix)
                ? fileName.Substring(ExtendedLengthPrefix.Length)
                : fileName;
        }

        /// <summary>
        /// Shortens a long path for compact display.
        /// See http://blog.codinghorror.com/shortening-long-file-paths/
        /// </summary>
        public static string Shorten(string path)
        {
            if (path.Length > 64)
            {
                return $"{path.Substring(0, 20)}\\...\\{path.Substring(path.Length - 40, 40)}";
            }

            return path;
        }
    }
}
