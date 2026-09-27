using BadWaters.Sim.Theatre;
using Godot;

namespace BadWaters.Game;

/// <summary>Theatre Ops · Bases & ISR tab: your bases' components (truth) and enemy bases as your picture believes them.</summary>
public partial class TheatreOps
{
    private VBoxContainer _basesView = null!;

    private void UpdateBases()
    {
        Clear(_basesView);
        var me = ViewSide == "all" ? CommandSide : ViewSide;
        var enemy = me == "blue" ? "red" : "blue";
        var pic = Trial.Intel?.Pictures[me];
        _basesView.AddChild(UiTheme.Text($"{SideName(me).ToUpperInvariant()} BASES · actual state", 13, UiTheme.Accent));
        foreach (var b in Trial.Bases.Values.Where(b => b.Site.Side == me))
        {
            var ready = Trial.Air?.Idle(me).Count(u => u.Base == b.Site.Id) ?? 0;
            var flags = new List<string>();
            if (b.Type.Components.Any(c => c.Effect == "sorties")) flags.Add(b.RunwayOpen ? "runway open" : "RUNWAY CLOSED");
            if (!b.CanArm) flags.Add("can't arm strikes");
            if (b.PrepFactor > 1) flags.Add($"turnaround ×{b.PrepFactor:0.#}");
            if (b.SensorFactor < 1) flags.Add($"sensors {b.SensorFactor:P0}");
            if (b.Type.Components.Any(c => c.Effect == "launch-pads") && !b.PadsOk) flags.Add("PADS DOWN");
            _basesView.AddChild(UiTheme.Text($"{b.Site.Name} · {b.Type.Name}{(ready > 0 ? $" · {ready} aircraft ready" : "")} · {string.Join(" · ", flags)}", 12, flags.Any(f => f == f.ToUpperInvariant()) ? UiTheme.Bad : UiTheme.Ink));
            foreach (var c in b.Components.Values) _basesView.AddChild(Row(c.Def.Name, c.Integrity, $"{c.Integrity:0}% · hard {c.Def.Hardness:0} · +{c.Def.RepairPerHour:0}/h", 1));
        }
        if (pic == null) return;
        _basesView.AddChild(UiTheme.Text($"{SideName(enemy).ToUpperInvariant()} BASES · your picture (BDA)", 13, UiTheme.Accent));
        _basesView.AddChild(UiTheme.Text($"Next satellite pass T+{AirTasking.Clock(pic.NextSatellite)}. Strike and impact reports are rough; satellites and recon flights give real damage.", 12, UiTheme.Dim, wrap: true));
        foreach (var b in Trial.Bases.Values.Where(b => b.Site.Side == enemy))
        {
            _basesView.AddChild(UiTheme.Text($"{b.Site.Name} · {b.Type.Name}", 12, UiTheme.Ink));
            foreach (var c in b.Components.Values)
            {
                var bel = pic.Belief(b.Site.Id, c.Def.Id);
                var age = Trial.Time - bel.AsOf;
                _basesView.AddChild(Row(c.Def.Name, bel.Integrity, $"est. {bel.Integrity:0}% · {bel.Source} · {(bel.AsOf <= 0 ? "pre-war" : $"{age / 60:0} min old")} · conf {bel.Confidence:P0}", bel.Confidence));
            }
        }
    }

    private static Control Row(string name, double integrity, string note, double confidence)
    {
        var h = new HBoxContainer();
        var n = UiTheme.Text(name, 11, UiTheme.Dim); n.CustomMinimumSize = new Vector2(150, 0); n.ClipText = true;
        var bar = new ProgressBar { MaxValue = 100, Value = integrity, ShowPercentage = false, CustomMinimumSize = new Vector2(70, 8), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        var col = integrity < 50 ? UiTheme.Bad : integrity < 80 ? new Color("ffb347") : UiTheme.Good;
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.05f, 0.12f, 0.2f) });
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(col, (float)Math.Max(0.35, confidence)) });
        var t = UiTheme.Text(note, 11, UiTheme.Ink); t.ClipText = true; t.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        h.AddChild(n); h.AddChild(bar); h.AddChild(t);
        return h;
    }
}
