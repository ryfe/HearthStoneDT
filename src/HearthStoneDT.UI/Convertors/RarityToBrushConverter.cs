using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HearthStoneDT.UI.Converters
{
    public sealed class RarityToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var rarity = value as string;

            return rarity switch
            {
                "COMMON" or "Common" => Brushes.White,                 // 일반
                "RARE" or "Rare" => new SolidColorBrush(Color.FromRgb(86, 156, 214)),   // 파랑
                "EPIC" or "Epic" => new SolidColorBrush(Color.FromRgb(197, 134, 192)),  // 보라
                "LEGENDARY" or "Legendary" => new SolidColorBrush(Color.FromRgb(255, 165, 0)), // 주황
                _ => Brushes.White
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
