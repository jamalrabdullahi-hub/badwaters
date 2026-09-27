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
        _basesView.AddChild(UiTheme.Text($"BASE STATUS · {SideName(me).ToUpperInvariant()} (actual) · {SideName(enemy).ToUpperInvariant()} (your estimate)", 13, UiTheme.Accent));
        _basesView.AddChild(StatusGrid(me, enemy, pic));
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

    private static readonly (string Effect, string Short)[] GridCols =
        { ("sorties", "RWY"), ("launch-pads", "PADS"), ("piers", "PIER"), ("fuel", "FUEL"), ("munitions", "MUN"), ("sensors", "RDR"), ("apron", "APR"), ("shelter", "HAS"), ("readiness", "MNT"), ("command", "CMD") };

    /// <summary>Bases down, components across, one coloured cell each: truth for your side, belief for the enemy.</summary>
    private Control StatusGrid(string me, string enemy, Picture? pic)
    {
        var g = new GridContainer { Columns = GridCols.Length + 1 };
        g.AddThemeConstantOverride("h_separation", 3);
        g.AddThemeConstantOverride("v_separation", 3);
        g.AddChild(new Control());
        foreach (var (_, sh) in GridCols) { var l = UiTheme.Text(sh, 10, UiTheme.Dim); l.HorizontalAlignment = HorizontalAlignment.Center; l.CustomMinimumSize = new Vector2(40, 0); g.AddChild(l); }
        foreach (var side in new[] { me, enemy })
            foreach (var b in Trial.Bases.Values.Where(b => b.Site.Side == side))
            {
                var n = UiTheme.Text(b.Site.Name.Replace(" Air Base", " AB").Replace(" naval base", " NB").Replace(" Naval Base", " NB"), 11, side == "blue" ? TheatreLayer.Blue : TheatreLayer.Red);
                n.CustomMinimumSize = new Vector2(130, 0); n.ClipText = true;
                g.AddChild(n);
                foreach (var (eff, _) in GridCols)
                {
                    var c = b.Components.Values.FirstOrDefault(x => x.Def.Effect == eff);
                    if (c == null) { g.AddChild(new Control { CustomMinimumSize = new Vector2(40, 18) }); continue; }
                    var val = side == me || pic == null ? c.Integrity : pic.Belief(b.Site.Id, c.Def.Id).Integrity;
                    var conf = side == me || pic == null ? 1 : pic.Belief(b.Site.Id, c.Def.Id).Confidence;
                    var col = val >= 80 ? UiTheme.Good : val >= 50 ? new Color("ffb347") : UiTheme.Bad;
                    var cell = new PanelContainer { CustomMinimumSize = new Vector2(40, 18), TooltipText = $"{c.Def.Name}: {val:0}%{(side == me ? "" : $" (estimate, confidence {conf:P0})")}" };
                    cell.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(col, (float)(0.25 + 0.6 * conf * (0.4 + val / 170))), ContentMarginLeft = 2, ContentMarginRight = 2 });
                    var t = UiTheme.Text($"{val:0}", 10, new Color("02121c"));
                    t.HorizontalAlignment = HorizontalAlignment.Center;
                    cell.AddChild(t);
                    g.AddChild(cell);
                }
            }
        return g;
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
