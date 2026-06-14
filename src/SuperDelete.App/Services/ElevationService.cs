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
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace SuperDelete.App.Services
{
    /// <summary>Reports elevation state and can relaunch the app as administrator.</summary>
    public interface IElevationService
    {
        /// <summary>True if the current process is running with administrator rights.</summary>
        bool IsElevated { get; }

        /// <summary>
        /// Attempts to relaunch the app elevated (UAC prompt) and, on success, shuts down the current
        /// instance. Returns false if the user cancelled the UAC prompt or the relaunch failed.
        /// </summary>
        bool RelaunchAsAdmin();
    }

    public sealed class ElevationService : IElevationService
    {
        public bool IsElevated
        {
            get
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public bool RelaunchAsAdmin()
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo(exePath)
                {
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch
            {
                // user declined the UAC prompt, or the launch failed
                return false;
            }

            Application.Current.Shutdown();
            return true;
        }
    }
}
