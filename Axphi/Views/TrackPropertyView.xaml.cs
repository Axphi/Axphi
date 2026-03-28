using Axphi.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Axphi.Views
{
    public partial class TrackPropertyView : UserControl
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(TrackPropertyView),
            new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty AddKeyframeCommandProperty = DependencyProperty.Register(
            nameof(AddKeyframeCommand),
            typeof(ICommand),
            typeof(TrackPropertyView),
            new PropertyMetadata(null));

        public static readonly DependencyProperty ExpressionSlotProperty = DependencyProperty.Register(
            nameof(ExpressionSlot),
            typeof(TrackExpressionSlot),
            typeof(TrackPropertyView),
            new PropertyMetadata(null));

        public static readonly DependencyProperty EditorContentProperty = DependencyProperty.Register(
            nameof(EditorContent),
            typeof(UIElement),
            typeof(TrackPropertyView),
            new PropertyMetadata(null));

        public static readonly DependencyProperty KeyframeButtonVisibilityProperty = DependencyProperty.Register(
            nameof(KeyframeButtonVisibility),
            typeof(Visibility),
            typeof(TrackPropertyView),
            new PropertyMetadata(Visibility.Visible));

        public static readonly DependencyProperty ExpressionIndicatorVisibilityProperty = DependencyProperty.Register(
            nameof(ExpressionIndicatorVisibility),
            typeof(Visibility),
            typeof(TrackPropertyView),
            new PropertyMetadata(Visibility.Visible));

        public static readonly DependencyProperty KeyframeColumnWidthProperty = DependencyProperty.Register(
            nameof(KeyframeColumnWidth),
            typeof(GridLength),
            typeof(TrackPropertyView),
            new PropertyMetadata(new GridLength(14)));

        public static readonly DependencyProperty ExpressionColumnWidthProperty = DependencyProperty.Register(
            nameof(ExpressionColumnWidth),
            typeof(GridLength),
            typeof(TrackPropertyView),
            new PropertyMetadata(new GridLength(14)));

        public TrackPropertyView()
        {
            InitializeComponent();
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public ICommand? AddKeyframeCommand
        {
            get => (ICommand?)GetValue(AddKeyframeCommandProperty);
            set => SetValue(AddKeyframeCommandProperty, value);
        }

        public TrackExpressionSlot? ExpressionSlot
        {
            get => (TrackExpressionSlot?)GetValue(ExpressionSlotProperty);
            set => SetValue(ExpressionSlotProperty, value);
        }

        public UIElement? EditorContent
        {
            get => (UIElement?)GetValue(EditorContentProperty);
            set => SetValue(EditorContentProperty, value);
        }

        public Visibility KeyframeButtonVisibility
        {
            get => (Visibility)GetValue(KeyframeButtonVisibilityProperty);
            set => SetValue(KeyframeButtonVisibilityProperty, value);
        }

        public Visibility ExpressionIndicatorVisibility
        {
            get => (Visibility)GetValue(ExpressionIndicatorVisibilityProperty);
            set => SetValue(ExpressionIndicatorVisibilityProperty, value);
        }

        public GridLength KeyframeColumnWidth
        {
            get => (GridLength)GetValue(KeyframeColumnWidthProperty);
            set => SetValue(KeyframeColumnWidthProperty, value);
        }

        public GridLength ExpressionColumnWidth
        {
            get => (GridLength)GetValue(ExpressionColumnWidthProperty);
            set => SetValue(ExpressionColumnWidthProperty, value);
        }

        private void ExpressionIndicator_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                return;
            }

            if (ExpressionSlot != null)
            {
                ExpressionSlot.IsEnabled = !ExpressionSlot.IsEnabled;
                e.Handled = true;
            }
        }
    }
}