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

namespace SuperDelete.Core.Models
{
    /// <summary>
    /// A single progress notification raised while an operation runs. One of these is reported
    /// for every file/folder the engine touches, so consumers can render live feedback.
    /// </summary>
    public sealed class DeletionProgress
    {
        public DeletionProgress(string path, bool isFolder, int filesProcessed, int foldersProcessed, bool isPreview)
        {
            Path = path;
            IsFolder = isFolder;
            FilesProcessed = filesProcessed;
            FoldersProcessed = foldersProcessed;
            IsPreview = isPreview;
        }

        /// <summary>The full path of the item just processed.</summary>
        public string Path { get; }

        /// <summary>True if the item is a folder, false if it is a file.</summary>
        public bool IsFolder { get; }

        /// <summary>Running count of files processed so far.</summary>
        public int FilesProcessed { get; }

        /// <summary>Running count of folders processed so far.</summary>
        public int FoldersProcessed { get; }

        /// <summary>
        /// True when this notification comes from a preview ("dry run"); the item was counted
        /// but not actually deleted.
        /// </summary>
        public bool IsPreview { get; }
    }
}
