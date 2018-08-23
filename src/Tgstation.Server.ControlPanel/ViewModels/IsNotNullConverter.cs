using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class IsNotNullConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value != null;

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
	}
}
