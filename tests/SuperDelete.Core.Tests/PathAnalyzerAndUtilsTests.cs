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

using System.IO;
using System.Threading.Tasks;
using SuperDelete.Core;
using SuperDelete.Core.Services;
using Xunit;

namespace SuperDelete.Core.Tests
{
    public class PathAnalyzerTests
    {
        private readonly PathAnalyzer _analyzer = new PathAnalyzer();

        [Fact]
        public async Task Analyzes_a_single_file()
        {
            using var tree = new TempTree();
            var file = tree.MakeFile("note.txt");

            var a = await _analyzer.AnalyzeAsync(file);

            Assert.True(a.Exists);
            Assert.False(a.IsDirectory);
            Assert.Equal(1, a.FileCount);
            Assert.Equal(1, a.TotalItems);
        }

        [Fact]
        public async Task Analyzes_a_folder_tree()
        {
            using var tree = new TempTree();
            tree.MakeFile("a.txt");
            tree.MakeFile(Path.Combine("sub", "b.txt"));
            tree.MakeFile(Path.Combine("sub", "deep", "c.txt"));

            var a = await _analyzer.AnalyzeAsync(tree.Root);

            Assert.True(a.Exists);
            Assert.True(a.IsDirectory);
            Assert.Equal(3, a.FileCount);
            Assert.Equal(2, a.FolderCount);                 // sub, deep (not counting root)
            Assert.Equal(6, a.TotalItems);                  // 3 files + 2 subfolders + root
        }

        [Fact]
        public async Task Reports_non_existent_path()
        {
            using var tree = new TempTree();
            var missing = Path.Combine(tree.Root, "nope");

            var a = await _analyzer.AnalyzeAsync(missing);

            Assert.False(a.Exists);
        }

        [Fact]
        public async Task Flags_path_length_over_max_path()
        {
            using var tree = new TempTree();
            var segment = new string('y', 50);
            var leaf = tree.MakeFile(Path.Combine(segment, segment, segment, segment, segment, segment, "f.txt"));

            var a = await _analyzer.AnalyzeAsync(leaf);

            Assert.True(a.Exists);
            Assert.True(a.PathLength > 260);
            Assert.True(a.ExceedsMaxPath);
        }
    }

    public class PathUtilsTests
    {
        [Fact]
        public void EnsureExtendedLengthPrefix_is_idempotent()
        {
            var once = PathUtils.EnsureExtendedLengthPrefix(@"C:\temp\x");
            var twice = PathUtils.EnsureExtendedLengthPrefix(once);

            Assert.StartsWith(@"\\?\", once);
            Assert.Equal(once, twice);
        }

        [Fact]
        public void StripExtendedLengthPrefix_round_trips()
        {
            const string plain = @"C:\temp\x";
            var prefixed = PathUtils.EnsureExtendedLengthPrefix(plain);

            Assert.Equal(plain, PathUtils.StripExtendedLengthPrefix(prefixed));
            Assert.Equal(plain, PathUtils.StripExtendedLengthPrefix(plain));
        }

        [Fact]
        public void GetFullPath_leaves_absolute_paths_untouched()
        {
            Assert.Equal(@"C:\a\b", PathUtils.GetFullPath(@"C:\a\b"));
            Assert.Equal(@"\\server\share", PathUtils.GetFullPath(@"\\server\share"));
        }

        [Fact]
        public void Shorten_collapses_very_long_paths()
        {
            var longPath = @"C:\" + new string('a', 200) + @"\file.txt";
            var shortened = PathUtils.Shorten(longPath);

            Assert.True(shortened.Length < longPath.Length);
            Assert.Contains(@"\...\", shortened);
        }
    }
}
