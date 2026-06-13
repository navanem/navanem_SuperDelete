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
    /// The outcome of a deletion (or preview) operation.
    /// </summary>
    public enum DeletionStatus
    {
        /// <summary>Everything requested was deleted successfully.</summary>
        Success,

        /// <summary>Some items were deleted before an error stopped the operation.</summary>
        Partial,

        /// <summary>Nothing was deleted; the operation failed up-front.</summary>
        Failed,

        /// <summary>The user cancelled the operation while it was running.</summary>
        Cancelled,

        /// <summary>A preview ("dry run") completed without deleting anything.</summary>
        PreviewCompleted
    }
}
