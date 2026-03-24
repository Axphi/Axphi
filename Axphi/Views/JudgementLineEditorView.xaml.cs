using Axphi.ViewModels;
using Axphi.Utilities;
using CommunityToolkit.Mvvm.Messaging;
using System.Linq;
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
            BoxSelect,
        }

        private bool _isPanning;
        private Point _lastPanPoint;
        private EditorDragMode _dragMode;
        private NoteViewModel? _dragNote;
        private Vector _dragAnchorOffset;
        private Point _dragStartPoint;
        private Rect _currentSelectionRect;

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
            if (_dragMode != EditorDragMode.None)
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

            if (vm.ScrollTimeByWheelDelta(e.Delta, EditorCanvas.RenderSize))
            {
                EditorCanvas.InvalidateVisual();
                e.Handled = true;
            }
        }

        private void EditorRoot_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not JudgementLineEditorViewModel vm)
            {
                return;
            }

            if ((e.Key == Key.Delete || (e.Key == Key.X && Keyboard.Modifiers == ModifierKeys.None)) && vm.IsVisible)
            {
                bool hasSelectedNotes = vm.ActiveTrack?.UINotes.Any(note => note.IsSelected) == true;
                if (hasSelectedNotes)
                {
                    vm.Timeline.DeleteSelectedKeyframesCommand.Execute(null);
                    EditorCanvas.InvalidateVisual();
                }

                e.Handled = true;
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

            _dragMode = EditorDragMode.BoxSelect;
            _dragStartPoint = point;
            _currentSelectionRect = new Rect(point, point);
            EditorCanvas.SelectionRect = _currentSelectionRect;
            EditorCanvas.CaptureMouse();
            EditorCanvas.Focus();

            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                foreach (var note in vm.ActiveTrack?.UINotes ?? Enumerable.Empty<NoteViewModel>())
                {
                    note.IsSelected = false;
                }
                EditorCanvas.InvalidateVisual();
            }
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

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                note.IsSelected = !note.IsSelected;
            }
            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                note.IsSelected = true;
            }
            else
            {
                foreach (var n in vm.ActiveTrack?.UINotes ?? Enumerable.Empty<NoteViewModel>())
                {
                    n.IsSelected = false;
                }
                note.IsSelected = true;
            }

            if (!note.IsSelected)
            {
                return;
            }

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
            if (_dragMode == EditorDragMode.BoxSelect)
            {
                double x = Math.Min(_dragStartPoint.X, pointerPoint.X);
                double y = Math.Min(_dragStartPoint.Y, pointerPoint.Y);
                double width = Math.Abs(_dragStartPoint.X - pointerPoint.X);
                double height = Math.Abs(_dragStartPoint.Y - pointerPoint.Y);
                _currentSelectionRect = new Rect(x, y, width, height);
                EditorCanvas.SelectionRect = _currentSelectionRect;

                bool addToSelection = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
                EditorCanvas.SelectNotesInRect(_currentSelectionRect, addToSelection);
                
                EditorCanvas.InvalidateVisual();
                return;
            }

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
                double targetOffsetX = Math.Clamp((desiredAnchorX - centerX) / metrics.PixelsPerChartUnit, -8.0, 8.0);
                double tickResolveY = desiredAnchorY - (_dragNote.CurrentOffsetY * metrics.PixelsPerChartUnit);
                int targetHitTick = vm.ResolveEditorTick(tickResolveY, EditorCanvas.RenderSize, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));

                double deltaX = targetOffsetX - _dragNote.CurrentOffsetX;
                int deltaTick = targetHitTick - _dragNote.HitTime;

                if (Math.Abs(deltaX) < 0.0001 && deltaTick == 0)
                {
                    return;
                }

                var selectedNotes = vm.ActiveTrack?.UINotes.Where(n => n.IsSelected).ToList();
                if (selectedNotes != null)
                {
                     foreach (var note in selectedNotes)
                     {
                         double newX = Math.Clamp(note.CurrentOffsetX + deltaX, -8.0, 8.0);
                         int newTick = Math.Max(0, note.HitTime + deltaTick);

                         if (Math.Abs(note.CurrentOffsetX - newX) > 0.0001)
                             note.ApplyPositionAbsolute(newX, note.CurrentOffsetY);
                         
                         if (note.HitTime != newTick)
                             note.HitTime = newTick;
                     }
                }

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
            if (_dragMode == EditorDragMode.BoxSelect)
            {
                if (_currentSelectionRect.Width < 5 && _currentSelectionRect.Height < 5)
                {
                    if (DataContext is JudgementLineEditorViewModel vm
                        && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    {
                        vm.TryAddNoteAtPoint(_dragStartPoint, EditorCanvas.RenderSize);
                    }
                }

                EditorCanvas.SelectionRect = Rect.Empty;
                _currentSelectionRect = Rect.Empty;
                // Don't select anything else here, as Box Select logic ran during Move.
            }

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