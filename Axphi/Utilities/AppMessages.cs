namespace Axphi.Utilities;

public sealed record AudioLoadedMessage(string FilePath);

public sealed record IllustrationLoadedMessage();

public sealed record ProjectLoadedMessage();

public sealed record UpdateRendererMessage();

public sealed record JudgementLinesChangedMessage();

public sealed record ForcePausePlaybackMessage();

public sealed record ForceSeekMessage(double TargetSeconds);
