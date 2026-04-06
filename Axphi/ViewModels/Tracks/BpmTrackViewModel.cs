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
        private readonly IMessenger _messenger;

        public TimelineViewModel _timeline;

        public TimelineViewModel Timeline => _timeline;

        private bool _isSyncing;

        public ObservableCollection<KeyFrameUIWrapper<double>> UIBpmKeyframes { get; } = new();

        [ObservableProperty]
        private double _currentBpm;

        public BpmTrackViewModel(Chart chart, TimelineViewModel timeline, IMessenger messenger)
        {
            _chart = chart;
            _timeline = timeline;
            _messenger = messenger;

            CurrentBpm = _chart.InitialBpm;

            SyncBpmKeyframeProjection();

            _messenger.Register<BpmTrackViewModel, KeyframesNeedSortMessage>(this, (recipient, _) =>
            {
                recipient._chart.BpmKeyFrames.Sort((a, b) => a.Time.CompareTo(b.Time));
                recipient.SyncBpmKeyframeProjection();
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

            _messenger.Send(new ForcePausePlaybackMessage());

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
            var existingModel = _chart.BpmKeyFrames.FirstOrDefault(frame => frame.Time == currentTick);

            if (existingModel != null)
            {
                existingModel.Value = CurrentBpm;
            }
            else
            {
                var newFrame = new KeyFrame<double>() { Time = currentTick, Value = CurrentBpm };

                _chart.BpmKeyFrames.Add(newFrame);
            }

            _chart.BpmKeyFrames.Sort((a, b) => a.Time.CompareTo(b.Time));
            SyncBpmKeyframeProjection();

            NotifyBpmChanged();
        }

        public void SyncBpmKeyframeProjection()
        {
            for (int i = UIBpmKeyframes.Count - 1; i >= 0; i--)
            {
                if (!_chart.BpmKeyFrames.Any(model => ReferenceEquals(model, UIBpmKeyframes[i].Model)))
                {
                    UIBpmKeyframes.RemoveAt(i);
                }
            }

            foreach (var keyframe in _chart.BpmKeyFrames)
            {
                if (!UIBpmKeyframes.Any(wrapper => ReferenceEquals(wrapper.Model, keyframe)))
                {
                    UIBpmKeyframes.Add(new KeyFrameUIWrapper<double>(keyframe, _timeline, _messenger));
                }
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
            _messenger.Send(new JudgementLinesChangedMessage());
            _timeline.AudioTrack?.UpdatePixels();
        }

        private void SyncPlayheadToTick(double targetExactTick)
        {
            double relativeTick = Math.Max(0, targetExactTick);

            double newSeconds = TimeTickConverter.TickToTime(relativeTick, _chart.BpmKeyFrames, _chart.InitialBpm);

            _timeline.CurrentPlayTimeSeconds = newSeconds;

            _messenger.Send(new ForceSeekMessage(newSeconds));

            _messenger.Send(new JudgementLinesChangedMessage());
        }

    }


}
