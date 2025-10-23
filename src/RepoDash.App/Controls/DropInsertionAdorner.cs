namespace RepoDash.App.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

internal sealed class DropInsertionAdorner : Adorner
{
    private static readonly Pen IndicatorPen;
    private Orientation _orientation = Orientation.Vertical;
    private bool _insertAfter;

    static DropInsertionAdorner()
    {
        var pen = new Pen(Brushes.Black, 2)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        pen.Freeze();
        IndicatorPen = pen;
    }

    public DropInsertionAdorner(UIElement adornedElement)
        : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    public void Update(Orientation orientation, bool insertAfter)
    {
        _orientation = orientation;
        _insertAfter = insertAfter;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var size = AdornedElement.RenderSize;
        if (size.Width <= 0 || size.Height <= 0) return;

        if (_orientation == Orientation.Vertical)
        {
            var y = _insertAfter ? size.Height : 0;
            drawingContext.DrawLine(IndicatorPen, new Point(0, y), new Point(size.Width, y));
        }
        else
        {
            var x = _insertAfter ? size.Width : 0;
            drawingContext.DrawLine(IndicatorPen, new Point(x, 0), new Point(x, size.Height));
        }
    }
}
