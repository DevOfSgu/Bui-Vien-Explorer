using System.Globalization;

namespace TravelSystem.Mobile.Converters;

public class FavoriteIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isFavorite = (bool)value;
        string param = parameter as string ?? "❤️|🤍";
        var variants = param.Split('|');
        
        return isFavorite ? variants[0] : variants[1];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
