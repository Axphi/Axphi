using Axphi.Data;
using Axphi.Utilities;
using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Axphi.Components
{
    public class JudgementLineEditorCanvas : FrameworkElement
    {
        private static readonly Brush CenterLineBrush = new SolidColorBrush(Color.FromRgb(255, 244, 163));
        private static readonly Brush MajorGridBrush = new SolidColorBrush(Color.FromArgb(70, 126, 167, 255));
        private static readonly Brush MinorGridBrush = new SolidColorBrush(Color.FromArgb(28, 126, 167, 255));
        private static readonly Brush VerticalGridBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        private static readonly Brush LabelBrush = new SolidColorBrush(Color.FromArgb(170, 210, 220, 235));
        private static readonly Brush WaveBrush = new SolidColorBrush(Color.FromArgb(70, 64, 214, 255));
        private static readonly Brush PreviewOverlayBrush = new SolidColorBrush(Color.FromArgb(110, 255, 255, 255));
        private static readonly Brush HoldTailHandleBrush = new SolidColorBrush(Color.FromArgb(220, 244, 246, 255));
        private static readonly Pen CenterPen = new Pen(CenterLineBrush, 2.0);
        private static readonly Pen MajorGridPen = new Pen(MajorGridBrush, 1.0);
        private static readonly Pen MinorGridPen = new Pen(MinorGridBrush, 1.0);
        private static readonly Pen VerticalGridPen = new Pen(VerticalGridBrush, 1.0);
        private static readonly Pen SelectedNotePen = new Pen(Brushes.White, 1.4);
        private static readonly Pen HoverPreviewPen = new Pen(PreviewOverlayBrush, 1.0);
        private static readonly Pen HoldTailHandlePen = new Pen(new SolidColorBrush(Color.FromArgb(220, 137, 186, 255)), 1.2);
        private static readonly Typeface LabelTypeface = new Typeface("Consolas");
        private static readonly BitmapImage TapImage = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/tap.png"));
        private static readonly BitmapImage DragImage = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/drag.png"));
        private static readonly BitmapImage HoldImage = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/hold.png"));
        private static readonly BitmapImage FlickImage = new BitmapImage(new Uri("pack://application:,,,/Axphi;component/Resources/Notes/flick.png"));
        private static readonly ImageBrush HoldTailBrush;
        private static readonly ImageBrush HoldBodyBrush;
        private static readonly ImageBrush HoldHeadBrush;
        private bool _isMessengerRegistered;

        public enum HitTargetKind
        {
            None,
            NoteBody,
            HoldTailHandle,
        }

        static JudgementLineEditorCanvas()
        {
            HoldTailBrush = new ImageBrush(HoldImage) { Viewbox = new Rect(0, 0, 1, 1.0 / 3.0), ViewboxUnits = BrushMappingMode.RelativeToBoundingBox };
            HoldBodyBrush = new ImageBrush(HoldImage) { Viewbox = new Rect(0, 1.0 / 3.0, 1, 1.0 / 3.0), ViewboxUnits = BrushMappingMode.RelativeToBoundingBox };
            HoldHeadBrush = new ImageBrush(HoldImage) { Viewbox = new Rect(0, 2.0 / 3.0, 1, 1.0 / 3.0), ViewboxUnits = BrushMappingMode.RelativeToBoundingBox };

            if (CenterLineBrush.CanFreeze) CenterLineBrush.Freeze();
            if (MajorGridBrush.CanFreeze) MajorGridBrush.Freeze();
            if (MinorGridBrush.CanFreeze) MinorGridBrush.Freeze();
            if (VerticalGridBrush.CanFreeze) VerticalGridBrush.Freeze();
            if (LabelBrush.CanFreeze) LabelBrush.Freeze();
            if (WaveBrush.CanFreeze) WaveBrush.Freeze();
            if (PreviewOverlayBrush.CanFreeze) PreviewOverlayBrush.Freeze();
            if (HoldTailHandleBrush.CanFreeze) HoldTailHandleBrush.Freeze();
            if (CenterPen.CanFreeze) CenterPen.Freeze();
            if (MajorGridPen.CanFreeze) MajorGridPen.Freeze();
            if (MinorGridPen.CanFreeze) MinorGridPen.Freeze();
            if (VerticalGridPen.CanFreeze) VerticalGridPen.Freeze();
            if (SelectedNotePen.CanFreeze) SelectedNotePen.Freeze();
            if (HoverPreviewPen.CanFreeze) HoverPreviewPen.Freeze();
            if (HoldTailHandlePen.Brush.CanFreeze) ((SolidColorBrush)HoldTailHandlePen.Brush).Freeze();
            if (HoldTailHandlePen.CanFreeze) HoldTailHandlePen.Freeze();
            if (TapImage.CanFreeze) TapImage.Freeze();
            if (DragImage.CanFreeze) DragImage.Freeze();
            if (HoldImage.CanFreeze) HoldImage.Freeze();
            if (FlickImage.CanFreeze) FlickImage.Freeze();
            if (HoldTailBrush.CanFreeze) HoldTailBrush.Freeze();
            if (HoldBodyBrush.CanFreeze) HoldBodyBrush.Freeze();
            if (HoldHeadBrush.CanFreeze) HoldHeadBrush.Freeze();
        }

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
            int currentTick = editor.Timeline.GetCurrentTick();

            foreach (var candidate in track.UINotes.Reverse())
            {
                if (!ShouldRenderNote(candidate, currentTick))
                {
                    continue;
                }

                Point noteCenter = GetNoteCenter(candidate, editor, track, metrics, RenderSize, centerX, centerY, currentTick);
                if (candidate.CurrentNoteKind == NoteKind.Hold)
                {
                    Rect tailHandleRect = GetHoldTailHandleRect(candidate, editor, track, metrics, RenderSize, noteCenter);
                    if (tailHandleRect.Contains(viewportPoint))
                    {
                        note = candidate;
                        targetKind = HitTargetKind.HoldTailHandle;
                        anchorPoint = new Point(tailHandleRect.X + tailHandleRect.Width / 2.0, tailHandleRect.Y + tailHandleRect.Height / 2.0);
                        return true;
                    }
                }

                Rect bodyRect = GetNoteBodyRect(candidate, editor, track, metrics, RenderSize, noteCenter);
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
                || e.PropertyName == nameof(JudgementLineEditorViewModel.CurrentNoteKind))
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
            int currentTick = editor.Timeline.GetCurrentTick();

            DrawVerticalGrid(drawingContext, editor, width, height, centerX, centerY, metrics.PixelsPerChartUnit);
            DrawHorizontalGrid(drawingContext, editor, track, viewportSize, width, height, centerX, centerY, currentTick);
            DrawCenterLine(drawingContext, width, centerY);
            DrawNotes(drawingContext, editor, track, viewportSize, metrics, width, height, centerX, centerY, currentTick);
            DrawHoverPreview(drawingContext, editor, track, viewportSize, metrics, width, height, centerX, centerY, currentTick);
        }

        private static void DrawVerticalGrid(DrawingContext dc, JudgementLineEditorViewModel editor, double width, double height, double centerX, double centerY, double pixelsPerChartUnit)
        {
            double divisionWidth = 16.0 / editor.HorizontalDivisions;
            int halfCount = editor.HorizontalDivisions / 2;

            for (int index = -halfCount; index <= halfCount; index++)
            {
                double chartX = index * divisionWidth;
                double pixelX = centerX + chartX * pixelsPerChartUnit;
                if (pixelX < -1 || pixelX > width + 1)
                {
                    continue;
                }

                dc.DrawLine(VerticalGridPen, new Point(pixelX, 0), new Point(pixelX, height));

                if (editor.HorizontalDivisions <= 16 || index % 2 == 0)
                {
                    var text = CreateLabel(chartX.ToString("0.##", CultureInfo.InvariantCulture), 11);
                    dc.DrawText(text, new Point(pixelX + 4, centerY + 8));
                }
            }
        }

        private static void DrawHorizontalGrid(DrawingContext dc, JudgementLineEditorViewModel editor, TrackViewModel track, Size viewportSize, double width, double height, double centerX, double centerY, int currentTick)
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
                    dc.DrawText(label, new Point(centerX + 12, y - 8));
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
            double offsetSeconds = TimeTickConverter.TickToTime(chart.Offset, chart.BpmKeyFrames, chart.InitialBpm);
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

        private static void DrawNotes(DrawingContext dc, JudgementLineEditorViewModel editor, TrackViewModel track, Size viewportSize, JudgementLineEditorRenderMath.ViewMetrics metrics, double width, double height, double centerX, double centerY, int currentTick)
        {
            foreach (var note in track.UINotes)
            {
                bool isHold = note.CurrentNoteKind == NoteKind.Hold;
                if (!isHold && currentTick >= note.HitTime)
                {
                    continue;
                }

                if (isHold && currentTick >= note.HitTime + note.HoldDuration)
                {
                    continue;
                }

                double x = centerX + note.CurrentOffsetX * metrics.PixelsPerChartUnit;
                double y = JudgementLineEditorRenderMath.CalculateNoteY(editor.Timeline, track, note, currentTick, viewportSize, editor.ViewZoom, centerY);
                double noteBounds = Math.Max(metrics.NotePixelWidth * Math.Max(note.CurrentScaleX, note.CurrentScaleY), 24);
                if (isHold)
                {
                    double holdLength = JudgementLineEditorRenderMath.CalculateHoldLength(editor.Timeline, track, note, viewportSize, editor.ViewZoom);
                    noteBounds = Math.Max(noteBounds, holdLength + metrics.NotePixelWidth);
                }

                if (x < -noteBounds || x > width + noteBounds || y < -noteBounds || y > height + noteBounds)
                {
                    continue;
                }

                DrawRenderedNote(dc, editor, track, note, viewportSize, metrics, currentTick, x, y, centerY, 1.0);

                if (note.IsSelected)
                {
                    double selectionSize = metrics.NotePixelWidth * Math.Max(note.CurrentScaleX, note.CurrentScaleY) + 6;
                    dc.DrawRectangle(null, SelectedNotePen, new Rect(x - selectionSize / 2, y - selectionSize / 2, selectionSize, selectionSize));

                    if (isHold)
                    {
                        DrawHoldTailHandle(dc, note, editor, track, metrics, viewportSize, new Point(x, y));
                    }
                }
            }
        }

        private static void DrawHoverPreview(DrawingContext dc, JudgementLineEditorViewModel editor, TrackViewModel track, Size viewportSize, JudgementLineEditorRenderMath.ViewMetrics metrics, double width, double height, double centerX, double centerY, int currentTick)
        {
            if (!editor.HasHoverPreview || !Enum.TryParse<NoteKind>(editor.CurrentNoteKind, out var kind))
            {
                return;
            }

            double y = JudgementLineEditorRenderMath.CalculateHoverY(editor.Timeline, track, currentTick, editor.HoverHitTick, viewportSize, editor.ViewZoom, centerY);
            double x = centerX + editor.HoverChartX * metrics.PixelsPerChartUnit;
            double previewBounds = metrics.NotePixelWidth + 12;
            if (x < -previewBounds || x > width + previewBounds || y < -previewBounds || y > height + previewBounds)
            {
                return;
            }

            DrawPreviewNote(dc, kind, metrics.NotePixelWidth, x, y, 0.4);
            double selectionSize = metrics.NotePixelWidth + 6;
            dc.DrawRectangle(null, HoverPreviewPen, new Rect(x - selectionSize / 2, y - selectionSize / 2, selectionSize, selectionSize));
        }

        private static void DrawRenderedNote(DrawingContext dc, JudgementLineEditorViewModel editor, TrackViewModel track, NoteViewModel note, Size viewportSize, JudgementLineEditorRenderMath.ViewMetrics metrics, int currentTick, double centerX, double centerY, double judgementLineY, double opacityMultiplier)
        {
            bool isHold = note.CurrentNoteKind == NoteKind.Hold;
            bool isForwardFlow = (note.Model.CustomSpeed ?? 1.0) >= 0;

            if (isHold)
            {
                Rect clipRect = isForwardFlow
                    ? new Rect(-100000, -100000, 200000, 100000 + judgementLineY)
                    : new Rect(-100000, judgementLineY, 200000, 100000);
                dc.PushClip(new RectangleGeometry(clipRect));
            }

            dc.PushTransform(new TranslateTransform(centerX, centerY));
            dc.PushTransform(new RotateTransform(note.CurrentRotation));
            dc.PushTransform(new ScaleTransform(note.CurrentScaleX, note.CurrentScaleY));
            dc.PushOpacity((note.CurrentOpacity / 100.0) * opacityMultiplier);

            if (isHold)
            {
                bool isHoldPassed = currentTick >= note.HitTime;
                double holdPixelLength = JudgementLineEditorRenderMath.CalculateHoldLength(editor.Timeline, track, note, viewportSize, editor.ViewZoom);
                double notePixelWidth = metrics.NotePixelWidth;
                double partHeight = notePixelWidth * (50.0 / 989.0);
                if (holdPixelLength < partHeight)
                {
                    holdPixelLength = partHeight;
                }

                double bodyHeight = Math.Max(0, holdPixelLength - partHeight);
                Rect headRect = new Rect(-notePixelWidth / 2, -partHeight / 2, notePixelWidth, partHeight);
                Rect tailRect = new Rect(-notePixelWidth / 2, -holdPixelLength - partHeight / 2, notePixelWidth, partHeight);
                Rect bodyRect = new Rect(-notePixelWidth / 2, -holdPixelLength + partHeight / 2 - 1.0, notePixelWidth, bodyHeight + 2.0);

                if (!isForwardFlow)
                {
                    dc.PushTransform(new ScaleTransform(1, -1));
                }

                dc.DrawRectangle(HoldBodyBrush, null, bodyRect);
                dc.DrawRectangle(HoldTailBrush, null, tailRect);
                if (!isHoldPassed)
                {
                    dc.DrawRectangle(HoldHeadBrush, null, headRect);
                }

                if (!isForwardFlow)
                {
                    dc.Pop();
                }
            }
            else
            {
                DrawNoteImage(dc, note.CurrentNoteKind, metrics.NotePixelWidth, opacity: 1.0);
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

        private static void DrawPreviewNote(DrawingContext dc, NoteKind kind, double width, double centerX, double centerY, double opacity)
        {
            dc.PushTransform(new TranslateTransform(centerX, centerY));
            DrawNoteImage(dc, kind, width, opacity);
            dc.Pop();
        }

        private static void DrawHoldTailHandle(DrawingContext dc, NoteViewModel note, JudgementLineEditorViewModel editor, TrackViewModel track, JudgementLineEditorRenderMath.ViewMetrics metrics, Size viewportSize, Point noteCenter)
        {
            Rect handleRect = GetHoldTailHandleRect(note, editor, track, metrics, viewportSize, noteCenter);
            dc.DrawEllipse(HoldTailHandleBrush, HoldTailHandlePen,
                new Point(handleRect.X + handleRect.Width / 2.0, handleRect.Y + handleRect.Height / 2.0),
                handleRect.Width / 2.0,
                handleRect.Height / 2.0);
        }

        private static bool ShouldRenderNote(NoteViewModel note, int currentTick)
        {
            bool isHold = note.CurrentNoteKind == NoteKind.Hold;
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

        private static Point GetNoteCenter(NoteViewModel note, JudgementLineEditorViewModel editor, TrackViewModel track, JudgementLineEditorRenderMath.ViewMetrics metrics, Size viewportSize, double centerX, double centerY, int currentTick)
        {
            return new Point(
                centerX + note.CurrentOffsetX * metrics.PixelsPerChartUnit,
            JudgementLineEditorRenderMath.CalculateNoteY(editor.Timeline, track, note, currentTick, viewportSize, editor.ViewZoom, centerY));
        }

        private static Rect GetNoteBodyRect(NoteViewModel note, JudgementLineEditorViewModel editor, TrackViewModel track, JudgementLineEditorRenderMath.ViewMetrics metrics, Size viewportSize, Point noteCenter)
        {
            double scaleX = Math.Abs(note.CurrentScaleX);
            double scaleY = Math.Abs(note.CurrentScaleY);
            double notePixelWidth = metrics.NotePixelWidth * Math.Max(scaleX, 0.1);
            double notePixelHeight = notePixelWidth * (GetNoteImage(note.CurrentNoteKind).Height / GetNoteImage(note.CurrentNoteKind).Width) * Math.Max(scaleY, 0.1);

            if (note.CurrentNoteKind != NoteKind.Hold)
            {
                return new Rect(noteCenter.X - notePixelWidth / 2.0, noteCenter.Y - notePixelHeight / 2.0, notePixelWidth, notePixelHeight);
            }

            double holdLength = JudgementLineEditorRenderMath.CalculateHoldLength(editor.Timeline, track, note, viewportSize, editor.ViewZoom) * Math.Max(scaleY, 0.1);
            bool isForwardFlow = (note.Model.CustomSpeed ?? 1.0) >= 0;
            double top = isForwardFlow ? noteCenter.Y - holdLength - notePixelHeight / 2.0 : noteCenter.Y - notePixelHeight / 2.0;
            double bottom = isForwardFlow ? noteCenter.Y + notePixelHeight / 2.0 : noteCenter.Y + holdLength + notePixelHeight / 2.0;
            return new Rect(noteCenter.X - notePixelWidth / 2.0, top, notePixelWidth, bottom - top);
        }

        private static Rect GetHoldTailHandleRect(NoteViewModel note, JudgementLineEditorViewModel editor, TrackViewModel track, JudgementLineEditorRenderMath.ViewMetrics metrics, Size viewportSize, Point noteCenter)
        {
            double scaleX = Math.Abs(note.CurrentScaleX);
            double scaleY = Math.Abs(note.CurrentScaleY);
            double holdLength = JudgementLineEditorRenderMath.CalculateHoldLength(editor.Timeline, track, note, viewportSize, editor.ViewZoom) * Math.Max(scaleY, 0.1);
            bool isForwardFlow = (note.Model.CustomSpeed ?? 1.0) >= 0;
            double handleSize = Math.Max(12.0, metrics.NotePixelWidth * 0.22 * Math.Max(scaleX, scaleY));
            double handleCenterY = noteCenter.Y + (isForwardFlow ? -holdLength : holdLength);
            return new Rect(noteCenter.X - handleSize / 2.0, handleCenterY - handleSize / 2.0, handleSize, handleSize);
        }

        private static ImageSource GetNoteImage(NoteKind kind)
        {
            return kind switch
            {
                NoteKind.Tap => TapImage,
                NoteKind.Drag => DragImage,
                NoteKind.Hold => HoldImage,
                NoteKind.Flick => FlickImage,
                _ => TapImage,
            };
        }

        private static void DrawNoteImage(DrawingContext dc, NoteKind kind, double width, double opacity)
        {
            ImageSource image = GetNoteImage(kind);

            double aspectRatio = image.Height / image.Width;
            double height = width * aspectRatio;
            dc.PushOpacity(opacity);
            dc.DrawImage(image, new Rect(-width / 2, -height / 2, width, height));
            dc.Pop();
        }

        private static FormattedText CreateLabel(string text, double fontSize)
        {
            return new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelTypeface,
                fontSize,
                LabelBrush,
                1.25);
        }
    }
}