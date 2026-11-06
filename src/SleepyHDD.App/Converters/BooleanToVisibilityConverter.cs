using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace SleepyHDD.App.Converters;

/// <summary>
/// WinUI 3 未提供 bool 到 Visibility 的内置转换器，这里补齐。
/// </summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
