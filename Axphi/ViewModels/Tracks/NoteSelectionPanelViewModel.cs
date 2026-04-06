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
        private readonly IMessenger _messenger;
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
        private double _currentAnchorX;

        [ObservableProperty]
        private double _currentAnchorY;

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

        public NoteSelectionPanelViewModel(TimelineViewModel timeline, IMessenger messenger)
        {
            _timeline = timeline;
            _messenger = messenger;
        }

        public ICommand? AddNoteKindKeyframeCommand => SingleSelectedNote?.AddNoteKindKeyframeCommand;
        public ICommand? AddAnchorKeyframeCommand => SingleSelectedNote?.AddAnchorKeyframeCommand;
        public ICommand? AddPositionKeyframeCommand => SingleSelectedNote?.AddPositionKeyframeCommand;
        public ICommand? AddScaleKeyframeCommand => SingleSelectedNote?.AddScaleKeyframeCommand;
        public ICommand? AddRotationKeyframeCommand => SingleSelectedNote?.AddRotationKeyframeCommand;
        public ICommand? AddOpacityKeyframeCommand => SingleSelectedNote?.AddOpacityKeyframeCommand;

        public bool CanEditKeyframes => IsSingleSelection && SingleSelectedNote != null;
        public bool HasResolvedCustomSpeed => HasCustomSpeed == true;

        private void NotifyKeyframeCommandBindingsChanged()
        {
            OnPropertyChanged(nameof(AddNoteKindKeyframeCommand));
            OnPropertyChanged(nameof(AddAnchorKeyframeCommand));
            OnPropertyChanged(nameof(AddPositionKeyframeCommand));
            OnPropertyChanged(nameof(AddScaleKeyframeCommand));
            OnPropertyChanged(nameof(AddRotationKeyframeCommand));
            OnPropertyChanged(nameof(AddOpacityKeyframeCommand));
            OnPropertyChanged(nameof(CanEditKeyframes));
        }

        public void SyncSelection()
        {
            var selectedNotes = GetSelectedNotesSnapshot();
            _isSyncing = true;

            HasSelection = selectedNotes.Count > 0;
            SelectedCount = selectedNotes.Count;
            IsSingleSelection = selectedNotes.Count == 1;
            SingleSelectedNote = IsSingleSelection ? selectedNotes[0] : null;

            if (!HasSelection)
            {
                SelectionTitle = "Note";
                SelectionModeHint = string.Empty;
                CurrentNoteKind = nameof(NoteKind.Tap);
                CurrentAnchorX = 0;
                CurrentAnchorY = 0;
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
                CurrentAnchorX = note.CurrentAnchorX;
                CurrentAnchorY = note.CurrentAnchorY;
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
                CurrentNoteKind = selectedNotes.Select(note => note.CurrentNoteKind).Distinct().Count() == 1
                    ? selectedNotes[0].CurrentNoteKind.ToString()
                    : "Mixed";
                CurrentAnchorX = 0;
                CurrentAnchorY = 0;
                CurrentOffsetX = 0;
                CurrentOffsetY = 0;
                CurrentScaleX = 0;
                CurrentScaleY = 0;
                CurrentRotation = 0;
                CurrentOpacity = 0;

                bool allEnabled = selectedNotes.All(note => note.HasCustomSpeed);
                bool allDisabled = selectedNotes.All(note => !note.HasCustomSpeed);
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

            _messenger.Send(new ForcePausePlaybackMessage());
            foreach (var note in GetSelectedNotesSnapshot())
            {
                note.ApplyNoteKindAbsolute(kind);
            }
            _messenger.Send(new JudgementLinesChangedMessage());
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

        partial void OnCurrentAnchorXChanged(double oldValue, double newValue)
        {
            if (_isSyncing || !HasSelection) return;
            if (IsSingleSelection)
            {
                ApplyAbsolute(note => note.ApplyAnchorAbsolute(CurrentAnchorX, CurrentAnchorY));
                return;
            }

            ApplyVectorDelta(newValue - oldValue, 0, (note, deltaX, deltaY) => note.ApplyAnchorDelta(deltaX, deltaY));
        }

        partial void OnCurrentAnchorYChanged(double oldValue, double newValue)
        {
            if (_isSyncing || !HasSelection) return;
            if (IsSingleSelection)
            {
                ApplyAbsolute(note => note.ApplyAnchorAbsolute(CurrentAnchorX, CurrentAnchorY));
                return;
            }

            ApplyVectorDelta(0, newValue - oldValue, (note, deltaX, deltaY) => note.ApplyAnchorDelta(deltaX, deltaY));
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

            var selectedNotes = GetSelectedNotesSnapshot();
            if (selectedNotes.Count == 0) return;

            _messenger.Send(new ForcePausePlaybackMessage());
            foreach (var note in selectedNotes)
            {
                note.ApplyHasCustomSpeed(value.Value, IsSingleSelection ? CurrentCustomSpeed : 1.0);
            }
            _messenger.Send(new JudgementLinesChangedMessage());
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
            var selectedNotes = GetSelectedNotesSnapshot();
            if (selectedNotes.Count == 0)
            {
                return;
            }

            _messenger.Send(new ForcePausePlaybackMessage());
            foreach (var note in selectedNotes)
            {
                applyAction(note);
            }
            _messenger.Send(new JudgementLinesChangedMessage());
        }

        private void ApplyVectorDelta(double deltaX, double deltaY, Action<NoteViewModel, double, double> applyAction)
        {
            if (Math.Abs(deltaX) < double.Epsilon && Math.Abs(deltaY) < double.Epsilon)
            {
                return;
            }

            var selectedNotes = GetSelectedNotesSnapshot();
            if (selectedNotes.Count == 0)
            {
                return;
            }

            _messenger.Send(new ForcePausePlaybackMessage());
            foreach (var note in selectedNotes)
            {
                applyAction(note, deltaX, deltaY);
            }
            _messenger.Send(new JudgementLinesChangedMessage());
        }

        private void ApplyScalarDelta(double delta, Action<NoteViewModel, double> applyAction)
        {
            if (Math.Abs(delta) < double.Epsilon)
            {
                return;
            }

            var selectedNotes = GetSelectedNotesSnapshot();
            if (selectedNotes.Count == 0)
            {
                return;
            }

            _messenger.Send(new ForcePausePlaybackMessage());
            foreach (var note in selectedNotes)
            {
                applyAction(note, delta);
            }
            _messenger.Send(new JudgementLinesChangedMessage());
        }

        private List<NoteViewModel> GetSelectedNotesSnapshot()
        {
            return _timeline.Tracks
                .SelectMany(track => track.UINotes)
                .Where(note => note.IsSelected)
                .ToList();
        }
    }
}