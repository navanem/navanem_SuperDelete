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
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SuperDelete.App.ViewModels;

namespace SuperDelete.App.Converters
{
    /// <summary>
    /// Maps a <see cref="StatusKind"/> to a themed brush. ConverterParameter selects "foreground"
    /// (default) or "background". Looking the brushes up from the active resources keeps the status
    /// banner correct in both light and dark themes.
    /// </summary>
    public sealed class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var kind = value is StatusKind k ? k : StatusKind.None;
            bool background = string.Equals(parameter as string, "background", StringComparison.OrdinalIgnoreCase);

            string key = (kind, background) switch
            {
                (StatusKind.Success, false) => "SuccessBrush",
                (StatusKind.Success, true) => "SuccessBackgroundBrush",
                (StatusKind.Warning, false) => "WarningBrush",
                (StatusKind.Warning, true) => "WarningBackgroundBrush",
                (StatusKind.Error, false) => "DangerBrush",
                (StatusKind.Error, true) => "ErrorBackgroundBrush",
                (StatusKind.Info, false) => "AccentBrush",
                (StatusKind.Info, true) => "CardBackgroundBrush",
                _ => background ? "CardBackgroundBrush" : "SubtleForegroundBrush"
            };

            return Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
