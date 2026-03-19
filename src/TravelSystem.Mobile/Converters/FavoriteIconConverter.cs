using System.Globalization;

namespace TravelSystem.Mobile.Converters;

public class FavoriteIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "favourited.svg" : "favourite.svg";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is "favourited.svg";
    }
}
