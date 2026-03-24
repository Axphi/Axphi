using Axphi.Data;
using Axphi.Utilities;
using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Axphi.Components
{
    public class JudgementLineEditorCanvas : FrameworkElement
    {
        private readonly record struct NoteRenderState(
            NoteKind Kind,
            Vector Offset,
            Vector Scale,
            double Rotation,
            double Opacity);

        private static readonly Brush CenterLineBrush = new SolidColorBrush(Color.FromRgb(255, 244, 163));
        private static readonly Brush MajorGridBrush = new SolidColorBrush(Color.FromArgb(132, 122, 186, 255));
        private static readonly Brush MinorGridBrush = new SolidColorBrush(Color.FromArgb(62, 112, 146, 214));
        private static readonly Brush VerticalMajorGridBrush = new SolidColorBrush(Color.FromArgb(110, 226, 236, 255));
        private static readonly Brush VerticalGridBrush = new SolidColorBrush(Color.FromArgb(76, 198, 210, 228));
        private static readonly Brush LabelBrush = new SolidColorBrush(Color.FromArgb(236, 240, 245, 255));
        private static readonly Brush LabelBackgroundBrush = new SolidColorBrush(Color.FromArgb(158, 8, 16, 28));
        private static readonly Brush WaveBrush = new SolidColorBrush(Color.FromArgb(70, 64, 214, 255));
        private static readonly Brush PreviewOverlayBrush = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255));
        private static readonly Brush HoldTailHandleBrush = new SolidColorBrush(Color.FromArgb(220, 244, 246, 255));
        private static readonly Pen CenterPen = new Pen(CenterLineBrush, 2.0);
        private static readonly Pen MajorGridPen = new Pen(MajorGridBrush, 1.4);
        private static readonly Pen MinorGridPen = new Pen(MinorGridBrush, 1.0);
        private static readonly Pen VerticalMajorGridPen = new Pen(VerticalMajorGridBrush, 1.2);
        private static readonly Pen VerticalGridPen = new Pen(VerticalGridBrush, 1.0);
        private static readonly Pen SelectedNotePen = new Pen(Brushes.White, 1.4);
        private static readonly Brush HoverPreviewPenBrush = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255));
        private static readonly Pen HoverPreviewPen = new Pen(HoverPreviewPenBrush, 1.0);
        private static readonly Pen HoverTimelineStrongPen = new Pen(new SolidColorBrush(Color.FromArgb(246, 255, 237, 166)), 2.0);
        private static readonly Pen HoverTimelineMediumPen = new Pen(new SolidColorBrush(Color.FromArgb(226, 218, 236, 255)), 1.7);
        private static readonly Pen HoverTimelineWeakPen = new Pen(new SolidColorBrush(Color.FromArgb(188, 179, 212, 244)), 1.35);
        private static readonly Pen HoverTimelineFinePen = new Pen(new SolidColorBrush(Color.FromArgb(152, 137, 170, 208)), 1.1);
        private static readonly Brush HoverTimelineStrongLabelBrush = new SolidColorBrush(Color.FromArgb(255, 255, 247, 210));
        private static readonly Brush HoverTimelineMediumLabelBrush = new SolidColorBrush(Color.FromArgb(255, 234, 243, 255));
        private static readonly Brush HoverTimelineWeakLabelBrush = new SolidColorBrush(Color.FromArgb(255, 210, 226, 246));
        private static readonly Brush HoverTimelineFineLabelBrush = new SolidColorBrush(Color.FromArgb(248, 194, 210, 230));
        private static readonly Brush HoverTimelineStrongLabelBackgroundBrush = new SolidColorBrush(Color.FromArgb(202, 84, 66, 16));
        private static readonly Brush HoverTimelineMediumLabelBackgroundBrush = new SolidColorBrush(Color.FromArgb(188, 26, 56, 94));
        private static readonly Brush HoverTimelineWeakLabelBackgroundBrush = new SolidColorBrush(Color.FromArgb(174, 20, 44, 78));
        private static readonly Brush HoverTimelineFineLabelBackgroundBrush = new SolidColorBrush(Color.FromArgb(160, 18, 34, 62));
        private static readonly Pen SelectionRectPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 120, 215)), 1.0);
        private static readonly Brush SelectionRectBrush = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215));
        private static readonly Pen HoldTailHandlePen = new Pen(new SolidColorBrush(Color.FromArgb(220, 137, 186, 255)), 1.2);
        private static readonly Typeface LabelTypeface = new Typeface("Consolas");
        private static readonly BitmapImage TapImage = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/tap.png"));
        private static readonly BitmapImage TapMultiHitImage = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/tapHL.png"));
        private static readonly BitmapImage DragImage = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/drag.png"));
        private static readonly BitmapImage DragMultiHitImage = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/dragHL.png"));
        private static readonly BitmapImage HoldImage = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/hold.png"));
        private static readonly BitmapImage HoldMultiHitImage = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/holdHL.png"));
        private static readonly BitmapImage FlickImage = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/flick.png"));
        private static readonly BitmapImage FlickMultiHitImage = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/flickHL.png"));
        private static readonly ImageBrush HoldTailBrush;
        private static readonly ImageBrush HoldBodyBrush;
        private static readonly ImageBrush HoldHeadBrush;
        private static readonly ImageBrush HoldTailMultiHitBrush;
        private static readonly ImageBrush HoldBodyMultiHitBrush;
        private static readonly ImageBrush HoldHeadMultiHitBrush;
        private const double BaseNoteTextureWidthPixels = 989.0;
        private const double HoldSegmentOverlapPixels = 1.0;
        private const double HoldHeadPixels = 50.0;
        private const double HoldTailPixels = 50.0;
        private const double HoldHighlightHeadPixels = 100.0;
        private const double HoldHighlightTailPixels = 100.0;
        private bool _isMessengerRegistered;

        private readonly record struct BeatGuideVisualStyle(Pen GuidePen, Brush LabelBrush, Brush LabelBackgroundBrush);

        public enum HitTargetKind
        {
            None,
            NoteBody,
            HoldTailHandle,
        }

        static JudgementLineEditorCanvas()
        {
            double holdHeight = Math.Max(1.0, HoldImage.PixelHeight);
            double holdBodyPixels = Math.Max(1.0, holdHeight - HoldHeadPixels - HoldTailPixels);
            HoldHeadBrush = new ImageBrush(HoldImage)
            {
                Viewbox = new Rect(0, (holdHeight - HoldHeadPixels) / holdHeight, 1, HoldHeadPixels / holdHeight),
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox
            };
            HoldBodyBrush = new ImageBrush(HoldImage)
            {
                Viewbox = new Rect(0, HoldHeadPixels / holdHeight, 1, holdBodyPixels / holdHeight),
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox
            };
            HoldTailBrush = new ImageBrush(HoldImage)
            {
                Viewbox = new Rect(0, 0, 1, HoldTailPixels / holdHeight),
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox
            };
            double multiHitHeight = Math.Max(1.0, HoldMultiHitImage.PixelHeight);
            double multiHitBodyPixels = Math.Max(1.0, multiHitHeight - HoldHighlightHeadPixels - HoldHighlightTailPixels);
            HoldHeadMultiHitBrush = new ImageBrush(HoldMultiHitImage)
            {
                Viewbox = new Rect(0, (multiHitHeight - HoldHighlightHeadPixels) / multiHitHeight, 1, HoldHighlightHeadPixels / multiHitHeight),
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox
            };
            HoldBodyMultiHitBrush = new ImageBrush(HoldMultiHitImage)
            {
                Viewbox = new Rect(0, HoldHighlightHeadPixels / multiHitHeight, 1, multiHitBodyPixels / multiHitHeight),
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox
            };
            HoldTailMultiHitBrush = new ImageBrush(HoldMultiHitImage)
            {
                Viewbox = new Rect(0, 0, 1, HoldHighlightTailPixels / multiHitHeight),
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox
            };

            if (CenterLineBrush.CanFreeze) CenterLineBrush.Freeze();
            if (MajorGridBrush.CanFreeze) MajorGridBrush.Freeze();
            if (MinorGridBrush.CanFreeze) MinorGridBrush.Freeze();
            if (VerticalMajorGridBrush.CanFreeze) VerticalMajorGridBrush.Freeze();
            if (VerticalGridBrush.CanFreeze) VerticalGridBrush.Freeze();
            if (LabelBrush.CanFreeze) LabelBrush.Freeze();
            if (LabelBackgroundBrush.CanFreeze) LabelBackgroundBrush.Freeze();
            if (WaveBrush.CanFreeze) WaveBrush.Freeze();
            if (PreviewOverlayBrush.CanFreeze) PreviewOverlayBrush.Freeze();
            if (HoldTailHandleBrush.CanFreeze) HoldTailHandleBrush.Freeze();
            if (HoverTimelineStrongPen.Brush.CanFreeze) ((SolidColorBrush)HoverTimelineStrongPen.Brush).Freeze();
            if (HoverTimelineStrongPen.CanFreeze) HoverTimelineStrongPen.Freeze();
            if (HoverTimelineMediumPen.Brush.CanFreeze) ((SolidColorBrush)HoverTimelineMediumPen.Brush).Freeze();
            if (HoverTimelineMediumPen.CanFreeze) HoverTimelineMediumPen.Freeze();
            if (HoverTimelineWeakPen.Brush.CanFreeze) ((SolidColorBrush)HoverTimelineWeakPen.Brush).Freeze();
            if (HoverTimelineWeakPen.CanFreeze) HoverTimelineWeakPen.Freeze();
            if (HoverTimelineFinePen.Brush.CanFreeze) ((SolidColorBrush)HoverTimelineFinePen.Brush).Freeze();
            if (HoverTimelineFinePen.CanFreeze) HoverTimelineFinePen.Freeze();
            if (HoverTimelineStrongLabelBrush.CanFreeze) HoverTimelineStrongLabelBrush.Freeze();
            if (HoverTimelineMediumLabelBrush.CanFreeze) HoverTimelineMediumLabelBrush.Freeze();
            if (HoverTimelineWeakLabelBrush.CanFreeze) HoverTimelineWeakLabelBrush.Freeze();
            if (HoverTimelineFineLabelBrush.CanFreeze) HoverTimelineFineLabelBrush.Freeze();
            if (HoverTimelineStrongLabelBackgroundBrush.CanFreeze) HoverTimelineStrongLabelBackgroundBrush.Freeze();
            if (HoverTimelineMediumLabelBackgroundBrush.CanFreeze) HoverTimelineMediumLabelBackgroundBrush.Freeze();
            if (HoverTimelineWeakLabelBackgroundBrush.CanFreeze) HoverTimelineWeakLabelBackgroundBrush.Freeze();
            if (HoverTimelineFineLabelBackgroundBrush.CanFreeze) HoverTimelineFineLabelBackgroundBrush.Freeze();
            if (CenterPen.CanFreeze) CenterPen.Freeze();
            if (MajorGridPen.CanFreeze) MajorGridPen.Freeze();
            if (MinorGridPen.CanFreeze) MinorGridPen.Freeze();
            if (VerticalMajorGridPen.CanFreeze) VerticalMajorGridPen.Freeze();
            if (VerticalGridPen.CanFreeze) VerticalGridPen.Freeze();
            if (SelectedNotePen.CanFreeze) SelectedNotePen.Freeze();
            if (HoverPreviewPen.CanFreeze) HoverPreviewPen.Freeze();
            if (HoldTailHandlePen.Brush.CanFreeze) ((SolidColorBrush)HoldTailHandlePen.Brush).Freeze();
            if (HoldTailHandlePen.CanFreeze) HoldTailHandlePen.Freeze();
            if (TapImage.CanFreeze) TapImage.Freeze();
            if (TapMultiHitImage.CanFreeze) TapMultiHitImage.Freeze();
            if (DragImage.CanFreeze) DragImage.Freeze();
            if (DragMultiHitImage.CanFreeze) DragMultiHitImage.Freeze();
            if (HoldImage.CanFreeze) HoldImage.Freeze();
            if (HoldMultiHitImage.CanFreeze) HoldMultiHitImage.Freeze();
            if (FlickImage.CanFreeze) FlickImage.Freeze();
            if (FlickMultiHitImage.CanFreeze) FlickMultiHitImage.Freeze();
            if (HoldTailBrush.CanFreeze) HoldTailBrush.Freeze();
            if (HoldBodyBrush.CanFreeze) HoldBodyBrush.Freeze();
            if (HoldHeadBrush.CanFreeze) HoldHeadBrush.Freeze();
            if (HoldTailMultiHitBrush.CanFreeze) HoldTailMultiHitBrush.Freeze();
            if (HoldBodyMultiHitBrush.CanFreeze) HoldBodyMultiHitBrush.Freeze();
            if (HoldHeadMultiHitBrush.CanFreeze) HoldHeadMultiHitBrush.Freeze();
        }

        public Rect SelectionRect
        {
            get => (Rect)GetValue(SelectionRectProperty);
            set => SetValue(SelectionRectProperty, value);
        }

        public static readonly DependencyProperty SelectionRectProperty =
            DependencyProperty.Register("SelectionRect", typeof(Rect), typeof(JudgementLineEditorCanvas), new FrameworkPropertyMetadata(Rect.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        public JudgementLineEditorCanvas()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_isMessengerRegistered)
            {
                return;
            }

            WeakReferenceMessenger.Default.Register<JudgementLineEditorCanvas, JudgementLinesChangedMessage>(this, static (recipient, _) => recipient.InvalidateVisual());
            _isMessengerRegistered = true;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
            _isMessengerRegistered = false;
        }

        public JudgementLineEditorViewModel? Editor
        {
            get => (JudgementLineEditorViewModel?)GetValue(EditorProperty);
            set => SetValue(EditorProperty, value);
        }

        public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
            nameof(Editor),
            typeof(JudgementLineEditorViewModel),
            typeof(JudgementLineEditorCanvas),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnEditorChanged));

        private static void OnEditorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not JudgementLineEditorCanvas canvas)
            {
                return;
            }

            if (e.OldValue is JudgementLineEditorViewModel oldEditor)
            {
                oldEditor.PropertyChanged -= canvas.Editor_PropertyChanged;
                oldEditor.Timeline.PropertyChanged -= canvas.Timeline_PropertyChanged;
            }

            if (e.NewValue is JudgementLineEditorViewModel newEditor)
            {
                newEditor.PropertyChanged += canvas.Editor_PropertyChanged;
                newEditor.Timeline.PropertyChanged += canvas.Timeline_PropertyChanged;
            }
        }

        public bool TryHitTest(Point viewportPoint, out NoteViewModel? note, out HitTargetKind targetKind, out Point anchorPoint)
        {
            note = null;
            targetKind = HitTargetKind.None;
            anchorPoint = default;

            var editor = Editor;
            var track = editor?.ActiveTrack;
            if (editor == null || track == null)
            {
                return false;
            }

            var metrics = JudgementLineEditorRenderMath.CalculateMetrics(RenderSize, editor.ViewZoom);
            double centerX = RenderSize.Width / 2.0 + editor.PanX;
            double centerY = RenderSize.Height / 2.0 + editor.PanY;
            double currentTick = editor.Timeline.GetExactTick();

            foreach (var candidate in track.UINotes.Reverse())
            {
                var renderState = GetNoteRenderState(editor.Timeline, candidate, currentTick);
                if (!ShouldRenderNote(candidate, renderState.Kind, currentTick))
                {
                    continue;
                }

                Point noteCenter = GetNoteCenter(candidate, editor, track, metrics, RenderSize, centerX, centerY, currentTick, renderState);
                if (renderState.Kind == NoteKind.Hold)
                {
                    Rect tailHandleRect = GetHoldTailHandleRect(candidate, editor, track, metrics, RenderSize, noteCenter, renderState);
                    if (tailHandleRect.Contains(viewportPoint))
                    {
                        note = candidate;
                        targetKind = HitTargetKind.HoldTailHandle;
                        anchorPoint = new Point(tailHandleRect.X + tailHandleRect.Width / 2.0, tailHandleRect.Y + tailHandleRect.Height / 2.0);
                        return true;
                    }
                }

                Rect bodyRect = GetNoteBodyRect(candidate, editor, track, metrics, RenderSize, noteCenter, renderState);
                if (bodyRect.Contains(viewportPoint))
                {
                    note = candidate;
                    targetKind = HitTargetKind.NoteBody;
                    anchorPoint = noteCenter;
                    return true;
                }
            }

            return false;
        }

        public void SelectNotesInRect(Rect selectionRect, bool addToSelection)
        {
            var editor = Editor;
            var track = editor?.ActiveTrack;
            if (editor == null || track == null)
            {
                return;
            }

            if (!addToSelection)
            {
                foreach (var note in track.UINotes)
                {
                    note.IsSelected = false;
                }
            }

            var metrics = JudgementLineEditorRenderMath.CalculateMetrics(RenderSize, editor.ViewZoom);
            double centerX = RenderSize.Width / 2.0 + editor.PanX;
            double centerY = RenderSize.Height / 2.0 + editor.PanY;
            double currentTick = editor.Timeline.GetExactTick();

            foreach (var candidate in track.UINotes)
            {
                var renderState = GetNoteRenderState(editor.Timeline, candidate, currentTick);
                if (!ShouldRenderNote(candidate, renderState.Kind, currentTick))
                {
                    continue;
                }

                Point noteCenter = GetNoteCenter(candidate, editor, track, metrics, RenderSize, centerX, centerY, currentTick, renderState);
                
                Rect bodyRect = GetNoteBodyRect(candidate, editor, track, metrics, RenderSize, noteCenter, renderState);
                if (selectionRect.IntersectsWith(bodyRect))
                {
                    candidate.IsSelected = true;
                }
                else if (renderState.Kind == NoteKind.Hold)
                {
                    // Check handle too? Usually clicking body is enough to select.
                    // But if checking intersection, body intersection covers it.
                }
            }
            InvalidateVisual();
        }

        private void Timeline_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TimelineViewModel.CurrentPlayTimeSeconds))
            {
                InvalidateVisual();
            }
        }

        private void Editor_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(JudgementLineEditorViewModel.ActiveTrack)
                || e.PropertyName == nameof(JudgementLineEditorViewModel.HorizontalDivisions)
                || e.PropertyName == nameof(JudgementLineEditorViewModel.ViewZoom)
                || e.PropertyName == nameof(JudgementLineEditorViewModel.PanX)
                || e.PropertyName == nameof(JudgementLineEditorViewModel.PanY)
                || e.PropertyName == nameof(JudgementLineEditorViewModel.CurrentNoteKind)
                || e.PropertyName == nameof(JudgementLineEditorViewModel.HasPendingHoldPlacement)
                || e.PropertyName == nameof(JudgementLineEditorViewModel.PendingHoldChartX)
                || e.PropertyName == nameof(JudgementLineEditorViewModel.PendingHoldStartTick))
            {
                InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var editor = Editor;
            var track = editor?.ActiveTrack;
            if (editor == null || track == null)
            {
                return;
            }

            double width = RenderSize.Width;
            double height = RenderSize.Height;
            Size viewportSize = RenderSize;
            var metrics = JudgementLineEditorRenderMath.CalculateMetrics(viewportSize, editor.ViewZoom);
            double centerX = width / 2.0 + editor.PanX;
            double centerY = height / 2.0 + editor.PanY;
            double currentTick = editor.Timeline.GetExactTick();
            var multiHitTicks = CollectMultiHitTicks(editor.Timeline);

            DrawVerticalGrid(drawingContext, editor, width, height, centerX, centerY, metrics.PixelsPerChartUnit);
            DrawHorizontalGrid(drawingContext, editor, track, viewportSize, width, height, centerX, centerY, currentTick);
            DrawCenterLine(drawingContext, width, centerY);
            DrawNotes(drawingContext, editor, track, viewportSize, metrics, width, height, centerX, centerY, currentTick, multiHitTicks);
            DrawHoveredNoteTimelineGuide(drawingContext, editor, track, viewportSize, metrics, width, height, centerX, centerY, currentTick);
            DrawHoverPreview(drawingContext, editor, track, viewportSize, metrics, width, height, centerX, centerY, currentTick, multiHitTicks);

            if (!SelectionRect.IsEmpty)
            {
                drawingContext.DrawRectangle(SelectionRectBrush, SelectionRectPen, SelectionRect);
            }
        }

        private static void DrawVerticalGrid(DrawingContext dc, JudgementLineEditorViewModel editor, double width, double height, double centerX, double centerY, double pixelsPerChartUnit)
        {
            int divisions = Math.Max(1, editor.HorizontalDivisions);
            double divisionWidth = 16.0 / divisions;
            int majorStride = Math.Max(1, divisions / 4);

            for (int index = 0; index <= divisions; index++)
            {
                double chartX = -8.0 + index * divisionWidth;
                double pixelX = centerX + chartX * pixelsPerChartUnit;
                if (pixelX < -1 || pixelX > width + 1)
                {
                    continue;
                }

                bool isMajor = index == 0 || index == divisions || index % majorStride == 0;
                dc.DrawLine(isMajor ? VerticalMajorGridPen : VerticalGridPen, new Point(pixelX, 0), new Point(pixelX, height));

                if (divisions <= 16 || index % 2 == 0 || index == 0 || index == divisions)
                {
                    var text = CreateLabel(chartX.ToString("0.##", CultureInfo.InvariantCulture), 11);
                    Point textOrigin = new Point(pixelX + 4, centerY + 8);
                    DrawLabelBadge(dc, text, textOrigin);
                }
            }
        }

        private static void DrawHorizontalGrid(DrawingContext dc, JudgementLineEditorViewModel editor, TrackViewModel track, Size viewportSize, double width, double height, double centerX, double centerY, double currentTick)
        {
            int minorStep = 32;
            int majorStep = 128;
            int startTick = Math.Max(0, track.Data.StartTick / minorStep * minorStep);
            int endTick = Math.Max(startTick, (int)Math.Ceiling((track.Data.StartTick + track.Data.DurationTicks) / (double)minorStep) * minorStep);

            for (int absoluteTick = startTick; absoluteTick <= endTick; absoluteTick += minorStep)
            {
                double y = JudgementLineEditorRenderMath.CalculateHoverY(editor.Timeline, track, currentTick, absoluteTick, viewportSize, editor.ViewZoom, centerY);
                if (y < -4 || y > height + 4)
                {
                    continue;
                }

                bool isMajor = absoluteTick % majorStep == 0;
                dc.DrawLine(isMajor ? MajorGridPen : MinorGridPen, new Point(0, y), new Point(width, y));

                double amplitude = GetWaveAmplitude(editor, absoluteTick);
                double waveWidth = 16 + amplitude * 42;
                dc.DrawRectangle(WaveBrush, null, new Rect(10, y - 1.5, waveWidth, 3));
                dc.DrawRectangle(WaveBrush, null, new Rect(width - 10 - waveWidth, y - 1.5, waveWidth, 3));

                if (isMajor)
                {
                    int measureIndex = absoluteTick / majorStep;
                    var label = CreateLabel(measureIndex.ToString(CultureInfo.InvariantCulture), 10);
                    DrawLabelBadge(dc, label, new Point(centerX + 12, y - 8));
                }
            }
        }

        private static double GetWaveAmplitude(JudgementLineEditorViewModel editor, double absoluteTick)
        {
            var peaks = editor.Timeline.AudioTrack?.WaveformPeaks;
            var chart = editor.Timeline.CurrentChart;
            double duration = editor.Timeline.AudioTrack?.AudioDurationSeconds ?? 0;
            if (peaks == null || peaks.Length == 0 || duration <= 0)
            {
                return 0;
            }

            double seconds = TimeTickConverter.TickToTime(absoluteTick, chart.BpmKeyFrames, chart.InitialBpm);
            double offsetSeconds = TimeTickConverter.TickToTime(editor.Timeline.AudioOffsetTicks, chart.BpmKeyFrames, chart.InitialBpm);
            double audioSeconds = seconds - offsetSeconds;
            if (audioSeconds < 0 || audioSeconds > duration)
            {
                return 0;
            }

            int index = (int)Math.Clamp(Math.Round(audioSeconds / duration * (peaks.Length - 1)), 0, peaks.Length - 1);
            return Math.Clamp(peaks[index], 0, 1);
        }

        private static void DrawCenterLine(DrawingContext dc, double width, double centerY)
        {
            dc.DrawLine(CenterPen, new Point(0, centerY), new Point(width, centerY));
        }

        private static void DrawNotes(DrawingContext dc, JudgementLineEditorViewModel editor, TrackViewModel track, Size viewportSize, JudgementLineEditorRenderMath.ViewMetrics metrics, double width, double height, double centerX, double centerY, double currentTick, HashSet<int> multiHitTicks)
        {
            foreach (var note in track.UINotes)
            {
                var renderState = GetNoteRenderState(editor.Timeline, note, currentTick);
                bool isHold = renderState.Kind == NoteKind.Hold;
                if (!ShouldRenderNote(note, renderState.Kind, currentTick))
                {
                    continue;
                }

                double x = centerX + renderState.Offset.X * metrics.PixelsPerChartUnit;
                double y = CalculateNoteY(editor.Timeline, track, note, currentTick, viewportSize, editor.ViewZoom, centerY, renderState.Offset.Y);
                double renderedNoteWidth = GetRenderedNotePixelWidth(metrics.NotePixelWidth, renderState.Kind, multiHitTicks.Contains(note.HitTime));
                double noteBounds = Math.Max(renderedNoteWidth * Math.Max(Math.Abs(renderState.Scale.X), Math.Abs(renderState.Scale.Y)), 24);
                if (isHold)
                {
                    double holdLength = JudgementLineEditorRenderMath.CalculateHoldLength(editor.Timeline, track, note, viewportSize, editor.ViewZoom);
                    noteBounds = Math.Max(noteBounds, holdLength + renderedNoteWidth);
                }

                if (x < -noteBounds || x > width + noteBounds || y < -noteBounds || y > height + noteBounds)
                {
                    continue;
                }

                DrawRenderedNote(dc, editor, track, note, viewportSize, metrics, currentTick, x, y, centerY, 1.0, multiHitTicks.Contains(note.HitTime), renderState);

                if (note.IsSelected)
                {
                    Rect selectionRect = GetSelectionRect(note, editor, track, metrics, viewportSize, new Point(x, y), renderState);
                    dc.DrawRectangle(null, SelectedNotePen, selectionRect);

                    if (isHold)
                    {
                        DrawHoldTailHandle(dc, note, editor, track, metrics, viewportSize, new Point(x, y), renderState);
                    }
                }
            }
        }

        private static void DrawHoverPreview(DrawingContext dc, JudgementLineEditorViewModel editor, TrackViewModel track, Size viewportSize, JudgementLineEditorRenderMath.ViewMetrics metrics, double width, double height, double centerX, double centerY, double currentTick, HashSet<int> multiHitTicks)
        {
            if (!editor.HasHoverPreview || !Enum.TryParse<NoteKind>(editor.CurrentNoteKind, out var kind))
            {
                return;
            }

            if (kind == NoteKind.Hold && editor.HasPendingHoldPlacement)
            {
                double previewX = centerX + editor.PendingHoldChartX * metrics.PixelsPerChartUnit;
                double previewY = JudgementLineEditorRenderMath.CalculateHoverY(editor.Timeline, track, currentTick, editor.PendingHoldStartTick, viewportSize, editor.ViewZoom, centerY);
                int holdDuration = Math.Max(1, editor.HoverHitTick - editor.PendingHoldStartTick);
                double holdLength = CalculatePreviewHoldLength(editor, track, viewportSize, centerY, holdDuration);
                double holdPreviewWidth = GetRenderedNotePixelWidth(metrics.NotePixelWidth, NoteKind.Hold, false);
                double holdPreviewBounds = Math.Max(holdPreviewWidth + 12, holdLength + holdPreviewWidth);
                if (previewX < -holdPreviewBounds || previewX > width + holdPreviewBounds || previewY < -holdPreviewBounds || previewY > height + holdPreviewBounds)
                {
                    return;
                }

                DrawPreviewHold(dc, holdPreviewWidth, previewX, previewY, holdLength, 0.58, true, false);
                return;
            }

            double y = JudgementLineEditorRenderMath.CalculateHoverY(editor.Timeline, track, currentTick, editor.HoverHitTick, viewportSize, editor.ViewZoom, centerY);
            double x = centerX + editor.HoverChartX * metrics.PixelsPerChartUnit;
            bool isMultiHitPreview = multiHitTicks.Contains(editor.HoverHitTick);
            double previewWidth = GetRenderedNotePixelWidth(metrics.NotePixelWidth, kind, isMultiHitPreview);
            double previewBounds = previewWidth + 12;
            if (x < -previewBounds || x > width + previewBounds || y < -previewBounds || y > height + previewBounds)
            {
                return;
            }

            if (kind == NoteKind.Hold)
            {
                DrawPreviewHold(dc, previewWidth, x, y, 0, 0.58, true, isMultiHitPreview);
            }
            else
            {
                DrawPreviewNote(dc, kind, previewWidth, x, y, 0.58, isMultiHitPreview);
            }

            string fractionText = BuildBeatFractionText(editor.HoverHitTick);
            var style = ResolveBeatGuideVisualStyle(editor.HoverHitTick);
            FormattedText previewMarkerText = CreateLabel(fractionText, 11.0, style.LabelBrush);
            double markerX = Math.Clamp(x + 14, 8, Math.Max(8, width - previewMarkerText.Width - 8));
            double markerY = Math.Clamp(y - previewMarkerText.Height - 10, 6, Math.Max(6, height - previewMarkerText.Height - 6));
            DrawLabelBadge(dc, previewMarkerText, new Point(markerX, markerY), style.LabelBackgroundBrush);
        }

        private void DrawHoveredNoteTimelineGuide(DrawingContext dc, JudgementLineEditorViewModel editor, TrackViewModel track, Size viewportSize, JudgementLineEditorRenderMath.ViewMetrics metrics, double width, double height, double centerX, double centerY, double currentTick)
        {
            if (!IsMouseOver)
            {
                return;
            }

            Point pointer = Mouse.GetPosition(this);
            if (!TryHitTest(pointer, out var hoveredNote, out _, out _) || hoveredNote == null)
            {
                return;
            }

            int hitTick = hoveredNote.HitTime;
            double y = JudgementLineEditorRenderMath.CalculateHoverY(editor.Timeline, track, currentTick, hitTick, viewportSize, editor.ViewZoom, centerY);
            if (y < -4 || y > height + 4)
            {
                return;
            }

            var style = ResolveBeatGuideVisualStyle(hitTick);
            string fractionText = BuildBeatFractionText(hitTick);

            dc.DrawLine(style.GuidePen, new Point(0, y), new Point(width, y));

            FormattedText markerText = CreateLabel(fractionText, 11.5, style.LabelBrush);
            double markerX = Math.Max(10, width - markerText.Width - 18);
            DrawLabelBadge(dc, markerText, new Point(markerX, y - markerText.Height - 8), style.LabelBackgroundBrush);

            var renderState = GetNoteRenderState(editor.Timeline, hoveredNote, currentTick);
            Point noteCenter = GetNoteCenter(hoveredNote, editor, track, metrics, viewportSize, centerX, centerY, currentTick, renderState);
            double noteMarkerX = Math.Clamp(noteCenter.X + 14, 8, Math.Max(8, width - markerText.Width - 8));
            double noteMarkerY = Math.Clamp(noteCenter.Y - markerText.Height - 10, 6, Math.Max(6, height - markerText.Height - 6));
            DrawLabelBadge(dc, markerText, new Point(noteMarkerX, noteMarkerY), style.LabelBackgroundBrush);
        }

        private static void DrawRenderedNote(DrawingContext dc, JudgementLineEditorViewModel editor, TrackViewModel track, NoteViewModel note, Size viewportSize, JudgementLineEditorRenderMath.ViewMetrics metrics, double currentTick, double centerX, double centerY, double judgementLineY, double opacityMultiplier, bool isMultiHit, NoteRenderState renderState)
        {
            bool isHold = renderState.Kind == NoteKind.Hold;
            bool useMultiHitResource = isMultiHit;
            bool isForwardFlow = (note.Model.CustomSpeed ?? 1.0) >= 0;

            if (isHold)
            {
                Rect clipRect = isForwardFlow
                    ? new Rect(-100000, -100000, 200000, 100000 + judgementLineY)
                    : new Rect(-100000, judgementLineY, 200000, 100000);
                dc.PushClip(new RectangleGeometry(clipRect));
            }

            dc.PushTransform(new TranslateTransform(centerX, centerY));
            dc.PushTransform(new RotateTransform(renderState.Rotation));
            dc.PushTransform(new ScaleTransform(renderState.Scale.X, renderState.Scale.Y));
            dc.PushOpacity((renderState.Opacity / 100.0) * opacityMultiplier);

            if (isHold)
            {
                double holdPixelLength = JudgementLineEditorRenderMath.CalculateHoldLength(editor.Timeline, track, note, viewportSize, editor.ViewZoom);
                double notePixelWidth = GetRenderedNotePixelWidth(metrics.NotePixelWidth, renderState.Kind, useMultiHitResource);
                DrawHoldVisual(dc, notePixelWidth, holdPixelLength, isForwardFlow, useMultiHitResource, drawHead: true);
            }
            else
            {
                double renderedNoteWidth = GetRenderedNotePixelWidth(metrics.NotePixelWidth, renderState.Kind, isMultiHit);
                DrawNoteImage(dc, renderState.Kind, renderedNoteWidth, opacity: 1.0, isMultiHit);
            }

            dc.Pop();
            dc.Pop();
            dc.Pop();
            dc.Pop();

            if (isHold)
            {
                dc.Pop();
            }
        }

        private static void DrawPreviewNote(DrawingContext dc, NoteKind kind, double width, double centerX, double centerY, double opacity, bool isMultiHit)
        {
            dc.PushTransform(new TranslateTransform(centerX, centerY));
            DrawNoteImage(dc, kind, width, opacity, isMultiHit);
            dc.Pop();
        }

        private static void DrawPreviewHold(DrawingContext dc, double notePixelWidth, double centerX, double centerY, double holdPixelLength, double opacity, bool isForwardFlow, bool isMultiHit)
        {
            dc.PushTransform(new TranslateTransform(centerX, centerY));
            dc.PushOpacity(opacity);

            DrawHoldVisual(dc, notePixelWidth, holdPixelLength, isForwardFlow, isMultiHit, drawHead: true);

            dc.Pop();
            dc.Pop();
        }

        private static void DrawHoldVisual(DrawingContext dc, double notePixelWidth, double holdPixelLength, bool isForwardFlow, bool isMultiHit, bool drawHead)
        {
            double sourceWidth = GetTexturePixelWidth(NoteKind.Hold, isMultiHit);
            double headPixels = isMultiHit ? HoldHighlightHeadPixels : HoldHeadPixels;
            double tailPixels = isMultiHit ? HoldHighlightTailPixels : HoldTailPixels;
            double headHeight = notePixelWidth * (headPixels / sourceWidth);
            double tailHeight = notePixelWidth * (tailPixels / sourceWidth);
            if (holdPixelLength < tailHeight)
            {
                holdPixelLength = tailHeight;
            }

            double bodyHeight = Math.Max(0, holdPixelLength - tailHeight);
            double overlap = HoldSegmentOverlapPixels;
            Rect headRect = new Rect(-notePixelWidth / 2, -overlap, notePixelWidth, headHeight + overlap);
            Rect tailRect = new Rect(-notePixelWidth / 2, -holdPixelLength, notePixelWidth, tailHeight + overlap);
            Rect bodyRect = new Rect(-notePixelWidth / 2, -bodyHeight, notePixelWidth, bodyHeight);

            if (!isForwardFlow)
            {
                dc.PushTransform(new ScaleTransform(1, -1));
            }

            dc.DrawRectangle(isMultiHit ? HoldBodyMultiHitBrush : HoldBodyBrush, null, bodyRect);
            dc.DrawRectangle(isMultiHit ? HoldTailMultiHitBrush : HoldTailBrush, null, tailRect);
            if (drawHead)
            {
                dc.DrawRectangle(isMultiHit ? HoldHeadMultiHitBrush : HoldHeadBrush, null, headRect);
            }

            if (!isForwardFlow)
            {
                dc.Pop();
            }
        }

        private static void DrawHoldTailHandle(DrawingContext dc, NoteViewModel note, JudgementLineEditorViewModel editor, TrackViewModel track, JudgementLineEditorRenderMath.ViewMetrics metrics, Size viewportSize, Point noteCenter, NoteRenderState renderState)
        {
            Rect handleRect = GetHoldTailHandleRect(note, editor, track, metrics, viewportSize, noteCenter, renderState);
            dc.DrawEllipse(HoldTailHandleBrush, HoldTailHandlePen,
                new Point(handleRect.X + handleRect.Width / 2.0, handleRect.Y + handleRect.Height / 2.0),
                handleRect.Width / 2.0,
                handleRect.Height / 2.0);
        }

        private static bool ShouldRenderNote(NoteViewModel note, NoteKind currentKind, double currentTick)
        {
            bool isHold = currentKind == NoteKind.Hold;
            if (!isHold && currentTick >= note.HitTime)
            {
                return false;
            }

            if (isHold && currentTick >= note.HitTime + note.HoldDuration)
            {
                return false;
            }

            return true;
        }

        private static Point GetNoteCenter(NoteViewModel note, JudgementLineEditorViewModel editor, TrackViewModel track, JudgementLineEditorRenderMath.ViewMetrics metrics, Size viewportSize, double centerX, double centerY, double currentTick, NoteRenderState renderState)
        {
            return new Point(
                centerX + renderState.Offset.X * metrics.PixelsPerChartUnit,
                CalculateNoteY(editor.Timeline, track, note, currentTick, viewportSize, editor.ViewZoom, centerY, renderState.Offset.Y));
        }

        private static Rect GetNoteBodyRect(NoteViewModel note, JudgementLineEditorViewModel editor, TrackViewModel track, JudgementLineEditorRenderMath.ViewMetrics metrics, Size viewportSize, Point noteCenter, NoteRenderState renderState)
        {
            double scaleX = Math.Abs(renderState.Scale.X);
            double scaleY = Math.Abs(renderState.Scale.Y);
            bool isMultiHit = IsMultiHitTick(editor.Timeline, note.HitTime);
            double notePixelWidth = GetRenderedNotePixelWidth(metrics.NotePixelWidth, renderState.Kind, isMultiHit) * Math.Max(scaleX, 0.1);
            ImageSource noteImage = GetNoteImage(renderState.Kind, isMultiHit);
            double notePixelHeight = notePixelWidth * (GetImagePixelHeight(noteImage) / GetImagePixelWidth(noteImage)) * Math.Max(scaleY, 0.1);

            if (renderState.Kind != NoteKind.Hold)
            {
                return new Rect(noteCenter.X - notePixelWidth / 2.0, noteCenter.Y - notePixelHeight / 2.0, notePixelWidth, notePixelHeight);
            }

            double holdLength = JudgementLineEditorRenderMath.CalculateHoldLength(editor.Timeline, track, note, viewportSize, editor.ViewZoom) * Math.Max(scaleY, 0.1);
            bool isForwardFlow = (note.Model.CustomSpeed ?? 1.0) >= 0;
            double headPixels = isMultiHit ? HoldHighlightHeadPixels : HoldHeadPixels;
            double headHeight = notePixelWidth * (headPixels / GetTexturePixelWidth(NoteKind.Hold, isMultiHit)) * Math.Max(scaleY, 0.1);
            double top = isForwardFlow ? noteCenter.Y - holdLength : noteCenter.Y - headHeight;
            double bottom = isForwardFlow ? noteCenter.Y + headHeight : noteCenter.Y + holdLength;
            return new Rect(noteCenter.X - notePixelWidth / 2.0, top, notePixelWidth, bottom - top);
        }

        private static Rect GetSelectionRect(NoteViewModel note, JudgementLineEditorViewModel editor, TrackViewModel track, JudgementLineEditorRenderMath.ViewMetrics metrics, Size viewportSize, Point noteCenter, NoteRenderState renderState)
        {
            Rect noteBodyRect = GetNoteBodyRect(note, editor, track, metrics, viewportSize, noteCenter, renderState);
            double padding = Math.Max(4.0, Math.Min(noteBodyRect.Width, noteBodyRect.Height) * 0.08);
            noteBodyRect.Inflate(padding, padding);
            return noteBodyRect;
        }

        private static Rect GetHoldTailHandleRect(NoteViewModel note, JudgementLineEditorViewModel editor, TrackViewModel track, JudgementLineEditorRenderMath.ViewMetrics metrics, Size viewportSize, Point noteCenter, NoteRenderState renderState)
        {
            double scaleX = Math.Abs(renderState.Scale.X);
            double scaleY = Math.Abs(renderState.Scale.Y);
            double holdLength = JudgementLineEditorRenderMath.CalculateHoldLength(editor.Timeline, track, note, viewportSize, editor.ViewZoom) * Math.Max(scaleY, 0.1);
            bool isForwardFlow = (note.Model.CustomSpeed ?? 1.0) >= 0;
            bool isMultiHit = IsMultiHitTick(editor.Timeline, note.HitTime);
            double renderedWidth = GetRenderedNotePixelWidth(metrics.NotePixelWidth, renderState.Kind, isMultiHit);
            double handleSize = Math.Max(12.0, renderedWidth * 0.22 * Math.Max(scaleX, scaleY));
            double handleCenterY = noteCenter.Y + (isForwardFlow ? -holdLength : holdLength);
            return new Rect(noteCenter.X - handleSize / 2.0, handleCenterY - handleSize / 2.0, handleSize, handleSize);
        }

        private static NoteRenderState GetNoteRenderState(TimelineViewModel timeline, NoteViewModel note, double currentTick)
        {
            EasingUtils.CalculateObjectTransform(
                currentTick,
                timeline.CurrentChart.KeyFrameEasingDirection,
                note.Model.AnimatableProperties,
                out var offset,
                out var scale,
                out var rotation,
                out var opacity);

            int discreteTick = (int)Math.Round(currentTick, MidpointRounding.AwayFromZero);
            NoteKind kind = note.Model.KindKeyFrames != null
                ? KeyFrameUtils.GetStepValueAtTick(note.Model.KindKeyFrames, discreteTick, note.Model.InitialKind)
                : note.Model.InitialKind;

            return new NoteRenderState(kind, offset, scale, rotation, opacity);
        }

        private static double CalculateNoteY(TimelineViewModel timeline, TrackViewModel track, NoteViewModel note, double currentTick, Size viewportSize, double viewZoom, double centerY, double offsetY)
        {
            var metrics = JudgementLineEditorRenderMath.CalculateMetrics(viewportSize, viewZoom);
            double noteSpeedMultiplier = note.Model.CustomSpeed ?? 1.0;
            double pixelDistance = CalculateTravelDistance(timeline.CurrentChart, track.Data, currentTick, note.HitTime, noteSpeedMultiplier, metrics);
            double pixelOffsetY = offsetY * metrics.PixelsPerChartUnit;
            return centerY + pixelOffsetY + pixelDistance;
        }

        private static double CalculateTravelDistance(Chart chart, JudgementLine line, double startTick, double endTick, double noteSpeedMultiplier, JudgementLineEditorRenderMath.ViewMetrics metrics)
        {
            if (line.SpeedMode == "Realtime")
            {
                double currentSeconds = TimeTickConverter.TickToTime(startTick, chart.BpmKeyFrames, chart.InitialBpm);
                double hitTimeSeconds = TimeTickConverter.TickToTime(endTick, chart.BpmKeyFrames, chart.InitialBpm);
                double currentRealtimeSpeed = line.InitialSpeed;

                EasingUtils.CalculateObjectSingleTransform(
                    startTick,
                    chart.KeyFrameEasingDirection,
                    line.InitialSpeed,
                    line.SpeedKeyFrames,
                    MathUtils.Lerp,
                    out currentRealtimeSpeed);

                double actualPixelsPerSecond = metrics.BaseVerticalFlowPixelsPerSecond * currentRealtimeSpeed * noteSpeedMultiplier;
                return -actualPixelsPerSecond * (hitTimeSeconds - currentSeconds);
            }

            return -CalculateIntegralDistance(startTick, endTick, line, chart, noteSpeedMultiplier, metrics.BaseVerticalFlowPixelsPerSecond);
        }

        private static double CalculateIntegralDistance(double startTick, double endTick, JudgementLine line, Chart chart, double noteSpeedMultiplier, double baseVerticalFlowPixelsPerSecond)
        {
            if (Math.Abs(startTick - endTick) < double.Epsilon)
            {
                return 0;
            }

            int steps = 150;
            double totalDistance = 0;
            double tMin = Math.Min(startTick, endTick);
            double tMax = Math.Max(startTick, endTick);
            double stepTick = (tMax - tMin) / steps;

            for (int index = 0; index < steps; index++)
            {
                double t1 = tMin + index * stepTick;
                double t2 = tMin + (index + 1) * stepTick;

                double sec1 = TimeTickConverter.TickToTime(t1, chart.BpmKeyFrames, chart.InitialBpm);
                double sec2 = TimeTickConverter.TickToTime(t2, chart.BpmKeyFrames, chart.InitialBpm);
                double midTick = (t1 + t2) / 2.0;

                EasingUtils.CalculateObjectSingleTransform(
                    midTick,
                    chart.KeyFrameEasingDirection,
                    line.InitialSpeed,
                    line.SpeedKeyFrames,
                    MathUtils.Lerp,
                    out var midSpeed);

                totalDistance += midSpeed * (sec2 - sec1);
            }

            double pixelDistance = totalDistance * baseVerticalFlowPixelsPerSecond * noteSpeedMultiplier;
            return startTick <= endTick ? pixelDistance : -pixelDistance;
        }

        private static ImageSource GetNoteImage(NoteKind kind, bool isMultiHit)
        {
            return kind switch
            {
                NoteKind.Tap => isMultiHit ? TapMultiHitImage : TapImage,
                NoteKind.Drag => isMultiHit ? DragMultiHitImage : DragImage,
                NoteKind.Hold => HoldImage,
                NoteKind.Flick => isMultiHit ? FlickMultiHitImage : FlickImage,
                _ => isMultiHit ? TapMultiHitImage : TapImage,
            };
        }

        private static void DrawNoteImage(DrawingContext dc, NoteKind kind, double width, double opacity, bool isMultiHit)
        {
            ImageSource image = GetNoteImage(kind, isMultiHit);

            double aspectRatio = GetImagePixelHeight(image) / GetImagePixelWidth(image);
            double height = width * aspectRatio;
            dc.PushOpacity(opacity);
            dc.DrawImage(image, new Rect(-width / 2, -height / 2, width, height));
            dc.Pop();
        }

        private static HashSet<int> CollectMultiHitTicks(TimelineViewModel timeline)
        {
            return timeline.Tracks
                .SelectMany(track => track.UINotes)
                .GroupBy(note => note.HitTime)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet();
        }

        private static bool IsMultiHitTick(TimelineViewModel timeline, int hitTick)
        {
            return timeline.Tracks.SelectMany(track => track.UINotes).Count(note => note.HitTime == hitTick) > 1;
        }

        private static double GetRenderedNotePixelWidth(double basePixelWidth, NoteKind kind, bool isMultiHit)
        {
            return basePixelWidth * (GetTexturePixelWidth(kind, isMultiHit) / BaseNoteTextureWidthPixels);
        }

        private static double GetTexturePixelWidth(NoteKind kind, bool isMultiHit)
        {
            BitmapImage image = kind switch
            {
                NoteKind.Tap => isMultiHit ? TapMultiHitImage : TapImage,
                NoteKind.Drag => isMultiHit ? DragMultiHitImage : DragImage,
                NoteKind.Hold => isMultiHit ? HoldMultiHitImage : HoldImage,
                NoteKind.Flick => isMultiHit ? FlickMultiHitImage : FlickImage,
                _ => isMultiHit ? TapMultiHitImage : TapImage,
            };

            return Math.Max(1.0, image.PixelWidth);
        }

        private static double GetImagePixelWidth(ImageSource image)
        {
            return image switch
            {
                BitmapSource bitmap => Math.Max(1.0, bitmap.PixelWidth),
                _ => Math.Max(1.0, image.Width)
            };
        }

        private static double GetImagePixelHeight(ImageSource image)
        {
            return image switch
            {
                BitmapSource bitmap => Math.Max(1.0, bitmap.PixelHeight),
                _ => Math.Max(1.0, image.Height)
            };
        }

        private static FormattedText CreateLabel(string text, double fontSize)
        {
            return CreateLabel(text, fontSize, LabelBrush);
        }

        private static FormattedText CreateLabel(string text, double fontSize, Brush foreground)
        {
            return new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelTypeface,
                fontSize,
                foreground,
                1.25);
        }

        private static double CalculatePreviewHoldLength(JudgementLineEditorViewModel editor, TrackViewModel track, Size viewportSize, double centerY, int holdDuration)
        {
            if (holdDuration <= 0)
            {
                return 0;
            }

            double currentTick = editor.Timeline.GetExactTick();
            double startY = JudgementLineEditorRenderMath.CalculateHoverY(editor.Timeline, track, currentTick, editor.PendingHoldStartTick, viewportSize, editor.ViewZoom, centerY);
            double endY = JudgementLineEditorRenderMath.CalculateHoverY(editor.Timeline, track, currentTick, editor.PendingHoldStartTick + holdDuration, viewportSize, editor.ViewZoom, centerY);
            return Math.Abs(startY - endY);
        }

        private static void DrawLabelBadge(DrawingContext dc, FormattedText text, Point origin)
        {
            DrawLabelBadge(dc, text, origin, LabelBackgroundBrush);
        }

        private static void DrawLabelBadge(DrawingContext dc, FormattedText text, Point origin, Brush backgroundBrush)
        {
            const double horizontalPadding = 5.0;
            const double verticalPadding = 2.0;
            Rect backgroundRect = new Rect(
                origin.X - horizontalPadding,
                origin.Y - verticalPadding,
                text.Width + horizontalPadding * 2.0,
                text.Height + verticalPadding * 2.0);

            dc.DrawRoundedRectangle(backgroundBrush, null, backgroundRect, 3.0, 3.0);
            dc.DrawText(text, origin);
        }

        private static (int Numerator, int Denominator) BuildBeatFractionWithoutReduction(int tickInMeasure)
        {
            int denominator = 16;
            while (denominator < 128 && (tickInMeasure * denominator) % 128 != 0)
            {
                denominator *= 2;
            }

            int numerator = tickInMeasure * denominator / 128;
            return (numerator, denominator);
        }

        private static string BuildBeatFractionText(int hitTick)
        {
            int tickInMeasure = ((hitTick % 128) + 128) % 128;
            (int numerator, int denominator) = BuildBeatFractionWithoutReduction(tickInMeasure);
            return $"{numerator}/{denominator}";
        }

        private static BeatGuideVisualStyle ResolveBeatGuideVisualStyle(int hitTick)
        {
            int tickInMeasure = ((hitTick % 128) + 128) % 128;
            if (tickInMeasure == 0)
            {
                return new BeatGuideVisualStyle(HoverTimelineStrongPen, HoverTimelineStrongLabelBrush, HoverTimelineStrongLabelBackgroundBrush);
            }

            if (tickInMeasure % 32 == 0)
            {
                return new BeatGuideVisualStyle(HoverTimelineMediumPen, HoverTimelineMediumLabelBrush, HoverTimelineMediumLabelBackgroundBrush);
            }

            if (tickInMeasure % 16 == 0)
            {
                return new BeatGuideVisualStyle(HoverTimelineWeakPen, HoverTimelineWeakLabelBrush, HoverTimelineWeakLabelBackgroundBrush);
            }

            return new BeatGuideVisualStyle(HoverTimelineFinePen, HoverTimelineFineLabelBrush, HoverTimelineFineLabelBackgroundBrush);
        }
    }
}