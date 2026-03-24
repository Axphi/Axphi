namespace Axphi.Data
{
    public class ProjectMetadata
    {
        public int AudioOffsetTicks { get; set; } = 0;

        public double AudioVolume { get; set; } = 100.0;

        public double PlayheadTimeSeconds { get; set; } = 0;

        public double CurrentHorizontalScrollOffset { get; set; } = 0;

        public double ZoomScale { get; set; } = 1.0;

        public int TotalDurationTicks { get; set; } = 10000;

        public int WorkspaceStartTick { get; set; } = 0;

        public int WorkspaceEndTick { get; set; } = 1920;

        public bool IsAudioTrackExpanded { get; set; }

        public bool IsAudioTrackLocked { get; set; }
    }
}