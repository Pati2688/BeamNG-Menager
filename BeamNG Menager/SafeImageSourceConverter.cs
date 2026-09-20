using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace BeamNGModManager
{
    internal sealed class SafeImageSourceConverter : IValueConverter
    {
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            if (value is not string path ||
                string.IsNullOrWhiteSpace(path))
            {
                return DependencyProperty.UnsetValue;
            }

            try
            {
                if (!Uri.TryCreate(
                    path,
                    UriKind.Absolute,
                    out Uri? uri))
                {
                    return DependencyProperty.UnsetValue;
                }

                BitmapImage image =
                    new BitmapImage();

                image.BeginInit();
                image.CacheOption =
                    BitmapCacheOption.OnLoad;
                image.UriSource =
                    uri;
                image.EndInit();
                image.Freeze();

                return image;
            }
            catch
            {
                return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
