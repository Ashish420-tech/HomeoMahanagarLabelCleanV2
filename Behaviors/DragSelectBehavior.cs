using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using HomeoMahanagarLabelCleanV2.Models;
using HomeoMahanagarLabelCleanV2.ViewModels;

namespace HomeoMahanagarLabelCleanV2.Behaviors
{
    public static class DragSelectBehavior
    {
        public static readonly DependencyProperty EnableDragProperty = DependencyProperty.RegisterAttached(
            "EnableDrag",
            typeof(bool),
            typeof(DragSelectBehavior),
            new PropertyMetadata(false, OnEnableDragChanged));

        public static void SetEnableDrag(DependencyObject element, bool value)
            => element.SetValue(EnableDragProperty, value);

        public static bool GetEnableDrag(DependencyObject element)
            => (bool)element.GetValue(EnableDragProperty);

        private static void OnEnableDragChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement fe)
            {
                if ((bool)e.NewValue)
                {
                    fe.PreviewMouseLeftButtonDown += Fe_PreviewMouseLeftButtonDown;
                    fe.PreviewMouseLeftButtonUp += Fe_PreviewMouseLeftButtonUp;
                    fe.PreviewMouseMove += Fe_PreviewMouseMove;
                }
                else
                {
                    fe.PreviewMouseLeftButtonDown -= Fe_PreviewMouseLeftButtonDown;
                    fe.PreviewMouseLeftButtonUp -= Fe_PreviewMouseLeftButtonUp;
                    fe.PreviewMouseMove -= Fe_PreviewMouseMove;
                }
            }
        }

        private static FrameworkElement? _dragElement;
        private static Point _startPoint;
        private static Point _origPos;
        private static Canvas _parentCanvas;

        private static void Fe_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement fe)
                {
                    fe.CaptureMouse();
                    _dragElement = fe;
                    _startPoint = e.GetPosition(null);

                    // find parent canvas
                    _parentCanvas = FindAncestorCanvas(fe);

                    // set original position
                    if (fe.DataContext is LabelCanvasItem item)
                    {
                        _origPos = new Point(item.X, item.Y);

                        // set selected item on VM if available
                        var win = Window.GetWindow(fe);
                        if (win?.DataContext is AdminLabelDesignerViewModel vm)
                        {
                            vm.SelectedItem = item;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log(ex);
            }
        }

        private static void Fe_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (_dragElement != null)
                {
                    _dragElement.ReleaseMouseCapture();
                    _dragElement = null;
                    _parentCanvas = null;
                }
            }
            catch (System.Exception ex)
            {
                HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log(ex);
            }
        }

        private static void Fe_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragElement == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            if (!(_dragElement.DataContext is LabelCanvasItem item))
                return;

            Point current;
            if (_parentCanvas != null)
            {
                current = e.GetPosition(_parentCanvas);
            }
            else
            {
                // fallback to window
                var win = Window.GetWindow(_dragElement);
                current = e.GetPosition(win);
            }

            double dx = current.X - _startPoint.X;
            double dy = current.Y - _startPoint.Y;

            item.X = _origPos.X + dx;
            item.Y = _origPos.Y + dy;
        }

        private static Canvas FindAncestorCanvas(DependencyObject start)
        {
            while (start != null)
            {
                if (start is Canvas c)
                    return c;
                start = VisualTreeHelper.GetParent(start);
            }
            return null;
        }
    }
}
