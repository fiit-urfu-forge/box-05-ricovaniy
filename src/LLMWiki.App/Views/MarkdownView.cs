using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LLMWiki.App.Views;

public sealed class MarkdownView : ContentControl
{
    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Source));

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    static MarkdownView()
    {
        SourceProperty.Changed.AddClassHandler<MarkdownView>((view, _) => view.Render());
    }

    private void Render()
    {
        var text = Source ?? string.Empty;
        var doc = Markdown.Parse(text, Pipeline);
        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 };
        foreach (var block in doc) stack.Children.Add(BuildBlock(block));
        Content = new ScrollViewer { Content = stack };
    }

    private static Control BuildBlock(Block block)
    {
        return block switch
        {
            HeadingBlock h => new TextBlock
            {
                Text = ExtractText(h.Inline),
                FontWeight = FontWeight.Bold,
                FontSize = 22 - Math.Min(h.Level - 1, 4) * 2,
                Margin = new Thickness(0, 8, 0, 4),
                TextWrapping = TextWrapping.Wrap,
            },
            ParagraphBlock p => new TextBlock
            {
                Text = ExtractText(p.Inline),
                TextWrapping = TextWrapping.Wrap,
            },
            CodeBlock c => new Border
            {
                Background = Brushes.LightGray,
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(4),
                Child = new SelectableTextBlock
                {
                    Text = c is FencedCodeBlock f ? string.Join('\n', f.Lines.Lines) : c.ToString() ?? string.Empty,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
                    TextWrapping = TextWrapping.NoWrap,
                },
            },
            ThematicBreakBlock => new Border
            {
                Height = 1,
                Background = Brushes.Gray,
                Margin = new Thickness(0, 8, 0, 8),
            },
            ListBlock list => BuildList(list),
            QuoteBlock q => new Border
            {
                BorderBrush = Brushes.SlateGray,
                BorderThickness = new Thickness(3, 0, 0, 0),
                Padding = new Thickness(8, 0, 0, 0),
                Child = new StackPanel
                {
                    Children = { new TextBlock { Text = string.Join('\n', q.OfType<ParagraphBlock>().Select(pb => ExtractText(pb.Inline))), TextWrapping = TextWrapping.Wrap } },
                },
            },
            _ => new TextBlock
            {
                Text = block.ToString() ?? string.Empty,
                TextWrapping = TextWrapping.Wrap,
            },
        };
    }

    private static Control BuildList(ListBlock list)
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
        var i = 0;
        foreach (var child in list)
        {
            i++;
            if (child is not ListItemBlock item) continue;
            var text = string.Join('\n', item.OfType<ParagraphBlock>().Select(p => ExtractText(p.Inline)));
            var bullet = list.IsOrdered ? $"{i}." : "•";
            stack.Children.Add(new TextBlock
            {
                Text = $"{bullet} {text}",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8, 0, 0, 0),
            });
        }
        return stack;
    }

    private static string ExtractText(ContainerInline? container)
    {
        if (container is null) return string.Empty;
        var parts = new List<string>();
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline lit:
                    parts.Add(lit.Content.ToString());
                    break;
                case CodeInline code:
                    parts.Add(code.Content);
                    break;
                case LinkInline link when link.Url is not null:
                    parts.Add($"{ExtractText(link)} ({link.Url})");
                    break;
                case ContainerInline c:
                    parts.Add(ExtractText(c));
                    break;
                case LineBreakInline:
                    parts.Add("\n");
                    break;
            }
        }
        return string.Concat(parts);
    }
}
