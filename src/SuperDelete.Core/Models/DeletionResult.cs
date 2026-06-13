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

namespace SuperDelete.Core.Models
{
    /// <summary>
    /// The structured result of a completed (or failed) deletion. This is the single value both
    /// the CLI and the UI inspect to decide what to show the user.
    /// </summary>
    public sealed class DeletionResult
    {
        private DeletionResult(
            DeletionStatus status,
            string path,
            int filesProcessed,
            int foldersProcessed,
            TimeSpan elapsed,
            string? errorMessage,
            Exception? exception)
        {
            Status = status;
            Path = path;
            FilesProcessed = filesProcessed;
            FoldersProcessed = foldersProcessed;
            Elapsed = elapsed;
            ErrorMessage = errorMessage;
            Exception = exception;
        }

        public DeletionStatus Status { get; }

        /// <summary>The full path the operation targeted.</summary>
        public string Path { get; }

        /// <summary>Files deleted (or, for a preview, that would be deleted).</summary>
        public int FilesProcessed { get; }

        /// <summary>Folders deleted (or, for a preview, that would be deleted).</summary>
        public int FoldersProcessed { get; }

        public TimeSpan Elapsed { get; }

        /// <summary>A user-friendly error message, present when <see cref="Status"/> is Failed/Partial.</summary>
        public string? ErrorMessage { get; }

        /// <summary>The underlying exception, for diagnostics / "show stack trace".</summary>
        public Exception? Exception { get; }

        public bool IsPreview => Status == DeletionStatus.PreviewCompleted;

        public static DeletionResult Success(string path, int files, int folders, TimeSpan elapsed) =>
            new(DeletionStatus.Success, path, files, folders, elapsed, null, null);

        public static DeletionResult Preview(string path, int files, int folders, TimeSpan elapsed) =>
            new(DeletionStatus.PreviewCompleted, path, files, folders, elapsed, null, null);

        public static DeletionResult Cancelled(string path, int files, int folders, TimeSpan elapsed) =>
            new(DeletionStatus.Cancelled, path, files, folders, elapsed, null, null);

        /// <summary>
        /// Builds a failure result. If anything was already processed the status is reported as
        /// <see cref="DeletionStatus.Partial"/>, otherwise <see cref="DeletionStatus.Failed"/>.
        /// </summary>
        public static DeletionResult Failure(
            string path, int files, int folders, TimeSpan elapsed, string message, Exception exception)
        {
            var status = (files > 0 || folders > 0) ? DeletionStatus.Partial : DeletionStatus.Failed;
            return new DeletionResult(status, path, files, folders, elapsed, message, exception);
        }
    }
}
