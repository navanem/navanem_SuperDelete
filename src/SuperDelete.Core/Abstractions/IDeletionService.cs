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
using System.Threading;
using System.Threading.Tasks;
using SuperDelete.Core.Models;

namespace SuperDelete.Core.Abstractions
{
    /// <summary>
    /// The shared deletion engine. Implementations delete files/folders with long-path support and
    /// report progress without depending on any particular UI (console, WPF, etc.).
    /// </summary>
    public interface IDeletionService
    {
        /// <summary>
        /// Deletes (or, when <see cref="DeletionOptions.PreviewOnly"/> is set, counts) the given path.
        /// Never throws for an expected failure: the outcome is always described by the returned
        /// <see cref="DeletionResult"/>.
        /// </summary>
        /// <param name="path">File or folder path. May be relative; it is resolved to absolute.</param>
        /// <param name="options">Behavioural options (bypass ACL, preview-only).</param>
        /// <param name="progress">Optional sink for live per-item progress notifications.</param>
        /// <param name="cancellationToken">Allows the UI to cancel a long-running operation.</param>
        DeletionResult Delete(
            string path,
            DeletionOptions options,
            IProgress<DeletionProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Convenience asynchronous wrapper that runs <see cref="Delete"/> on a background thread,
        /// keeping a UI responsive.
        /// </summary>
        Task<DeletionResult> DeleteAsync(
            string path,
            DeletionOptions options,
            IProgress<DeletionProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }
}
