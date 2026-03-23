using Axphi.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Axphi.ViewModels
{
    public partial class NoteSelectionPanelViewModel : ObservableObject
    {
        private readonly TimelineViewModel _timeline;
        private readonly List<NoteViewModel> _selectedNotes = new();
        private bool _isSyncing;

        [ObservableProperty]
        private bool _hasSelection;

        [ObservableProperty]
        private bool _isSingleSelection;

        [ObservableProperty]
        private int _selectedCount;

        [ObservableProperty]
        private string _selectionTitle = "Note";

        [ObservableProperty]
        private string _selectionModeHint = string.Empty;

        [ObservableProperty]
        private NoteViewModel? _singleSelectedNote;

        [ObservableProperty]
        private string _currentNoteKind = nameof(NoteKind.Tap);

        [ObservableProperty]
        private double _currentOffsetX;

        [ObservableProperty]
        private double _currentOffsetY;

        [ObservableProperty]
        private double _currentScaleX = 1.0;

        [ObservableProperty]
        private double _currentScaleY = 1.0;

        [ObservableProperty]
        private double _currentRotation;

        [ObservableProperty]
        private double _currentOpacity = 100.0;

        [ObservableProperty]
        private bool? _hasCustomSpeed;

        [ObservableProperty]
        private double _currentCustomSpeed;

        public NoteSelectionPanelViewModel(TimelineViewModel timeline)
        {
            _timeline = timeline;
        }

        public ICommand? AddNoteKindKeyframeCommand => SingleSelectedNote?.AddNoteKindKeyframeCommand;
        public ICommand? AddPositionKeyframeCommand => SingleSelectedNote?.AddPositionKeyframeCommand;
        public ICommand? AddScaleKeyframeCommand => SingleSelectedNote?.AddScaleKeyframeCommand;
        public ICommand? AddRotationKeyframeCommand => SingleSelectedNote?.AddRotationKeyframeCommand;
        public ICommand? AddOpacityKeyframeCommand => SingleSelectedNote?.AddOpacityKeyframeCommand;

        public bool CanEditKeyframes => IsSingleSelection && SingleSelectedNote != null;
        public bool HasResolvedCustomSpeed => HasCustomSpeed == true;

        private void NotifyKeyframeCommandBindingsChanged()
        {
            OnPropertyChanged(nameof(AddNoteKindKeyframeCommand));
            OnPropertyChanged(nameof(AddPositionKeyframeCommand));
            OnPropertyChanged(nameof(AddScaleKeyframeCommand));
            OnPropertyChanged(nameof(AddRotationKeyframeCommand));
            OnPropertyChanged(nameof(AddOpacityKeyframeCommand));
            OnPropertyChanged(nameof(CanEditKeyframes));
        }

        public void SyncSelection(IReadOnlyList<NoteViewModel> selectedNotes)
        {
            _selectedNotes.Clear();
            _selectedNotes.AddRange(selectedNotes);

            _isSyncing = true;

            HasSelection = _selectedNotes.Count > 0;
            SelectedCount = _selectedNotes.Count;
            IsSingleSelection = _selectedNotes.Count == 1;
            SingleSelectedNote = IsSingleSelection ? _selectedNotes[0] : null;

            if (!HasSelection)
            {
                SelectionTitle = "Note";
                SelectionModeHint = string.Empty;
                CurrentNoteKind = nameof(NoteKind.Tap);
                CurrentOffsetX = 0;
                CurrentOffsetY = 0;
                CurrentScaleX = 1.0;
                CurrentScaleY = 1.0;
                CurrentRotation = 0;
                CurrentOpacity = 100.0;
                HasCustomSpeed = false;
                CurrentCustomSpeed = 1.0;
                _isSyncing = false;
                NotifyKeyframeCommandBindingsChanged();
                OnPropertyChanged(nameof(HasResolvedCustomSpeed));
                return;
            }

            if (IsSingleSelection)
            {
                var note = SingleSelectedNote!;
                SelectionTitle = "Note";
                SelectionModeHint = string.Empty;
                CurrentNoteKind = note.CurrentNoteKind.ToString();
                CurrentOffsetX = note.CurrentOffsetX;
                CurrentOffsetY = note.CurrentOffsetY;
                CurrentScaleX = note.CurrentScaleX;
                CurrentScaleY = note.CurrentScaleY;
                CurrentRotation = note.CurrentRotation;
                CurrentOpacity = note.CurrentOpacity;
                HasCustomSpeed = note.HasCustomSpeed;
                CurrentCustomSpeed = note.CurrentCustomSpeed;
            }
            else
            {
                SelectionTitle = $"{SelectedCount} Notes";
                SelectionModeHint = "Delta";
                CurrentNoteKind = _selectedNotes.Select(note => note.CurrentNoteKind).Distinct().Count() == 1
                    ? _selectedNotes[0].CurrentNoteKind.ToString()
                    : "Mixed";
                CurrentOffsetX = 0;
                CurrentOffsetY = 0;
                CurrentScaleX = 0;
                CurrentScaleY = 0;
                CurrentRotation = 0;
                CurrentOpacity = 0;

                bool allEnabled = _selectedNotes.All(note => note.HasCustomSpeed);
                bool allDisabled = _selectedNotes.All(note => !note.HasCustomSpeed);
                HasCustomSpeed = allEnabled ? true : allDisabled ? false : null;
                CurrentCustomSpeed = 0;
            }

            _isSyncing = false;
            NotifyKeyframeCommandBindingsChanged();
            OnPropertyChanged(nameof(HasResolvedCustomSpeed));
        }

        partial void OnCurrentNoteKindChanged(string value)
        {
            if (_isSyncing || !HasSelection || value == "Mixed" || !Enum.TryParse<NoteKind>(value, out var kind))
            {
                return;
            }

            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            foreach (var note in _selectedNotes)
            {
                note.ApplyNoteKindAbsolute(kind);
            }
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        partial void OnCurrentOffsetXChanged(double oldValue, double newValue)
        {
            if (_isSyncing || !HasSelection) return;
            if (IsSingleSelection)
            {
                ApplyAbsolute(note => note.ApplyPositionAbsolute(CurrentOffsetX, CurrentOffsetY));
                return;
            }

            ApplyVectorDelta(newValue - oldValue, 0, (note, deltaX, deltaY) => note.ApplyPositionDelta(deltaX, deltaY));
        }

        partial void OnCurrentOffsetYChanged(double oldValue, double newValue)
        {
            if (_isSyncing || !HasSelection) return;
            if (IsSingleSelection)
            {
                ApplyAbsolute(note => note.ApplyPositionAbsolute(CurrentOffsetX, CurrentOffsetY));
                return;
            }

            ApplyVectorDelta(0, newValue - oldValue, (note, deltaX, deltaY) => note.ApplyPositionDelta(deltaX, deltaY));
        }

        partial void OnCurrentScaleXChanged(double oldValue, double newValue)
        {
            if (_isSyncing || !HasSelection) return;
            if (IsSingleSelection)
            {
                ApplyAbsolute(note => note.ApplyScaleAbsolute(CurrentScaleX, CurrentScaleY));
                return;
            }

            ApplyVectorDelta(newValue - oldValue, 0, (note, deltaX, deltaY) => note.ApplyScaleDelta(deltaX, deltaY));
        }

        partial void OnCurrentScaleYChanged(double oldValue, double newValue)
        {
            if (_isSyncing || !HasSelection) return;
            if (IsSingleSelection)
            {
                ApplyAbsolute(note => note.ApplyScaleAbsolute(CurrentScaleX, CurrentScaleY));
                return;
            }

            ApplyVectorDelta(0, newValue - oldValue, (note, deltaX, deltaY) => note.ApplyScaleDelta(deltaX, deltaY));
        }

        partial void OnCurrentRotationChanged(double oldValue, double newValue)
        {
            if (_isSyncing || !HasSelection) return;
            if (IsSingleSelection)
            {
                ApplyAbsolute(note => note.ApplyRotationAbsolute(newValue));
                return;
            }

            ApplyScalarDelta(newValue - oldValue, (note, delta) => note.ApplyRotationDelta(delta));
        }

        partial void OnCurrentOpacityChanged(double oldValue, double newValue)
        {
            if (_isSyncing || !HasSelection) return;
            if (IsSingleSelection)
            {
                ApplyAbsolute(note => note.ApplyOpacityAbsolute(newValue));
                return;
            }

            ApplyScalarDelta(newValue - oldValue, (note, delta) => note.ApplyOpacityDelta(delta));
        }

        partial void OnHasCustomSpeedChanged(bool? value)
        {
            if (_isSyncing || !HasSelection || value == null) return;

            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            foreach (var note in _selectedNotes)
            {
                note.ApplyHasCustomSpeed(value.Value, IsSingleSelection ? CurrentCustomSpeed : 1.0);
            }
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        partial void OnCurrentCustomSpeedChanged(double oldValue, double newValue)
        {
            if (_isSyncing || !HasSelection || HasCustomSpeed != true) return;

            if (IsSingleSelection)
            {
                ApplyAbsolute(note => note.ApplyCustomSpeedAbsolute(newValue));
                return;
            }

            ApplyScalarDelta(newValue - oldValue, (note, delta) => note.ApplyCustomSpeedDelta(delta));
        }

        private void ApplyAbsolute(Action<NoteViewModel> applyAction)
        {
            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            foreach (var note in _selectedNotes)
            {
                applyAction(note);
            }
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        private void ApplyVectorDelta(double deltaX, double deltaY, Action<NoteViewModel, double, double> applyAction)
        {
            if (Math.Abs(deltaX) < double.Epsilon && Math.Abs(deltaY) < double.Epsilon)
            {
                return;
            }

            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            foreach (var note in _selectedNotes)
            {
                applyAction(note, deltaX, deltaY);
            }
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

        private void ApplyScalarDelta(double delta, Action<NoteViewModel, double> applyAction)
        {
            if (Math.Abs(delta) < double.Epsilon)
            {
                return;
            }

            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());
            foreach (var note in _selectedNotes)
            {
                applyAction(note, delta);
            }
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }
    }
}