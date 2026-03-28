using Axphi.ViewModels;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Axphi.Views
{
    public partial class TrackTimelinePropertyView : UserControl
    {
        private Point _lastMousePos;

        public static readonly DependencyProperty KeyframesSourceProperty = DependencyProperty.Register(
            nameof(KeyframesSource),
            typeof(IEnumerable),
            typeof(TrackTimelinePropertyView),
            new PropertyMetadata(null));

        public static readonly DependencyProperty ExpressionSlotProperty = DependencyProperty.Register(
            nameof(ExpressionSlot),
            typeof(TrackExpressionSlot),
            typeof(TrackTimelinePropertyView),
            new PropertyMetadata(null));

        public static readonly DependencyProperty KeyframeToolTipProperty = DependencyProperty.Register(
            nameof(KeyframeToolTip),
            typeof(string),
            typeof(TrackTimelinePropertyView),
            new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty KeyframeFillProperty = DependencyProperty.Register(
            nameof(KeyframeFill),
            typeof(string),
            typeof(TrackTimelinePropertyView),
            new PropertyMetadata("#FFD700"));

        public static readonly DependencyProperty RowBackgroundProperty = DependencyProperty.Register(
            nameof(RowBackground),
            typeof(string),
            typeof(TrackTimelinePropertyView),
            new PropertyMetadata(null));

        public static readonly DependencyProperty RowBorderBrushProperty = DependencyProperty.Register(
            nameof(RowBorderBrush),
            typeof(string),
            typeof(TrackTimelinePropertyView),
            new PropertyMetadata("#333"));

        public static readonly DependencyProperty TimelineWidthProperty = DependencyProperty.Register(
            nameof(TimelineWidth),
            typeof(double),
            typeof(TrackTimelinePropertyView),
            new PropertyMetadata(double.NaN));

        public static readonly DependencyProperty ShowExpressionEditorProperty = DependencyProperty.Register(
            nameof(ShowExpressionEditor),
            typeof(bool),
            typeof(TrackTimelinePropertyView),
            new PropertyMetadata(true));

        public static readonly DependencyProperty EnableRightClickProperty = DependencyProperty.Register(
            nameof(EnableRightClick),
            typeof(bool),
            typeof(TrackTimelinePropertyView),
            new PropertyMetadata(true));

        public static readonly DependencyProperty ExpressionEditorStyleProperty = DependencyProperty.Register(
            nameof(ExpressionEditorStyle),
            typeof(Style),
            typeof(TrackTimelinePropertyView),
            new PropertyMetadata(null));

        public static readonly DependencyProperty ExpressionEditorTextBoxStyleProperty = DependencyProperty.Register(
            nameof(ExpressionEditorTextBoxStyle),
            typeof(Style),
            typeof(TrackTimelinePropertyView),
            new PropertyMetadata(null));

        public TrackTimelinePropertyView()
        {
            InitializeComponent();
        }

        public IEnumerable? KeyframesSource
        {
            get => (IEnumerable?)GetValue(KeyframesSourceProperty);
            set => SetValue(KeyframesSourceProperty, value);
        }

        public TrackExpressionSlot? ExpressionSlot
        {
            get => (TrackExpressionSlot?)GetValue(ExpressionSlotProperty);
            set => SetValue(ExpressionSlotProperty, value);
        }

        public string KeyframeToolTip
        {
            get => (string)GetValue(KeyframeToolTipProperty);
            set => SetValue(KeyframeToolTipProperty, value);
        }

        public string KeyframeFill
        {
            get => (string)GetValue(KeyframeFillProperty);
            set => SetValue(KeyframeFillProperty, value);
        }

        public string? RowBackground
        {
            get => (string?)GetValue(RowBackgroundProperty);
            set => SetValue(RowBackgroundProperty, value);
        }

        public string RowBorderBrush
        {
            get => (string)GetValue(RowBorderBrushProperty);
            set => SetValue(RowBorderBrushProperty, value);
        }

        public double TimelineWidth
        {
            get => (double)GetValue(TimelineWidthProperty);
            set => SetValue(TimelineWidthProperty, value);
        }

        public bool ShowExpressionEditor
        {
            get => (bool)GetValue(ShowExpressionEditorProperty);
            set => SetValue(ShowExpressionEditorProperty, value);
        }

        public bool EnableRightClick
        {
            get => (bool)GetValue(EnableRightClickProperty);
            set => SetValue(EnableRightClickProperty, value);
        }

        public Style? ExpressionEditorStyle
        {
            get => (Style?)GetValue(ExpressionEditorStyleProperty);
            set => SetValue(ExpressionEditorStyleProperty, value);
        }

        public Style? ExpressionEditorTextBoxStyle
        {
            get => (Style?)GetValue(ExpressionEditorTextBoxStyleProperty);
            set => SetValue(ExpressionEditorTextBoxStyleProperty, value);
        }

        private void KeyframeThumb_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _lastMousePos = Mouse.GetPosition(this);
            if (sender is FrameworkElement fe && fe.DataContext != null)
            {
                dynamic wrapper = fe.DataContext;
                wrapper.OnDragStarted();
            }
        }

        private void KeyframeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Point currentPos = Mouse.GetPosition(this);
            double stableDelta = currentPos.X - _lastMousePos.X;
            _lastMousePos = currentPos;

            if (sender is FrameworkElement fe && fe.DataContext != null)
            {
                dynamic wrapper = fe.DataContext;
                wrapper.OnDragDelta(stableDelta);
            }
        }

        private void KeyframeThumb_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext != null)
            {
                dynamic wrapper = fe.DataContext;
                wrapper.OnDragCompleted();
            }
        }

        private void KeyframeThumb_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!EnableRightClick)
            {
                return;
            }

            if (sender is not FrameworkElement fe || fe.DataContext == null)
            {
                return;
            }

            dynamic wrapper = fe.DataContext;
            wrapper.OnRightClick();
            e.Handled = true;
        }
    }
}