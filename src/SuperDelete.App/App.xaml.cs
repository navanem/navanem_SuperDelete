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
using System.Windows;
using System.Windows.Threading;
using SuperDelete.App.Services;
using SuperDelete.App.ViewModels;
using SuperDelete.Core.Services;

namespace SuperDelete.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Never fail silently: surface an unhandled UI-thread exception to the user.
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            // Compose the object graph by hand (no DI container needed for an app this size).
            var viewModel = new MainViewModel(
                new DeletionService(),
                new PathAnalyzer(),
                new DialogService(),
                new JsonSettingsStore(),
                new ElevationService(),
                ApplyTheme);

            var window = new MainWindow { DataContext = viewModel };
            MainWindow = window;
            window.Show();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                e.Exception.Message,
                "SuperDelete — unexpected error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        /// <summary>
        /// Swaps the active theme dictionary (index 0 of the merged dictionaries) between light and dark.
        /// </summary>
        public void ApplyTheme(bool dark)
        {
            // Use an absolute pack URI: a code-created ResourceDictionary has no base URI, so a
            // relative "Themes/Dark.xaml" cannot be resolved and would throw.
            var source = new Uri(
                dark ? "pack://application:,,,/Themes/Dark.xaml" : "pack://application:,,,/Themes/Light.xaml",
                UriKind.Absolute);
            Resources.MergedDictionaries[0] = new ResourceDictionary { Source = source };
        }
    }
}
