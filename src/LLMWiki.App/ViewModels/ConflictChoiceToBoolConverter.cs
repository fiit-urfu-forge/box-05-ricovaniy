using System.Globalization;
using Avalonia.Data.Converters;

namespace LLMWiki.App.ViewModels;

public sealed class ConflictChoiceToBoolConverter : IValueConverter
{
    public static readonly ConflictChoiceToBoolConverter KeepLocal =
        new(ConflictChoice.KeepLocal);

    public static readonly ConflictChoiceToBoolConverter TakeRemote =
        new(ConflictChoice.TakeRemote);

    private readonly ConflictChoice _target;

    private ConflictChoiceToBoolConverter(ConflictChoice target) => _target = target;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ConflictChoice c && c == _target;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? _target : ConflictChoice.Unresolved;
}
