using Axphi.ViewModels;
using System.Windows;
using System.Windows.Controls;
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