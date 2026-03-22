using Axphi.ViewModels;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;

namespace Axphi.Views
{
    public partial class JudgementLineEditorView : UserControl
    {
        private enum EditorDragMode
        {
            None,
            MoveNote,
            ResizeHoldTail,
        }

        private bool _isPanning;
        private Point _lastPanPoint;
        private EditorDragMode _dragMode;
        private NoteViewModel? _dragNote;
        private Vector _dragAnchorOffset;

        public JudgementLineEditorView()
        {
            InitializeComponent();
            IsVisibleChanged += JudgementLineEditorView_IsVisibleChanged;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            FocusEditorCanvas();
        }

        private void EditorCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void EditorCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
        }

        private void EditorCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning || DataContext is not JudgementLineEditorViewModel vm)
            {
                return;
            }

            Point point = e.GetPosition(EditorCanvas);
            if (_dragMode != EditorDragMode.None && _dragNote != null)
            {
                UpdateDraggedNote(vm, point);
                EditorCanvas.InvalidateVisual();
                e.Handled = true;
                return;
            }

            UpdateCanvasCursor(point);
        }

        private void EditorRoot_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (DataContext is not JudgementLineEditorViewModel vm)
                {
                    return;
                }

                if (IsMouseOnHud(e.OriginalSource as DependencyObject)
                    || e.OriginalSource is DependencyObject dependencyObject && FindVisualParent<ComboBox>(dependencyObject) != null
                    || e.OriginalSource is DependencyObject buttonObject && FindVisualParent<Button>(buttonObject) != null)
                {
                    return;
                }

                HandleCanvasLeftButtonDown(vm, e.GetPosition(EditorCanvas));
                e.Handled = true;
                return;
            }

            if (e.ChangedButton != MouseButton.Middle)
            {
                return;
            }

            _isPanning = true;
            _lastPanPoint = e.GetPosition(EditorRoot);
            EditorRoot.CaptureMouse();
            e.Handled = true;
        }

        private void EditorRoot_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (DataContext is not JudgementLineEditorViewModel vm)
            {
                return;
            }

            if (!_isPanning)
            {
                if (_dragMode == EditorDragMode.None)
                {
                    UpdateHoverPreviewFromRoot(e, vm);
                }

                return;
            }

            Point point = e.GetPosition(EditorRoot);
            vm.PanBy(point.X - _lastPanPoint.X, point.Y - _lastPanPoint.Y);
            _lastPanPoint = point;
            EditorCanvas.InvalidateVisual();
            e.Handled = true;
        }

        private void EditorRoot_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (_dragMode != EditorDragMode.None)
                {
                    EndNoteInteraction();
                    e.Handled = true;
                }

                return;
            }

            if (e.ChangedButton != MouseButton.Middle)
            {
                return;
            }

            _isPanning = false;
            EditorRoot.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void EditorRoot_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (DataContext is not JudgementLineEditorViewModel vm)
            {
                return;
            }

            if (e.OriginalSource is DependencyObject dependencyObject && FindVisualParent<ComboBox>(dependencyObject) != null)
            {
                return;
            }

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                vm.ZoomAt(e.Delta, e.GetPosition(EditorCanvas), EditorCanvas.RenderSize);
                EditorCanvas.InvalidateVisual();
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                vm.PanBy(e.Delta * 0.2, 0);
                EditorCanvas.InvalidateVisual();
                e.Handled = true;
                return;
            }

            vm.PanBy(0, e.Delta * 0.2);
            EditorCanvas.InvalidateVisual();
            e.Handled = true;
        }

        private void EditorRoot_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not JudgementLineEditorViewModel vm)
            {
                return;
            }

            if (e.OriginalSource is DependencyObject dependencyObject && FindVisualParent<ComboBox>(dependencyObject) != null)
            {
                return;
            }

            if (Keyboard.Modifiers != ModifierKeys.None)
            {
                return;
            }

            if (e.Key == Key.Escape && vm.CancelPendingHoldPlacement())
            {
                vm.ClearHoverPreview();
                EditorCanvas.InvalidateVisual();
                e.Handled = true;
                return;
            }

            string? nextKind = e.Key switch
            {
                Key.Q => nameof(Axphi.Data.NoteKind.Tap),
                Key.W => nameof(Axphi.Data.NoteKind.Hold),
                Key.E => nameof(Axphi.Data.NoteKind.Drag),
                Key.R => nameof(Axphi.Data.NoteKind.Flick),
                _ => null,
            };

            if (nextKind == null || vm.CurrentNoteKind == nextKind)
            {
                return;
            }

            vm.CurrentNoteKind = nextKind;
            EditorCanvas.InvalidateVisual();
            e.Handled = true;
        }

        private void JudgementLineEditorView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                FocusEditorCanvas();
            }
        }

        private void EditorRoot_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_dragMode == EditorDragMode.None)
            {
                EditorCanvas.Cursor = Cursors.Arrow;
            }

            if (DataContext is JudgementLineEditorViewModel vm && vm.ClearHoverPreview())
            {
                EditorCanvas.InvalidateVisual();
            }
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                {
                    return parent;
                }

                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            }

            return null;
        }

        private void UpdateHoverPreviewFromRoot(MouseEventArgs e, JudgementLineEditorViewModel vm)
        {
            if (IsMouseOnHud(e.OriginalSource as DependencyObject))
            {
                return;
            }

            Point point = e.GetPosition(EditorCanvas);
            if (vm.UpdateHoverPreview(point, EditorCanvas.RenderSize))
            {
                EditorCanvas.InvalidateVisual();
            }
        }

        private void HandleCanvasLeftButtonDown(JudgementLineEditorViewModel vm, Point point)
        {
            if (vm.HasPendingHoldPlacement)
            {
                vm.TryAddNoteAtPoint(point, EditorCanvas.RenderSize);
                EditorCanvas.InvalidateVisual();
                EditorCanvas.Focus();
                return;
            }

            if (EditorCanvas.TryHitTest(point, out var hitNote, out var targetKind, out var anchorPoint) && hitNote != null)
            {
                BeginNoteInteraction(vm, hitNote, targetKind, point, anchorPoint);
                return;
            }

            vm.TryAddNoteAtPoint(point, EditorCanvas.RenderSize);
            EditorCanvas.InvalidateVisual();
            EditorCanvas.Focus();
        }

        private bool IsMouseOnHud(DependencyObject? dependencyObject)
        {
            while (dependencyObject != null)
            {
                if (ReferenceEquals(dependencyObject, EditorHud))
                {
                    return true;
                }

                dependencyObject = System.Windows.Media.VisualTreeHelper.GetParent(dependencyObject);
            }

            return false;
        }

        private void FocusEditorCanvas()
        {
            Dispatcher.BeginInvoke(() =>
            {
                EditorRoot.Focus();
                EditorCanvas.Focus();
                Keyboard.Focus(EditorCanvas);
            }, DispatcherPriority.Input);
        }

        private void BeginNoteInteraction(JudgementLineEditorViewModel vm, NoteViewModel note, Axphi.Components.JudgementLineEditorCanvas.HitTargetKind targetKind, Point pointerPoint, Point anchorPoint)
        {
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            SelectSingleNote(vm, note);

            _dragNote = note;
            _dragMode = targetKind == Axphi.Components.JudgementLineEditorCanvas.HitTargetKind.HoldTailHandle
                ? EditorDragMode.ResizeHoldTail
                : EditorDragMode.MoveNote;
            _dragAnchorOffset = pointerPoint - anchorPoint;

            vm.ClearHoverPreview();
            EditorCanvas.CaptureMouse();
            EditorCanvas.Focus();
            UpdateCanvasCursor(pointerPoint);
            EditorCanvas.InvalidateVisual();
        }

        private void UpdateDraggedNote(JudgementLineEditorViewModel vm, Point pointerPoint)
        {
            if (_dragNote == null)
            {
                return;
            }

            var metrics = JudgementLineEditorRenderMath.CalculateMetrics(EditorCanvas.RenderSize, vm.ViewZoom);
            double centerX = EditorCanvas.RenderSize.Width / 2.0 + vm.PanX;
            double desiredAnchorX = pointerPoint.X - _dragAnchorOffset.X;
            double desiredAnchorY = pointerPoint.Y - _dragAnchorOffset.Y;

            if (_dragMode == EditorDragMode.MoveNote)
            {
                double newOffsetX = Math.Clamp((desiredAnchorX - centerX) / metrics.PixelsPerChartUnit, -8.0, 8.0);
                double tickResolveY = desiredAnchorY - (_dragNote.CurrentOffsetY * metrics.PixelsPerChartUnit);
                int hitTick = vm.ResolveEditorTick(tickResolveY, EditorCanvas.RenderSize, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));

                _dragNote.ApplyPositionAbsolute(newOffsetX, _dragNote.CurrentOffsetY);
                _dragNote.HitTime = hitTick;
                WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
                return;
            }

            if (_dragMode == EditorDragMode.ResizeHoldTail)
            {
                double tickResolveY = desiredAnchorY - (_dragNote.CurrentOffsetY * metrics.PixelsPerChartUnit);
                int endTick = vm.ResolveEditorTick(tickResolveY, EditorCanvas.RenderSize, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));

                _dragNote.HoldDuration = Math.Max(1, endTick - _dragNote.HitTime);
            }
        }

        private void EndNoteInteraction()
        {
            EditorCanvas.ReleaseMouseCapture();
            _dragMode = EditorDragMode.None;
            _dragNote = null;
            WeakReferenceMessenger.Default.Send(new NotesNeedSortMessage());
            EditorCanvas.Cursor = Cursors.Arrow;
            EditorCanvas.InvalidateVisual();
        }

        private void UpdateCanvasCursor(Point point)
        {
            if (EditorCanvas.TryHitTest(point, out _, out var targetKind, out _))
            {
                EditorCanvas.Cursor = targetKind == Axphi.Components.JudgementLineEditorCanvas.HitTargetKind.HoldTailHandle
                    ? Cursors.SizeNS
                    : Cursors.SizeAll;
                return;
            }

            EditorCanvas.Cursor = Cursors.Arrow;
        }

        private static void SelectSingleNote(JudgementLineEditorViewModel vm, NoteViewModel note)
        {
            vm.Timeline.EnterSubItemSelectionContext(note);
            vm.Timeline.ClearNoteSelection(note);
            note.IsSelected = true;
            vm.Timeline.RefreshNoteSelectionState(note.ParentTrack, note);
        }
    }
}