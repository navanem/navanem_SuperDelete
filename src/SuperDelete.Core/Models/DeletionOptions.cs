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
    /// Options controlling a single deletion request. Immutable once constructed.
    /// </summary>
    public sealed class DeletionOptions
    {
        /// <summary>
        /// When true, the engine first enables backup/restore privileges and deletes using
        /// backup semantics, which bypasses ACL checks. Requires administrative rights.
        /// </summary>
        public bool BypassAcl { get; init; }

        /// <summary>
        /// When true, the engine walks and counts everything that <em>would</em> be deleted
        /// but performs no actual deletion. Used by the "Preview only" mode in the UI.
        /// </summary>
        public bool PreviewOnly { get; init; }

        /// <summary>Convenience instance representing a normal delete with default settings.</summary>
        public static DeletionOptions Default { get; } = new DeletionOptions();
    }
}
