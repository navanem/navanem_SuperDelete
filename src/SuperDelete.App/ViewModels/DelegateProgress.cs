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

namespace SuperDelete.App.ViewModels
{
    /// <summary>
    /// An <see cref="IProgress{T}"/> that invokes its callback synchronously on the reporting thread,
    /// unlike <see cref="Progress{T}"/> which marshals every report onto the captured
    /// SynchronizationContext. This lets the delete engine report at full speed while the UI samples
    /// the latest values on a timer, keeping the window smooth even for very large trees.
    /// </summary>
    public sealed class DelegateProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public DelegateProgress(Action<T> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void Report(T value) => _handler(value);
    }
}
