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

namespace SuperDelete.App.Services
{
    /// <summary>
    /// Abstracts the windowing/dialog interactions the view model needs, keeping the view model free
    /// of direct WPF dialog dependencies (and testable).
    /// </summary>
    public interface IDialogService
    {
        /// <summary>Shows an open-file dialog. Returns the selected path, or null if cancelled.</summary>
        string? BrowseForFile();

        /// <summary>Shows an open-folder dialog. Returns the selected path, or null if cancelled.</summary>
        string? BrowseForFolder();

        /// <summary>Shows a modal, danger-styled confirmation. Returns true if the user confirms.</summary>
        bool ConfirmDeletion(string title, string message);

        /// <summary>Shows the modal About dialog (version, website, license).</summary>
        void ShowAbout();
    }
}
