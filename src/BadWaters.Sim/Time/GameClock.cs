namespace BadWaters.Sim.Time;

/// <summary>Length of one strategic turn. The player picks it each turn.</summary>
public enum TurnLength { Day, Week, Month, Quarter, HalfYear, Year }

/// <summary>Real-time speed multipliers for Theatre Ops.</summary>
public static class TheatreSpeeds
{
    public static readonly int[] Allowed = { 0, 1, 2, 5, 10, 30, 50, 100 };
    public static bool IsAllowed(int speed) => Array.IndexOf(Allowed, speed) >= 0;
}

/// <summary>Game calendar. Strategic turns advance it in steps; Theatre Ops advances it in game seconds.</summary>
public sealed class GameClock
{
    public DateTime Now { get; private set; }
    public DateTime End { get; }

    public GameClock(DateTime start, DateTime end)
    {
        if (end <= start) throw new ArgumentException("End must be after start.");
        Now = start;
        End = end;
    }

    public bool Finished => Now >= End;

    public static DateTime Next(DateTime from, TurnLength len) => len switch
    {
        TurnLength.Day => from.AddDays(1),
        TurnLength.Week => from.AddDays(7),
        TurnLength.Month => from.AddMonths(1),
        TurnLength.Quarter => from.AddMonths(3),
        TurnLength.HalfYear => from.AddMonths(6),
        TurnLength.Year => from.AddYears(1),
        _ => throw new ArgumentOutOfRangeException(nameof(len)),
    };

    /// <summary>Advance one strategic turn, clamped to the campaign end. Returns the span covered.</summary>
    public TimeSpan Advance(TurnLength len)
    {
        var next = Next(Now, len);
        if (next > End) next = End;
        var span = next - Now;
        Now = next;
        return span;
    }

    /// <summary>Advance in Theatre Ops by game seconds (already multiplied by the speed setting).</summary>
    public void AdvanceSeconds(double gameSeconds)
    {
        if (gameSeconds < 0) throw new ArgumentOutOfRangeException(nameof(gameSeconds));
        var next = Now.AddSeconds(gameSeconds);
        Now = next > End ? End : next;
    }
}
