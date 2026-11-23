using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BtcWidget.Controls;

/// <summary>
/// Lays the widget's lines out in as many columns as fit. The tile width is measured, never configured: the
/// widest line's natural width, so it follows the font size, price lengths and the detail row. While only one
/// tile fits, it fills the width exactly like a plain stack. Once two or more fit, every tile keeps that width
/// and the group stays together on the left or right; all spare room is on the other side.
/// </summary>
public sealed class TilePanel : Panel
{
    /// <summary>A tile may shrink by this much to keep its column count, so a scrollbar or a longer price cannot make the layout flip.</summary>
    private const double StickyBand = 12;
    /// <summary>Spare width needed before the alignment toggle is offered.</summary>
    private const double ToggleSpace = 24;

    public static readonly DependencyProperty AlignRightProperty = DependencyProperty.Register(
        nameof(AlignRight), typeof(bool), typeof(TilePanel),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(TilePanel),
        new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public bool AlignRight { get => (bool)GetValue(AlignRightProperty); set => SetValue(AlignRightProperty, value); }
    public double Gap { get => (double)GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>All lines sit on one row with at least two tiles' width available (from the latest measure).</summary>
    public bool MeasuredSingleRow { get; private set; }
    public bool IsSingleRow { get; private set; }
    /// <summary>There is room beside the tiles for the alignment toggle.</summary>
    public bool HasSpareRoom { get; private set; }
    public int Columns { get; private set; } = 1;
    public double TileWidth { get; private set; }

    /// <summary>Raised after an arrange that changed the single-row or spare-room state.</summary>
    public event Action<TilePanel>? Reflowed;

    private readonly record struct Plan(int Fit, int Columns, int Rows, double Width);

    private double _natural;
    private int _committedFit = 1;
    private bool _refreshQueued;

    /// <summary>
    /// Re-measure the tile width once layout has settled. Shared-size columns only agree after a layout pass,
    /// and a tile held to a fixed width cannot report that its content now wants more, so anything that can
    /// change a line's natural width (a new price, the font size) calls this.
    /// </summary>
    public void Refresh()
    {
        if (_refreshQueued) return;
        _refreshQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _refreshQueued = false;
            InvalidateMeasure();
        });
    }

    private Plan MakePlan(double available, int count)
    {
        var gap = Gap;
        if (double.IsInfinity(available)) available = _natural;
        var fit = _natural > 0 ? (int)Math.Floor((available + gap) / (_natural + gap)) : 1;
        if (_committedFit > fit && _committedFit >= 2
            && available + StickyBand >= _committedFit * _natural + (_committedFit - 1) * gap)
            fit = _committedFit;

        if (fit <= 1) return new Plan(1, 1, count, available);
        var columns = Math.Min(fit, count);
        var width = Math.Min(_natural, Math.Floor((available - (columns - 1) * gap) / columns));
        return new Plan(fit, columns, (int)Math.Ceiling(count / (double)columns), width);
    }

    protected override Size MeasureOverride(Size available)
    {
        var children = InternalChildren;
        var count = children.Count;
        if (count == 0) { MeasuredSingleRow = false; return new Size(0, 0); }

        var natural = 0.0;
        foreach (UIElement child in children)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            natural = Math.Max(natural, child.DesiredSize.Width);
        }
        natural = Math.Ceiling(natural);
        if (Math.Abs(natural - _natural) >= 1) Refresh();   // settles: the next pass sees the agreed column widths
        _natural = natural;

        var plan = MakePlan(available.Width, count);
        MeasuredSingleRow = plan.Rows == 1 && plan.Fit >= 2;

        var height = 0.0;
        var rowHeight = 0.0;
        for (var i = 0; i < count; i++)
        {
            children[i].Measure(new Size(plan.Width, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, children[i].DesiredSize.Height);
            if ((i + 1) % plan.Columns == 0 || i == count - 1) { height += rowHeight; rowHeight = 0; }
        }
        var width = double.IsInfinity(available.Width)
            ? plan.Columns * plan.Width + (plan.Columns - 1) * Gap
            : available.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = InternalChildren;
        var count = children.Count;
        if (count == 0) { Publish(false, false, 1, 0); return finalSize; }

        var plan = MakePlan(finalSize.Width, count);
        _committedFit = plan.Fit;
        var gap = Gap;
        var step = plan.Width + gap;
        var y = 0.0;
        for (var row = 0; row < plan.Rows; row++)
        {
            var first = row * plan.Columns;
            var inRow = Math.Min(plan.Columns, count - first);
            var rowHeight = 0.0;
            for (var i = 0; i < inRow; i++) rowHeight = Math.Max(rowHeight, children[first + i].DesiredSize.Height);
            // Right-aligned, a short last row hugs the right edge too, staying on the same column lines.
            var x0 = AlignRight && plan.Fit >= 2 ? finalSize.Width - (inRow * step - gap) : 0;
            for (var i = 0; i < inRow; i++)
                children[first + i].Arrange(new Rect(x0 + i * step, y, plan.Width, rowHeight));
            y += rowHeight;
        }

        var lastRow = count % plan.Columns == 0 ? plan.Columns : count % plan.Columns;
        var spare = finalSize.Width - (lastRow * step - gap);
        Publish(plan.Rows == 1 && plan.Fit >= 2, plan.Fit >= 2 && spare >= ToggleSpace, plan.Columns, plan.Width);
        return finalSize;
    }

    private void Publish(bool singleRow, bool spareRoom, int columns, double width)
    {
        Columns = columns;
        TileWidth = width;
        if (singleRow == IsSingleRow && spareRoom == HasSpareRoom) return;
        IsSingleRow = singleRow;
        HasSpareRoom = spareRoom;
        Reflowed?.Invoke(this);
    }
}

/// <summary>
/// Limits how much width its content may claim when a tile's natural width is being measured, then gets out of
/// the way. A long name therefore cannot force wider tiles; it uses the room there is and trims.
/// </summary>
public sealed class NaturalWidthCap : System.Windows.Controls.Decorator
{
    public static readonly DependencyProperty CapProperty = DependencyProperty.Register(
        nameof(Cap), typeof(double), typeof(NaturalWidthCap),
        new FrameworkPropertyMetadata(double.PositiveInfinity, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double Cap { get => (double)GetValue(CapProperty); set => SetValue(CapProperty, value); }

    protected override Size MeasureOverride(Size constraint)
    {
        var size = base.MeasureOverride(constraint);
        return double.IsInfinity(constraint.Width) ? new Size(Math.Min(size.Width, Cap), size.Height) : size;
    }
}
