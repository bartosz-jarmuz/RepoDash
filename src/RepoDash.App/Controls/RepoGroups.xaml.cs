using System.Windows.Media.Media3D;

namespace RepoDash.App.Controls;

using RepoDash.App.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

public partial class RepoGroups : UserControl
{
    private Point? _dragStartPoint;
    private FrameworkElement? _dropIndicatorElement;
    private DropInsertionAdorner? _dropIndicator;
    private AdornerLayer? _dropIndicatorLayer;
    private bool _dragOriginIsHeader;

    public RepoGroups()
    {
        InitializeComponent();
    }

    private ItemsControl? ItemsHost => Items;
    private RepoGroupsViewModel? ViewModel => DataContext as RepoGroupsViewModel;

    private void OnGroupPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = null;
        _dragOriginIsHeader = false;
        if (sender is not Border border) return;
        if (!IsHeaderDragHit(border, e.OriginalSource, e.GetPosition(border))) return;
        _dragStartPoint = e.GetPosition(null);
        _dragOriginIsHeader = true;
    }

    private void OnGroupPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (!_dragOriginIsHeader || _dragStartPoint is null) return;

        var position = e.GetPosition(null);
        var start = _dragStartPoint.Value;

        if (Math.Abs(position.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragStartPoint = null;
        _dragOriginIsHeader = false;

        if (sender is Border border && border.DataContext is RepoGroupViewModel group)
        {
            DragDrop.DoDragDrop(border, group, DragDropEffects.Move);
        }
    }

    private void OnItemsPreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(RepoGroupViewModel)))
        {
            e.Effects = DragDropEffects.None;
            HideDropIndicator();
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        var (_, insertAfter, container) = ResolveDropTarget(e);

        if (container is not null)
        {
            ShowDropIndicator(container, insertAfter);
        }
        else
        {
            HideDropIndicator();
        }

        e.Handled = true;
    }

    private async void OnItemsDrop(object sender, DragEventArgs e)
    {
        _dragStartPoint = null;
        _dragOriginIsHeader = false;
        HideDropIndicator();
        e.Handled = true;

        if (!e.Data.GetDataPresent(typeof(RepoGroupViewModel))) return;
        if (ViewModel is null) return;

        if (e.Data.GetData(typeof(RepoGroupViewModel)) is not RepoGroupViewModel sourceGroup) return;

        var (targetGroup, insertAfter, _) = ResolveDropTarget(e);
        var targetKey = targetGroup?.InternalKey;

        await ViewModel.MoveGroupAsync(sourceGroup.InternalKey, targetKey, insertAfter);
    }

    private void OnItemsDragLeave(object sender, DragEventArgs e)
    {
        if (ItemsHost is null) return;
        if (!ItemsHost.IsMouseOver)
        {
            HideDropIndicator();
        }
    }

    private (RepoGroupViewModel? item, bool insertAfter, FrameworkElement? container) ResolveDropTarget(DragEventArgs e)
    {
        if (ItemsHost is null) return (null, true, null);
        if (ItemsHost.Items.Count == 0) return (null, false, null);

        var position = e.GetPosition(ItemsHost);
        FrameworkElement? closestContainer = null;
        RepoGroupViewModel? closestItem = null;
        bool insertAfter = false;
        double closestDistance = double.MaxValue;

        for (int i = 0; i < ItemsHost.Items.Count; i++)
        {
            if (ItemsHost.Items[i] is not RepoGroupViewModel item) continue;
            var container = ItemsHost.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
            if (container is null) continue;

            var bounds = GetItemBounds(container);

            if (bounds.Contains(position))
            {
                var after = ShouldInsertAfter(bounds, position);
                return (item, after, container);
            }

            var distance = DistanceToRect(position, bounds);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestContainer = container;
                closestItem = item;
                insertAfter = ShouldInsertAfter(bounds, position);
            }
        }

        return (closestItem, insertAfter, closestContainer);
    }

    private Rect GetItemBounds(FrameworkElement element)
    {
        if (ItemsHost is null) return Rect.Empty;
        var transform = element.TransformToVisual(ItemsHost);
        var origin = transform.Transform(new Point(0, 0));
        return new Rect(origin, new Size(element.ActualWidth, element.ActualHeight));
    }

    private bool ShouldInsertAfter(Rect bounds, Point position)
    {
        var orientation = GetOrientation();
        return orientation == Orientation.Horizontal
            ? position.X > bounds.Left + bounds.Width / 2
            : position.Y > bounds.Top + bounds.Height / 2;
    }

    private double DistanceToRect(Point point, Rect rect)
    {
        double dx = Math.Max(rect.Left - point.X, Math.Max(0, point.X - rect.Right));
        double dy = Math.Max(rect.Top - point.Y, Math.Max(0, point.Y - rect.Bottom));
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private Orientation GetOrientation()
    {
        if (ItemsHost is null) return Orientation.Horizontal;

        if (ItemsHost.Items.Count > 0)
        {
            if (ItemsHost.ItemContainerGenerator.ContainerFromIndex(0) is FrameworkElement element)
            {
                var parent = VisualTreeHelper.GetParent(element);
                if (parent is WrapPanel wrap)
                {
                    return wrap.Orientation;
                }
            }
        }

        return Orientation.Horizontal;
    }

    private void ShowDropIndicator(FrameworkElement element, bool insertAfter)
    {
        var orientation = GetOrientation();

        if (!ReferenceEquals(_dropIndicatorElement, element))
        {
            HideDropIndicator();
            var layer = AdornerLayer.GetAdornerLayer(element);
            if (layer is null) return;

            _dropIndicatorElement = element;
            _dropIndicator = new DropInsertionAdorner(element);
            _dropIndicatorLayer = layer;
            layer.Add(_dropIndicator);
        }

        _dropIndicator?.Update(orientation, insertAfter);
    }

    private void HideDropIndicator()
    {
        if (_dropIndicator is not null && _dropIndicatorLayer is not null)
        {
            _dropIndicatorLayer.Remove(_dropIndicator);
        }

        _dropIndicator = null;
        _dropIndicatorElement = null;
        _dropIndicatorLayer = null;
    }

    private static bool IsScrollComponent(object? origin) =>
        origin is DependencyObject dep && FindAncestor<ScrollBar>(dep) is not null;

    private bool IsHeaderDragHit(Border container, object? origin, Point containerPosition)
    {
        if (IsScrollComponent(origin)) return false;
        if (origin is DependencyObject dep && FindAncestor<ButtonBase>(dep) is not null)
            return false;

        if (container.Child is not RepoGroup group) return true;
        if (group.FindName("HeaderDragHandle") is not FrameworkElement header || header.ActualHeight <= 0)
            return true;

        try
        {
            var headerTopLeft = header.TranslatePoint(new Point(0, 0), container);
            var headerRect = new Rect(headerTopLeft, new Size(header.ActualWidth, header.ActualHeight));
            if (headerRect.Contains(containerPosition)) return true;

            if (origin is DependencyObject originDep)
            {
                return IsDescendantOf(originDep, header);
            }

            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool IsDescendantOf(DependencyObject node, DependencyObject ancestor)
    {
        var current = node;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor)) return true;
            current = GetVisualOrLogicalParent(current);
        }

        return false;
    }

    private static T? FindAncestor<T>(DependencyObject? origin) where T : DependencyObject
    {
        var current = origin;
        while (current is not null)
        {
            if (current is T match) return match;
            current = GetVisualOrLogicalParent(current);
        }

        return null;
    }

    private static DependencyObject? GetVisualOrLogicalParent(DependencyObject node) =>
        node switch
        {
            Visual or Visual3D => VisualTreeHelper.GetParent(node),
            FrameworkContentElement fce => fce.Parent,
            _ => null
        };
}
