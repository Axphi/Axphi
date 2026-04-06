using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Axphi.Data;
using Axphi.Data.KeyFrames;
using Axphi.Utilities;


namespace Axphi.ViewModels
{
    public partial class BpmTrackViewModel : ObservableObject
    {
        private readonly Chart _chart;

        public TimelineViewModel _timeline;

        public TimelineViewModel Timeline => _timeline;

        private bool _isSyncing;

        public ObservableCollection<KeyFrameUIWrapper<double>> UIBpmKeyframes { get; } = new();

        [ObservableProperty]
        private double _currentBpm;

        public BpmTrackViewModel(Chart chart, TimelineViewModel timeline)
        {
            _chart = chart;
            _timeline = timeline;

            CurrentBpm = _chart.InitialBpm;

            InitializeUiKeyframes();

            WeakReferenceMessenger.Default.Register<BpmTrackViewModel, KeyframesNeedSortMessage>(this, (recipient, _) =>
            {
                recipient._chart.BpmKeyFrames.Sort((a, b) => a.Time.CompareTo(b.Time));
            });
        }

        public void SyncValuesToTime(int currentTick)
        {
            _isSyncing = true;

            CurrentBpm = KeyFrameUtils.GetStepValueAtTick(_chart.BpmKeyFrames, currentTick, _chart.InitialBpm);

            _isSyncing = false;
        }

        partial void OnCurrentBpmChanged(double value)
        {
            if (_isSyncing)
            {
                return;
            }

            WeakReferenceMessenger.Default.Send(new ForcePausePlaybackMessage());

            if (_chart.BpmKeyFrames.Count == 0)
            {
                double currentExactTick = _timeline.GetExactTick();
                UpdateInitialBpmAndSyncPlayhead(value, currentExactTick);
                return;
            }

            AddBpmKeyframe();
        }

        [RelayCommand]
        private void AddBpmKeyframe()
        {
            int currentTick = _timeline.GetCurrentTick();
            var existingWrapper = UIBpmKeyframes.FirstOrDefault(w => w.Model.Time == currentTick);

            if (existingWrapper != null)
            {
                existingWrapper.Model.Value = CurrentBpm;
            }
            else
            {
                var newFrame = new KeyFrame<double>() { Time = currentTick, Value = CurrentBpm };

                _chart.BpmKeyFrames.Add(newFrame);

                _chart.BpmKeyFrames.Sort((a, b) => a.Time.CompareTo(b.Time));

                UIBpmKeyframes.Add(new KeyFrameUIWrapper<double>(newFrame, _timeline));
            }

            NotifyBpmChanged();
        }

        private void InitializeUiKeyframes()
        {
            if (_chart.BpmKeyFrames == null)
            {
                return;
            }

            foreach (var keyframe in _chart.BpmKeyFrames)
            {
                UIBpmKeyframes.Add(new KeyFrameUIWrapper<double>(keyframe, _timeline));
            }
        }

        private void UpdateInitialBpmAndSyncPlayhead(double bpm, double currentExactTick)
        {
            _chart.InitialBpm = bpm;

            // 保持当前播放头所在 Tick 不变，避免 BPM 变化后游标跳变。
            SyncPlayheadToTick(currentExactTick);
            _timeline.AudioTrack?.UpdatePixels();
        }

        private void NotifyBpmChanged()
        {
            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
            _timeline.AudioTrack?.UpdatePixels();
        }

        private void SyncPlayheadToTick(double targetExactTick)
        {
            double relativeTick = Math.Max(0, targetExactTick);

            double newSeconds = TimeTickConverter.TickToTime(relativeTick, _chart.BpmKeyFrames, _chart.InitialBpm);

            _timeline.CurrentPlayTimeSeconds = newSeconds;

            WeakReferenceMessenger.Default.Send(new ForceSeekMessage(newSeconds));

            WeakReferenceMessenger.Default.Send(new JudgementLinesChangedMessage());
        }

    }


}