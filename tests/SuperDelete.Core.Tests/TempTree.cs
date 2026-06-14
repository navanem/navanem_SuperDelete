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
using System.IO;
using SuperDelete.Core;

namespace SuperDelete.Core.Tests
{
    /// <summary>
    /// A throwaway temp directory for a test, created with the extended-length API so it can host
    /// paths longer than 260 characters. Disposing best-effort removes whatever survived.
    /// </summary>
    internal sealed class TempTree : IDisposable
    {
        public string Root { get; }

        public TempTree()
        {
            Root = Path.Combine(Path.GetTempPath(), "sd_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Creates a nested directory using the extended-length API (handles &gt;260 chars).</summary>
        public string MakeDir(string relative)
        {
            var full = Path.Combine(Root, relative);
            Directory.CreateDirectory(PathUtils.EnsureExtendedLengthPrefix(full));
            return full;
        }

        /// <summary>Creates a file (long-path safe) with some content.</summary>
        public string MakeFile(string relative, string content = "x")
        {
            var full = Path.Combine(Root, relative);
            var dir = Path.GetDirectoryName(full)!;
            Directory.CreateDirectory(PathUtils.EnsureExtendedLengthPrefix(dir));
            File.WriteAllText(PathUtils.EnsureExtendedLengthPrefix(full), content);
            return full;
        }

        public bool Exists(string fullPath) =>
            File.Exists(PathUtils.EnsureExtendedLengthPrefix(fullPath)) ||
            Directory.Exists(PathUtils.EnsureExtendedLengthPrefix(fullPath));

        public void Dispose()
        {
            try
            {
                var prefixed = PathUtils.EnsureExtendedLengthPrefix(Root);
                if (Directory.Exists(prefixed))
                {
                    // clear read-only attributes then delete, best effort
                    foreach (var f in Directory.EnumerateFiles(prefixed, "*", SearchOption.AllDirectories))
                    {
                        try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                    }
                    Directory.Delete(prefixed, true);
                }
            }
            catch
            {
                // leave it for the OS temp cleaner if we cannot remove it
            }
        }
    }
}
