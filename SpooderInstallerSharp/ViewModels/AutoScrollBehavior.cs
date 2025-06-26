using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System.Collections.Specialized;

namespace SpooderInstallerSharp.Behaviors
{
    public class AutoScrollBehavior : Behavior<ScrollViewer>
    {
        private StackPanel _stackPanel;

        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject != null)
            {
                AssociatedObject.Loaded += OnScrollViewerLoaded;
                AssociatedObject.PropertyChanged += OnScrollViewerContentChanged;
            }
        }

        protected override void OnDetaching()
        {
            DetachFromStackPanel();
            if (AssociatedObject != null)
            {
                AssociatedObject.Loaded -= OnScrollViewerLoaded;
                AssociatedObject.PropertyChanged -= OnScrollViewerContentChanged;
            }
            base.OnDetaching();
        }

        private void OnScrollViewerLoaded(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            AttachToStackPanel();
        }

        private void AttachToStackPanel()
        {
            if (AssociatedObject?.Content is StackPanel stackPanel)
            {
                _stackPanel = stackPanel;
                _stackPanel.Children.CollectionChanged += OnStackPanelChildrenChanged;
            }
        }

        private void DetachFromStackPanel()
        {
            if (_stackPanel != null)
            {
                _stackPanel.Children.CollectionChanged -= OnStackPanelChildrenChanged;
                _stackPanel = null;
            }
        }

        private void OnScrollViewerContentChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ScrollViewer.ContentProperty)
            {
                DetachFromStackPanel();
                AttachToStackPanel();
            }
        }

        private void OnStackPanelChildrenChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (AssociatedObject != null && e.Action == NotifyCollectionChangedAction.Add)
            {
                // Use Dispatcher to ensure UI updates are complete before scrolling
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AssociatedObject.ScrollToEnd();
                }, Avalonia.Threading.DispatcherPriority.Background);
            }
        }
    }
}