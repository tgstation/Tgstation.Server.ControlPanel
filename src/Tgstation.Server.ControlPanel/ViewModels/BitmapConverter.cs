using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Globalization;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class BitmapConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
				return null;
			var uri = new Uri((string)value, UriKind.RelativeOrAbsolute);
			var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
			var asset = assets.Open(uri);
			return new Bitmap(asset);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
	}
}
