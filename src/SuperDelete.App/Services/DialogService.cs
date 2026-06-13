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

using Microsoft.Win32;
using System.Windows;

namespace SuperDelete.App.Services
{
    /// <summary>
    /// WPF implementation of <see cref="IDialogService"/>. Uses the native .NET 8 file/folder dialogs
    /// (no WinForms dependency) and a standard message box for the deletion confirmation.
    /// </summary>
    public sealed class DialogService : IDialogService
    {
        public string? BrowseForFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select a file to delete",
                CheckFileExists = true,
                Multiselect = false
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string? BrowseForFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select a folder to delete",
                Multiselect = false
            };

            return dialog.ShowDialog() == true ? dialog.FolderName : null;
        }

        public bool ConfirmDeletion(string title, string message)
        {
            var result = MessageBox.Show(
                message,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            return result == MessageBoxResult.Yes;
        }
    }
}
