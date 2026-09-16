namespace Inkoova.Academy.Application.Abstractions;

/// <summary>
/// Time as a dependency. Every deadline in this system is business-critical — the 14-day
/// commission window, the grace period, the certificate hash — so tests drive the clock
/// instead of sleeping (T-16 acceptance criteria).
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
