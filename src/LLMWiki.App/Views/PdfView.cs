using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Runtime.Versioning;
using PDFtoImage;
using SkiaSharp;
using PdfRenderOptions = PDFtoImage.RenderOptions;

namespace LLMWiki.App.Views;

public sealed class PdfView : ContentControl
{
    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<PdfView, string?>(nameof(Source));

    public static readonly StyledProperty<int> DpiProperty =
        AvaloniaProperty.Register<PdfView, int>(nameof(Dpi), 144);

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public int Dpi
    {
        get => GetValue(DpiProperty);
        set => SetValue(DpiProperty, value);
    }

    static PdfView()
    {
        SourceProperty.Changed.AddClassHandler<PdfView>((view, _) => view.Reload());
        DpiProperty.Changed.AddClassHandler<PdfView>((view, _) => view.Reload());
    }

    private readonly ObservableCollection<Bitmap> _pages = new();

    public PdfView()
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 };
        var items = new ItemsControl
        {
            ItemsSource = _pages,
            ItemTemplate = new FuncDataTemplate<Bitmap>((bmp, _) =>
                new Border
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Background = Brushes.White,
                    Padding = new Thickness(2),
                    Child = new Image
                    {
                        Source = bmp,
                        Stretch = Stretch.Uniform,
                        StretchDirection = StretchDirection.DownOnly,
                    },
                }),
        };
        stack.Children.Add(items);
        Content = new ScrollViewer { Content = stack };
    }

    private void Reload()
    {
        foreach (var p in _pages) p.Dispose();
        _pages.Clear();

        if (string.IsNullOrEmpty(Source)) return;
        if (!File.Exists(Source))
        {
            Content = new TextBlock { Text = "PDF не найден: " + Source };
            return;
        }

        if (!OperatingSystem.IsWindows()
            && !OperatingSystem.IsLinux()
            && !OperatingSystem.IsMacOS())
        {
            Content = new TextBlock { Text = "PDF просмотр недоступен на этой платформе" };
            return;
        }

        try
        {
            var renderOptions = new PdfRenderOptions { Dpi = Dpi };
            using var fs = File.OpenRead(Source);
            foreach (var page in Conversion.ToImages(fs, leaveOpen: false,
                         password: string.Empty, options: renderOptions))
            {
                using (page)
                {
                    using var data = page.Encode(SKEncodedImageFormat.Png, 90);
                    using var ms = new MemoryStream(data.ToArray());
                    _pages.Add(new Bitmap(ms));
                }
            }
        }
        catch (Exception ex)
        {
            Content = new TextBlock
            {
                Text = $"Не удалось открыть PDF: {ex.Message}",
                TextWrapping = TextWrapping.Wrap,
            };
        }
    }
}
