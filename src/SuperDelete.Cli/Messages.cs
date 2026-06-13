//Copyright 2016 Marcel Nita (marcel.nita@gmail.com)
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

namespace SuperDelete.Cli
{
    /// <summary>
    /// Console-facing strings. Replaces the old <c>Resources.resx</c> with plain constants so the CLI
    /// builds with the dotnet CLI on any machine (no resx code-generator required). The text is
    /// unchanged from the original tool.
    /// </summary>
    internal static class Messages
    {
        public const string ConfirmationLine = "Are you sure you want to delete file/folder at path {0}? (Y/n)";
        public const string InvalidSwitchError = "Invalid switch {0}";
        public const string NoFilenamesSpecified = "No file/directory specified";
        public const string TooManyFilenamesError = "Too many filenames specified";
        public const string UsageLine = "Error: {0} Usage: SuperDelete.exe {1} <file or folder path>";
        public const string VersionLine = "SuperDelete version {0}.";
    }
}
