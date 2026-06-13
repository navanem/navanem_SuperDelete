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

namespace SuperDelete.App.Converters
{
    /// <summary>
    /// Bool → Visibility. Pass ConverterParameter="invert" to collapse when true instead of false.
    /// </summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Treat bools directly, and offer sensible "truthiness" for the common cases the UI binds:
            // a non-zero count, a non-empty string, or a non-null object.
            bool flag = value switch
            {
                bool b => b,
                int i => i > 0,
                string s => !string.IsNullOrEmpty(s),
                null => false,
                _ => true
            };

            if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
            {
                flag = !flag;
            }
            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }
}
