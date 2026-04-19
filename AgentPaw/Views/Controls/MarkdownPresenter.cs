using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdBlock = Markdig.Syntax.Block;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdInline = Markdig.Syntax.Inlines.Inline;
using WpfInline = System.Windows.Documents.Inline;

namespace AgentPaw.Views.Controls;

/// <summary>
/// Markdig로 파싱한 MD AST를 WPF 요소 트리로 변환해 표시하는 경량 프리젠터.
/// 채팅 말풍선용으로 문단·헤딩·리스트·인용·수평선·코드블록·테이블·인라인 강조/코드/링크를 지원한다.
/// </summary>
public class MarkdownPresenter : ContentControl
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseEmphasisExtras()
        .UsePipeTables()
        .UseAutoLinks()
        .UseTaskLists()
        .Build();

    // 파싱된 AST를 텍스트 키로 공유한다 — 가상화 재활성화로 동일 본문을 여러 번 파싱하는 비용을 제거한다.
    // WPF 요소 자체는 단일 부모 제약으로 공유 불가, AST만 캐시한다.
    private static readonly ConcurrentDictionary<string, MarkdownDocument> _astCache = new();
    private const int AstCacheCapacity = 200;

    private static MarkdownDocument GetOrParse(string text)
    {
        if (_astCache.TryGetValue(text, out var cached)) return cached;
        var doc = Markdig.Markdown.Parse(text, Pipeline);
        if (_astCache.Count >= AstCacheCapacity)
        {
            // 간단한 LRU 대체 — 임의 항목 하나를 증발시켜 상한을 유지한다. 완전한 LRU는 과설계이다.
            var firstKey = _astCache.Keys.FirstOrDefault();
            if (firstKey != null) _astCache.TryRemove(firstKey, out _);
        }
        _astCache[text] = doc;
        return doc;
    }

    // 인스턴스별 최종 렌더링 입력값 — 동일 파라미터로 Rebuild()가 또 호출되면 즉시 반환한다.
    private string? _lastText;
    private Brush? _lastForeground;
    private double _lastFontSize;

    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.Register(
            nameof(Markdown),
            typeof(string),
            typeof(MarkdownPresenter),
            new PropertyMetadata(string.Empty, OnMarkdownChanged));

    public static readonly DependencyProperty TextForegroundProperty =
        DependencyProperty.Register(
            nameof(TextForeground),
            typeof(Brush),
            typeof(MarkdownPresenter),
            new PropertyMetadata(Brushes.Black, (d, _) => ((MarkdownPresenter)d).Rebuild()));

    public static readonly DependencyProperty BaseFontSizeProperty =
        DependencyProperty.Register(
            nameof(BaseFontSize),
            typeof(double),
            typeof(MarkdownPresenter),
            new PropertyMetadata(13.0, (d, _) => ((MarkdownPresenter)d).Rebuild()));

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public Brush TextForeground
    {
        get => (Brush)GetValue(TextForegroundProperty);
        set => SetValue(TextForegroundProperty, value);
    }

    public double BaseFontSize
    {
        get => (double)GetValue(BaseFontSizeProperty);
        set => SetValue(BaseFontSizeProperty, value);
    }

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MarkdownPresenter)d).Rebuild();

    private void Rebuild()
    {
        var text = Markdown ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            Content = null;
            _lastText = text;
            _lastForeground = TextForeground;
            _lastFontSize = BaseFontSize;
            return;
        }

        // 동일 파라미터로 재호출되면 WPF 트리를 다시 쌓지 않는다 — Foreground/FontSize DP 콜백이 중복 발화해도 무비용
        if (Content != null
            && string.Equals(text, _lastText, StringComparison.Ordinal)
            && ReferenceEquals(_lastForeground, TextForeground)
            && _lastFontSize == BaseFontSize)
        {
            return;
        }

        var panel = new StackPanel { Orientation = Orientation.Vertical };
        try
        {
            var doc = GetOrParse(text);
            foreach (var block in doc)
                RenderBlock(block, panel);
        }
        catch
        {
            panel.Children.Clear();
            panel.Children.Add(MakeTextBlock(text));
        }
        Content = panel;
        _lastText = text;
        _lastForeground = TextForeground;
        _lastFontSize = BaseFontSize;
    }

    private void RenderBlock(MdBlock block, StackPanel host)
    {
        switch (block)
        {
            case HeadingBlock h:
                host.Children.Add(RenderHeading(h));
                break;
            case ParagraphBlock p:
                host.Children.Add(RenderParagraph(p));
                break;
            case ListBlock list:
                host.Children.Add(RenderList(list));
                break;
            case QuoteBlock q:
                host.Children.Add(RenderQuote(q));
                break;
            case FencedCodeBlock fenced:
                host.Children.Add(RenderCodeBlock(fenced));
                break;
            case CodeBlock code:
                host.Children.Add(RenderCodeBlock(code));
                break;
            case ThematicBreakBlock:
                host.Children.Add(new Separator { Margin = new Thickness(0, 6, 0, 6), Opacity = 0.4 });
                break;
            case MdTable table:
                host.Children.Add(RenderTable(table));
                break;
            default:
                // 알 수 없는 블록은 원문 그대로 출력
                var raw = block.ToString();
                if (!string.IsNullOrWhiteSpace(raw))
                    host.Children.Add(MakeTextBlock(raw!));
                break;
        }
    }

    private UIElement RenderHeading(HeadingBlock h)
    {
        var tb = new TextBlock
        {
            FontWeight = FontWeights.Bold,
            Foreground = TextForeground,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, h.Level == 1 ? 6 : 4, 0, 2)
        };
        tb.FontSize = h.Level switch
        {
            1 => BaseFontSize + 6,
            2 => BaseFontSize + 4,
            3 => BaseFontSize + 2,
            _ => BaseFontSize + 1
        };
        if (h.Inline != null) AppendInlines(tb.Inlines, h.Inline);
        return tb;
    }

    private UIElement RenderParagraph(ParagraphBlock p)
    {
        var tb = MakeTextBlock();
        tb.Margin = new Thickness(0, 2, 0, 2);
        if (p.Inline != null) AppendInlines(tb.Inlines, p.Inline);
        return tb;
    }

    private UIElement RenderList(ListBlock list)
    {
        var panel = new StackPanel { Margin = new Thickness(12, 2, 0, 2) };
        int index = list.IsOrdered && int.TryParse(list.OrderedStart, out var start) ? start : 1;
        foreach (var item in list.OfType<ListItemBlock>())
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var bullet = new TextBlock
            {
                Text = list.IsOrdered ? $"{index}." : "•",
                Foreground = TextForeground,
                FontSize = BaseFontSize,
                Margin = new Thickness(0, 2, 6, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(bullet, 0);
            row.Children.Add(bullet);

            var content = new StackPanel();
            Grid.SetColumn(content, 1);
            foreach (var child in item)
                RenderBlock(child, content);
            row.Children.Add(content);

            panel.Children.Add(row);
            index++;
        }
        return panel;
    }

    private UIElement RenderQuote(QuoteBlock q)
    {
        var inner = new StackPanel { Margin = new Thickness(10, 2, 0, 2) };
        foreach (var child in q)
            RenderBlock(child, inner);
        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(4, 0, 0, 0),
            Margin = new Thickness(0, 2, 0, 2),
            Child = inner
        };
    }

    private UIElement RenderCodeBlock(LeafBlock code)
    {
        var lines = code.Lines.Lines
            .Take(code.Lines.Count)
            .Select(l => l.ToString())
            .ToArray();
        var text = string.Join("\n", lines);

        var tb = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New"),
            FontSize = BaseFontSize - 1,
            Foreground = TextForeground,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(10, 8, 10, 8)
        };

        return new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 4, 0, 4),
            Child = tb
        };
    }

    private UIElement RenderTable(MdTable table)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        int colCount = 0;
        foreach (var row in table.OfType<MdTableRow>())
            colCount = Math.Max(colCount, row.Count);
        for (int i = 0; i < colCount; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int r = 0;
        foreach (var row in table.OfType<MdTableRow>())
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            int c = 0;
            foreach (var cell in row.OfType<MdTableCell>())
            {
                var cellPanel = new StackPanel();
                foreach (var block in cell)
                    RenderBlock(block, cellPanel);

                var border = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)),
                    BorderThickness = new Thickness(0.5),
                    Padding = new Thickness(6, 3, 6, 3),
                    Background = row.IsHeader
                        ? new SolidColorBrush(Color.FromArgb(40, 128, 128, 128))
                        : Brushes.Transparent,
                    Child = cellPanel
                };
                Grid.SetRow(border, r);
                Grid.SetColumn(border, c);
                grid.Children.Add(border);
                c++;
            }
            r++;
        }
        return grid;
    }

    private TextBlock MakeTextBlock(string? text = null)
    {
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = TextForeground,
            FontSize = BaseFontSize
        };
        if (!string.IsNullOrEmpty(text)) tb.Text = text;
        return tb;
    }

    private void AppendInlines(InlineCollection target, ContainerInline container)
    {
        foreach (var inline in container)
            AppendInline(target, inline);
    }

    private void AppendInline(InlineCollection target, MdInline inline)
    {
        switch (inline)
        {
            case LiteralInline lit:
                target.Add(new Run(lit.Content.ToString()));
                break;
            case EmphasisInline emph:
            {
                Span span = emph.DelimiterCount >= 2 ? new Bold() : new Italic();
                AppendInlines(span.Inlines, emph);
                target.Add(span);
                break;
            }
            case CodeInline code:
            {
                var run = new Run(code.Content)
                {
                    FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New"),
                    Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128))
                };
                target.Add(run);
                break;
            }
            case LinkInline link:
            {
                var hl = new Hyperlink { NavigateUri = TryCreateUri(link.Url) };
                hl.RequestNavigate += OnHyperlinkNavigate;
                AppendInlines(hl.Inlines, link);
                target.Add(hl);
                break;
            }
            case LineBreakInline br:
                target.Add(br.IsHard ? (WpfInline)new LineBreak() : new Run(" "));
                break;
            case AutolinkInline auto:
            {
                var hl = new Hyperlink(new Run(auto.Url)) { NavigateUri = TryCreateUri(auto.Url) };
                hl.RequestNavigate += OnHyperlinkNavigate;
                target.Add(hl);
                break;
            }
            case HtmlInline html:
                target.Add(new Run(html.Tag));
                break;
            case ContainerInline container:
                AppendInlines(target, container);
                break;
            default:
                var text = inline.ToString();
                if (!string.IsNullOrEmpty(text)) target.Add(new Run(text));
                break;
        }
    }

    private static Uri? TryCreateUri(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null;
    }

    private static void OnHyperlinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            if (e.Uri != null)
                Process.Start(new ProcessStartInfo { FileName = e.Uri.ToString(), UseShellExecute = true });
        }
        catch { /* ignore */ }
        e.Handled = true;
    }
}
