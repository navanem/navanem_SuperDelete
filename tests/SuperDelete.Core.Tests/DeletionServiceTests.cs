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
using System.Threading;
using SuperDelete.Core;
using SuperDelete.Core.Models;
using SuperDelete.Core.Services;
using Xunit;

namespace SuperDelete.Core.Tests
{
    public class DeletionServiceTests
    {
        private readonly DeletionService _service = new DeletionService();

        [Fact]
        public void Deletes_a_single_file()
        {
            using var tree = new TempTree();
            var file = tree.MakeFile("hello.txt");

            var result = _service.Delete(file, DeletionOptions.Default);

            Assert.Equal(DeletionStatus.Success, result.Status);
            Assert.Equal(1, result.FilesProcessed);
            Assert.Equal(0, result.FoldersProcessed);
            Assert.False(tree.Exists(file));
        }

        [Fact]
        public void Deletes_a_nested_tree_with_correct_counts()
        {
            using var tree = new TempTree();
            tree.MakeFile("a.txt");
            tree.MakeFile(Path.Combine("sub", "b.txt"));
            tree.MakeFile(Path.Combine("sub", "deep", "c.txt"));

            var result = _service.Delete(tree.Root, DeletionOptions.Default);

            Assert.Equal(DeletionStatus.Success, result.Status);
            Assert.Equal(3, result.FilesProcessed);           // a, b, c
            Assert.Equal(3, result.FoldersProcessed);          // root, sub, deep
            Assert.False(Directory.Exists(tree.Root));
        }

        [Fact]
        public void Deletes_a_read_only_file()
        {
            using var tree = new TempTree();
            var file = tree.MakeFile("locked.txt");
            File.SetAttributes(PathUtils.EnsureExtendedLengthPrefix(file), FileAttributes.ReadOnly);

            var result = _service.Delete(tree.Root, DeletionOptions.Default);

            Assert.Equal(DeletionStatus.Success, result.Status);
            Assert.False(tree.Exists(file));
        }

        [Fact]
        public void Deletes_a_path_longer_than_260_characters()
        {
            using var tree = new TempTree();
            // Build a path well past MAX_PATH.
            var segment = new string('x', 40);
            var rel = Path.Combine(segment, segment, segment, segment, segment, segment);
            var leaf = tree.MakeFile(Path.Combine(rel, "deepfile.txt"));
            Assert.True(leaf.Length > 260, $"expected a >260 path, got {leaf.Length}");

            var result = _service.Delete(tree.Root, DeletionOptions.Default);

            Assert.Equal(DeletionStatus.Success, result.Status);
            Assert.False(Directory.Exists(PathUtils.EnsureExtendedLengthPrefix(tree.Root)));
        }

        [Fact]
        public void Preview_counts_without_deleting_anything()
        {
            using var tree = new TempTree();
            tree.MakeFile("a.txt");
            tree.MakeFile(Path.Combine("sub", "b.txt"));

            var result = _service.Delete(tree.Root, new DeletionOptions { PreviewOnly = true });

            Assert.Equal(DeletionStatus.PreviewCompleted, result.Status);
            Assert.True(result.IsPreview);
            Assert.Equal(2, result.FilesProcessed);
            Assert.Equal(2, result.FoldersProcessed);          // root + sub
            Assert.True(Directory.Exists(tree.Root));          // nothing was deleted
        }

        [Fact]
        public void Missing_path_returns_failure_not_exception()
        {
            using var tree = new TempTree();
            var missing = Path.Combine(tree.Root, "does-not-exist");

            var result = _service.Delete(missing, DeletionOptions.Default);

            Assert.Equal(DeletionStatus.Failed, result.Status);
            Assert.NotNull(result.Exception);
            Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
        }

        [Fact]
        public void Already_cancelled_token_yields_cancelled_result()
        {
            using var tree = new TempTree();
            tree.MakeFile("a.txt");
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = _service.Delete(tree.Root, DeletionOptions.Default, progress: null, cancellationToken: cts.Token);

            Assert.Equal(DeletionStatus.Cancelled, result.Status);
            Assert.True(Directory.Exists(tree.Root));          // nothing deleted before cancel
        }

        [Fact]
        public void Reports_progress_for_each_item()
        {
            using var tree = new TempTree();
            tree.MakeFile("a.txt");
            tree.MakeFile("b.txt");

            int reports = 0;
            var progress = new SimpleProgress(_ => reports++);

            var result = _service.Delete(tree.Root, DeletionOptions.Default, progress);

            Assert.Equal(DeletionStatus.Success, result.Status);
            Assert.Equal(3, reports);                           // a, b, root
        }

        private sealed class SimpleProgress : System.IProgress<DeletionProgress>
        {
            private readonly System.Action<DeletionProgress> _h;
            public SimpleProgress(System.Action<DeletionProgress> h) => _h = h;
            public void Report(DeletionProgress value) => _h(value);
        }
    }
}
