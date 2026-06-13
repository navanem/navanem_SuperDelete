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
    /// The result of analysing a path before deletion. Powers the UI "Preview" summary so a user
    /// can understand exactly what they are about to delete.
    /// </summary>
    public sealed class PathAnalysis
    {
        /// <summary>The absolute, long-path-resolved path that was analysed.</summary>
        public required string FullPath { get; init; }

        /// <summary>True if the path currently exists on disk.</summary>
        public bool Exists { get; init; }

        /// <summary>True if the path is a directory, false if it is a single file.</summary>
        public bool IsDirectory { get; init; }

        /// <summary>Character length of the full path.</summary>
        public int PathLength { get; init; }

        /// <summary>True when the path exceeds the classic Windows MAX_PATH (260) limit.</summary>
        public bool ExceedsMaxPath => PathLength > 260;

        /// <summary>Number of files found beneath the path (0 for a single file or empty folder).</summary>
        public int FileCount { get; init; }

        /// <summary>Number of sub-folders found beneath the path.</summary>
        public int FolderCount { get; init; }

        /// <summary>Total items (files + folders) that a deletion would remove, including the root.</summary>
        public int TotalItems { get; init; }

        /// <summary>True if a read-only / reparse-point / access concern was detected during analysis.</summary>
        public bool HasAccessIssues { get; init; }

        /// <summary>Human-readable detail about any detected access issue, if any.</summary>
        public string? AccessIssueDetail { get; init; }
    }
}
