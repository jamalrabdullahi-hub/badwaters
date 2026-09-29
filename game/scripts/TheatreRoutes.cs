using BadWaters.Sim.Theatre;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// Theatre Ops · route editing: drag the ingress, IP, egress or station of one of your flights that hasn't taken off.
/// The time on target stays fixed; take-off and landing are re-worked (AirTasking.MoveWaypoint). Also draws jamming cover.
/// </summary>
public partial class TheatreOps
{
    private (Flight F, int Index)? _drag;
    private Vector2 _dragAt;

    private static bool Movable(Waypoint w) => w.Name is "Ingress" or "IP" or "Egress" or "Station";

    /// <summary>Your flights whose routes can be edited: planned, not yet airborne, and shown (selected, or routes on).</summary>
    private IEnumerable<Flight> Editable() =>
        Trial.Air?.Flights.Where(f => f.Side == CommandSide && f.State == FlightState.Planned && Trial.Time < f.Takeoff && (SelectedFlight == f || Show("routes"))) ?? Enumerable.Empty<Flight>();

    /// <summary>Left press on a handle starts a drag; motion moves the ghost; release applies the move.</summary>
    private bool RouteInput(InputEvent e)
    {
        var vp = GetViewport();
        var zoom = vp.GetCamera2D()?.Zoom.X ?? 1;
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            var world = vp.GetCanvasTransform().AffineInverse() * mb.Position;
            if (mb.Pressed)
            {
                foreach (var f in Editable())
                    for (var i = 0; i < f.Route.Count; i++)
                        if (Movable(f.Route[i]) && Session.Project(f.Route[i].Lon, f.Route[i].Lat).DistanceTo(world) < 9 / zoom)
                        {
                            _drag = (f, i); _dragAt = world; SelectedFlight = f;
                            vp.SetInputAsHandled();
                            return true;
                        }
                return false;
            }
            if (_drag is { } d)
            {
                var (lon, lat) = Session.Unproject(world);
                var msg = Trial.Air!.MoveWaypoint(d.F, d.Index, lon, lat);
                if (msg != "") Trial.Report("refused", msg);
                _drag = null;
                Layer.QueueRedraw();
                vp.SetInputAsHandled();
                return true;
            }
        }
        if (e is InputEventMouseMotion mm && _drag != null)
        {
            _dragAt = vp.GetCanvasTransform().AffineInverse() * mm.Position;
            Layer.QueueRedraw();
            vp.SetInputAsHandled();
            return true;
        }
        return false;
    }

    public void DrawWaypointHandles(TheatreLayer layer, float zoom, float w)
    {
        foreach (var f in Editable())
            for (var i = 0; i < f.Route.Count; i++)
            {
                if (!Movable(f.Route[i])) continue;
                var p = Session.Project(f.Route[i].Lon, f.Route[i].Lat);
                var s = 5 / zoom;
                layer.DrawRect(new Rect2(p - new Vector2(s, s), new Vector2(2 * s, 2 * s)), new Color(UiTheme.TacAccent, 0.9f), false, w);
            }
        if (_drag is { } d)
        {
            var prev = Session.Project(d.F.Route[d.Index - 1].Lon, d.F.Route[d.Index - 1].Lat);
            var next = Session.Project(d.F.Route[d.Index + 1].Lon, d.F.Route[d.Index + 1].Lat);
            var c = new Color(UiTheme.TacAccent, 0.9f);
            layer.DrawDashedLine(prev, _dragAt, c, w, 6 / zoom);
            layer.DrawDashedLine(_dragAt, next, c, w, 6 / zoom);
            layer.DrawCircle(_dragAt, 4 / zoom, c);
            layer.Labels.Add((_dragAt, $"{d.F.Route[d.Index].Name} · release to re-time", c));
        }
        // stand-off jamming cover of airborne jammers (either side, if the viewer holds them)
        if (!Show("jamming") || Trial.Air == null) return;
        foreach (var j in Trial.Air.Flights.Where(j => j.Mission == Mission.Jamming && j.Airborne(Trial.Time) && j.Aircraft.Count > 0 && Trial.Emitting(j.Id)
                                                   && (ViewSide == "all" || j.Side == ViewSide || (Trial.Intel?.Pictures[ViewSide].Contacts.ContainsKey(j.Id) ?? false))))
        {
            var (lon, lat) = j.Pos(Trial.Time);
            var p = Session.Project(lon, lat);
            var r = (float)(AirPacing.JamKm * Session.PxPerDeg / (111.0 * Math.Cos(lat * Math.PI / 180)));
            var c = j.Side == "blue" ? TheatreLayer.Blue : TheatreLayer.Red;
            layer.DrawCircle(p, r, new Color(TheatreLayer.Orange, 0.05f));
            layer.DrawArc(p, r, 0, Mathf.Tau, 64, new Color(TheatreLayer.Orange, 0.6f), w, true);
            layer.Labels.Add((p + new Vector2(0, r * 0.9f), $"JAMMING · {j.Callsign}", new Color(c, 0.9f)));
        }
    }
}
