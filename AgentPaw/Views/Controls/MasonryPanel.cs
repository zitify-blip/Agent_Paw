using System.Windows;
using System.Windows.Controls;

namespace AgentPaw.Views.Controls;

/// <summary>
/// Pinterest 형태 2열 이상 메이슨리 레이아웃. 각 아이템은 내용에 맞춰 자연 높이를 가지며
/// 매번 가장 짧은 열에 배치되어 열 길이가 서로 다르게 채워진다.
/// </summary>
public class MasonryPanel : Panel
{
    public static readonly DependencyProperty ColumnsProperty =
        DependencyProperty.Register(nameof(Columns), typeof(int), typeof(MasonryPanel),
            new FrameworkPropertyMetadata(2,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public static readonly DependencyProperty ColumnSpacingProperty =
        DependencyProperty.Register(nameof(ColumnSpacing), typeof(double), typeof(MasonryPanel),
            new FrameworkPropertyMetadata(8.0,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public double ColumnSpacing
    {
        get => (double)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public static readonly DependencyProperty RowSpacingProperty =
        DependencyProperty.Register(nameof(RowSpacing), typeof(double), typeof(MasonryPanel),
            new FrameworkPropertyMetadata(8.0,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    private int ShortestColumn(double[] heights)
    {
        int idx = 0;
        for (int i = 1; i < heights.Length; i++)
            if (heights[i] < heights[idx]) idx = i;
        return idx;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int cols = System.Math.Max(1, Columns);
        double width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        double colWidth = (width - ColumnSpacing * (cols - 1)) / cols;
        if (colWidth <= 0) colWidth = 0;

        var heights = new double[cols];
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(colWidth, double.PositiveInfinity));
            int c = ShortestColumn(heights);
            if (heights[c] > 0) heights[c] += RowSpacing;
            heights[c] += child.DesiredSize.Height;
        }

        double maxH = 0;
        foreach (var h in heights) if (h > maxH) maxH = h;
        return new Size(width, maxH);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int cols = System.Math.Max(1, Columns);
        double colWidth = (finalSize.Width - ColumnSpacing * (cols - 1)) / cols;
        if (colWidth <= 0) colWidth = 0;

        var heights = new double[cols];
        foreach (UIElement child in InternalChildren)
        {
            int c = ShortestColumn(heights);
            double x = c * (colWidth + ColumnSpacing);
            double y = heights[c] > 0 ? heights[c] + RowSpacing : 0;
            child.Arrange(new Rect(x, y, colWidth, child.DesiredSize.Height));
            heights[c] = y + child.DesiredSize.Height;
        }

        double maxH = 0;
        foreach (var h in heights) if (h > maxH) maxH = h;
        return new Size(finalSize.Width, maxH);
    }
}
