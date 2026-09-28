namespace BadWaters.Sim.Environment;

/// <summary>
/// A deliberately simple, deterministic weather field for the Strategic Map: a couple of dozen storm cells that
/// drift along climate belts and strengthen in their hemisphere's storm season. Game abstraction, not a forecast.
/// Its one effect for now: storms along a trade route add a little disruption.
/// </summary>
public static class Weather
{
    public sealed record Cell(double Lon, double Lat, double RadiusDeg, double Intensity);

    public const int Count = 24;

    /// <summary>Storm cells on a given day (0..1 intensity).</summary>
    public static IEnumerable<Cell> Cells(DateTime t)
    {
        var days = (t - new DateTime(1980, 1, 1)).TotalDays;
        var doy = t.DayOfYear / 365.25 * 2 * Math.PI;
        for (var k = 0; k < Count; k++)
        {
            var north = k % 2 == 0;
            var tropical = k % 3 != 0;                                   // tropical belt drifts west, mid-latitude east
            var belt = (tropical ? 18 : 48) * (north ? 1 : -1);
            var speed = (tropical ? -2.5 : 5.0) * (1 + (k % 4) * 0.15);  // degrees of longitude per day
            var lon = Wrap(k * 137.5 + days * speed);
            var lat = belt + 7 * Math.Sin(days / (7 + k % 5) + k);
            var season = north ? 0.5 - 0.5 * Math.Cos(doy - 0.4) : 0.5 + 0.5 * Math.Cos(doy - 0.4); // peaks late summer
            var pulse = 0.5 + 0.5 * Math.Sin(days / (3 + k % 7) + k * 1.7);
            var intensity = Math.Clamp((tropical ? season : 0.6 + 0.4 * (1 - season)) * pulse, 0, 1);
            if (intensity < 0.15) continue;
            yield return new Cell(lon, lat, 4 + (k % 5) * 1.5, intensity);
        }
    }

    /// <summary>Storm strength at a point (0..1).</summary>
    public static double StormAt(double lon, double lat, DateTime t)
    {
        var best = 0.0;
        foreach (var c in Cells(t))
        {
            var dx = Wrap(lon - c.Lon) * Math.Cos(lat * Math.PI / 180); var dy = lat - c.Lat;
            best = Math.Max(best, c.Intensity * Math.Exp(-(dx * dx + dy * dy) / (c.RadiusDeg * c.RadiusDeg)));
        }
        return best;
    }

    /// <summary>Worst storm along a path of [lon, lat] points.</summary>
    public static double StormAlong(IEnumerable<double[]> path, DateTime t) => path.Select(p => StormAt(p[0], p[1], t)).DefaultIfEmpty(0).Max();

    private static double Wrap(double lon) => ((lon + 180) % 360 + 360) % 360 - 180;
}
