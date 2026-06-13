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
using SuperDelete.Core;
using SuperDelete.Core.Models;
using SuperDelete.Core.Services;

namespace SuperDelete.Cli
{
    internal class Program
    {
        public static int Main(string[] args)
        {
            ParsedCmdLineArgs parsedArgs;
            try
            {
                parsedArgs = CmdLineArgsParser.Parse(args);
            }
            catch (CmdLineArgsParser.InvalidCmdLineException e)
            {
                CmdLineArgsParser.PrintUsage(e);
                return 1;
            }

            string filename;
            try
            {
                // Resolve the full path up front so the confirmation prompt shows exactly what will go.
                filename = PathUtils.GetFullPath(parsedArgs.FileName!);
            }
            catch (Exception e)
            {
                Console.WriteLine();
                PrintError(e, parsedArgs.PrintStackTrace);
                return 1;
            }

            // If silent mode is not specified, ask for confirmation (unchanged behaviour).
            if (!parsedArgs.SilentModeEnabled)
            {
                Console.WriteLine(Messages.ConfirmationLine, filename);
                var keyInfo = Console.ReadKey();
                if (keyInfo.Key != ConsoleKey.Y && keyInfo.Key != ConsoleKey.Enter)
                {
                    return 0;
                }
                Console.WriteLine();
            }

            var options = new DeletionOptions { BypassAcl = parsedArgs.BypassAcl };
            var progress = new ConsoleProgressReporter();
            var service = new DeletionService();

            DeletionResult result = service.Delete(filename, options, progress);

            // Render the same closing summary the original ProgressTracker produced.
            Console.WriteLine(
                $"\rDone. Deleted {result.FilesProcessed} files and {result.FoldersProcessed} folders in {result.Elapsed}.\t\t\t\t");

            if (result.Status == DeletionStatus.Failed || result.Status == DeletionStatus.Partial)
            {
                PrintError(result.Exception!, parsedArgs.PrintStackTrace);
                return result.Status == DeletionStatus.Partial ? 2 : 1;
            }

            return 0;
        }

        private static void PrintError(Exception e, bool printStackTrace)
        {
            if (printStackTrace)
            {
                Console.WriteLine($"Error: {e}");
            }
            else
            {
                Console.WriteLine($"Error: {e.Message}");
            }
        }

        /// <summary>
        /// Renders live progress to the console, reproducing the original on-screen experience
        /// ("Deleting &lt;shortened path&gt;") that the old singleton ProgressTracker provided.
        /// </summary>
        private sealed class ConsoleProgressReporter : IProgress<DeletionProgress>
        {
            public void Report(DeletionProgress value)
            {
                Console.Write($"\rDeleting {PathUtils.Shorten(value.Path)}\t\t\t\t");
            }
        }
    }
}
