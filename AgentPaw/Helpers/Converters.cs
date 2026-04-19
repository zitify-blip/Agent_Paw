using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace AgentPaw.Helpers;

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}

public class ZeroToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class RoleToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string role && parameter is string target && role == target
            ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class ForcePersonaTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "강제 지정" : "Auto";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BoolToToggleTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "비활성화" : "활성화";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Avatar 경로(로컬 파일 또는 base64 data URI)를 ImageSource로 변환한다.
/// 경로가 비어있거나 파일이 없으면 null을 반환하여 Fallback이 동작하게 한다.
/// 디코딩된 BitmapImage는 Freeze 후 경로를 키로 정적 캐시한다 — 가상화 재사이클·여러 메시지 버블에서
/// 동일 base64/파일을 반복 디코딩하지 않기 위함.
/// </summary>
public class AvatarToImageConverter : IValueConverter
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImageSource> _cache = new();
    private const int CacheCapacity = 100;

    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
            return null;

        if (_cache.TryGetValue(path, out var cached))
            return cached;

        try
        {
            BitmapImage? image = null;

            // base64 data URI
            if (path.StartsWith("data:image/"))
            {
                var base64 = path[(path.IndexOf(",", StringComparison.Ordinal) + 1)..];
                var bytes = System.Convert.FromBase64String(base64);
                image = new BitmapImage();
                using var ms = new MemoryStream(bytes);
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();
            }
            // 로컬 파일 경로
            else if (File.Exists(path))
            {
                image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
            }

            if (image != null)
            {
                if (_cache.Count >= CacheCapacity)
                {
                    var firstKey = _cache.Keys.FirstOrDefault();
                    if (firstKey != null) _cache.TryRemove(firstKey, out _);
                }
                _cache[path] = image;
                return image;
            }
        }
        catch { }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Avatar가 비어있으면 Visible (폴백 아이콘 표시용)
/// </summary>
public class EmptyToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrWhiteSpace(s) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Avatar가 있으면 Visible (이미지 표시용)
/// </summary>
public class NotEmptyToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrWhiteSpace(s) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 페르소나 아이콘 이름(compass, code, book-open, film, bot 등)을 WPF-UI SymbolRegular로 변환한다.
/// 매핑되지 않는 이름은 Bot24 폴백.
/// </summary>
public class IconNameToSymbolConverter : IValueConverter
{
    private static readonly Dictionary<string, SymbolRegular> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["compass"] = SymbolRegular.BookCompass24,
        ["code"] = SymbolRegular.Code24,
        ["film"] = SymbolRegular.Filmstrip24,
        ["book"] = SymbolRegular.Book24,
        ["book-open"] = SymbolRegular.BookOpen24,
        ["book_open"] = SymbolRegular.BookOpen24,
        ["bookopen"] = SymbolRegular.BookOpen24,
        ["bot"] = SymbolRegular.Bot24,
        ["person"] = SymbolRegular.Person24,
        ["people"] = SymbolRegular.PeopleTeam24,
        ["folder"] = SymbolRegular.Folder24,
        ["document"] = SymbolRegular.Document24,
        ["chat"] = SymbolRegular.Chat24,
        ["edit"] = SymbolRegular.Edit24,
        ["settings"] = SymbolRegular.Settings24,
        ["search"] = SymbolRegular.Search24,
        ["brush"] = SymbolRegular.PaintBrush24,
        ["paint"] = SymbolRegular.PaintBrush24,
        ["palette"] = SymbolRegular.Color24,
        ["sparkle"] = SymbolRegular.Sparkle24,
        ["star"] = SymbolRegular.Star24,
        ["flag"] = SymbolRegular.Flag24,
        ["shield"] = SymbolRegular.Shield24,
        ["lightbulb"] = SymbolRegular.Lightbulb24,
        ["camera"] = SymbolRegular.Camera24,
        ["image"] = SymbolRegular.Image24,
        ["video"] = SymbolRegular.Video24,
        ["music"] = SymbolRegular.MusicNote124,
        ["mic"] = SymbolRegular.Mic24,
        ["database"] = SymbolRegular.Database24,
        ["server"] = SymbolRegular.Server24,
        ["cloud"] = SymbolRegular.Cloud24,
        ["globe"] = SymbolRegular.Globe24,
        ["link"] = SymbolRegular.Link24,
        ["tag"] = SymbolRegular.Tag24,
        ["bookmark"] = SymbolRegular.Bookmark24,
        ["heart"] = SymbolRegular.Heart24,
        ["task"] = SymbolRegular.TaskListSquareLtr24,
        ["clipboard"] = SymbolRegular.Clipboard24,
        ["calendar"] = SymbolRegular.Calendar24,
        ["clock"] = SymbolRegular.Clock24,
        ["rocket"] = SymbolRegular.Rocket24,
        ["wand"] = SymbolRegular.Wand24,
        ["beaker"] = SymbolRegular.Beaker24,
        ["puzzle"] = SymbolRegular.PuzzleCube24,
        ["target"] = SymbolRegular.Target24,
        ["leaf"] = SymbolRegular.LeafOne24,
        ["tasklist"] = SymbolRegular.TaskListLtr24
    };

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s) && Map.TryGetValue(s.Trim(), out var sym))
            return sym;
        return SymbolRegular.Bot24;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 페르소나 색상 이름(indigo, blue, amber, red, green 등)을 SolidColorBrush로 변환한다.
/// 매핑되지 않으면 중립 회색.
/// </summary>
public class PersonaColorToBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, Color> Palette = new(StringComparer.OrdinalIgnoreCase)
    {
        ["indigo"] = Color.FromRgb(0x4F, 0x46, 0xE5),
        ["blue"] = Color.FromRgb(0x25, 0x63, 0xEB),
        ["sky"] = Color.FromRgb(0x02, 0x84, 0xC7),
        ["cyan"] = Color.FromRgb(0x08, 0x91, 0xB2),
        ["teal"] = Color.FromRgb(0x0D, 0x94, 0x88),
        ["green"] = Color.FromRgb(0x16, 0xA3, 0x4A),
        ["emerald"] = Color.FromRgb(0x05, 0x96, 0x69),
        ["lime"] = Color.FromRgb(0x65, 0xA3, 0x0D),
        ["yellow"] = Color.FromRgb(0xCA, 0x8A, 0x04),
        ["amber"] = Color.FromRgb(0xD9, 0x77, 0x06),
        ["orange"] = Color.FromRgb(0xEA, 0x58, 0x0C),
        ["red"] = Color.FromRgb(0xDC, 0x26, 0x26),
        ["rose"] = Color.FromRgb(0xE1, 0x1D, 0x48),
        ["pink"] = Color.FromRgb(0xDB, 0x27, 0x77),
        ["fuchsia"] = Color.FromRgb(0xC0, 0x26, 0xD3),
        ["purple"] = Color.FromRgb(0x93, 0x33, 0xEA),
        ["violet"] = Color.FromRgb(0x7C, 0x3A, 0xED),
        ["slate"] = Color.FromRgb(0x47, 0x55, 0x69),
        ["gray"] = Color.FromRgb(0x4B, 0x55, 0x63),
        ["stone"] = Color.FromRgb(0x57, 0x53, 0x4E),
        ["zinc"] = Color.FromRgb(0x52, 0x52, 0x5B)
    };

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var color = value is string s && !string.IsNullOrWhiteSpace(s) && Palette.TryGetValue(s.Trim(), out var c)
            ? c
            : Color.FromRgb(0x6B, 0x72, 0x80);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
