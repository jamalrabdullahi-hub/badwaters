using BadWaters.Sim.Economy;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// Industry: the national economy at a glance, production programmes, construction orders, fielded-force readiness,
/// bottlenecks, and a supply-chain explorer that walks any design or good down to its deposits, marking what the
/// nation can make itself, what it must import, and what is short. The player sets policy; the engine does the rest.
/// </summary>
public partial class IndustryScreen : PanelContainer
{
    private VBoxContainer _left = null!, _mid = null!;
    private RichTextLabel _tree = null!;
    private OptionButton _explore = null!;
    private readonly List<string> _exploreIds = new();
    private static string Me => Session.World.Player;
    private static NationEconomy Ne => Session.World.Economy.Nations[Me];
    private static EconomyCatalog Cat => Session.Content.Economy;
    private static NationReport? Rep => Session.Econ.LastReports.GetValueOrDefault(Me);

    public override void _Ready()
    {
        AnchorRight = 1; AnchorBottom = 1;
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 12);
        AddChild(h);
        _left = Column(h, 430); _mid = Column(h, 420);
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        h.AddChild(right);
        right.AddChild(UiTheme.Text("SUPPLY CHAIN EXPLORER", 14, UiTheme.Accent));
        right.AddChild(UiTheme.Text("Pick a design or a good: its chain down to the deposits. ✓ made at home · ⇄ imported · ✗ short or no capacity.", 11, UiTheme.Dim, wrap: true));
        _explore = new OptionButton { ClipText = true };
        foreach (var d in Session.Content.Designs.Values.OrderBy(d => d.Name)) { _exploreIds.Add("design:" + d.Id); _explore.AddItem("Design · " + d.Name); }
        foreach (var g in Cat.Goods.Values.Where(g => g.Tier >= 3).OrderBy(g => g.Name)) { _exploreIds.Add("good:" + g.Id); _explore.AddItem("Good · " + g.Name); }
        _explore.ItemSelected += _ => ShowTree();
        right.AddChild(_explore);
        _tree = new RichTextLabel { BbcodeEnabled = true, SizeFlagsVertical = SizeFlags.ExpandFill, ScrollActive = true, FitContent = false };
        _tree.AddThemeFontSizeOverride("normal_font_size", 12);
        right.AddChild(_tree);
        if (DevScreenshot.Arg("explore") is { } ex && _exploreIds.IndexOf(ex) is var ix and >= 0) _explore.Select(ix);
        Refresh();
    }

    private static VBoxContainer Column(HBoxContainer h, int w)
    {
        var sc = new ScrollContainer { CustomMinimumSize = new Vector2(w, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var v = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        v.AddThemeConstantOverride("separation", 4);
        sc.AddChild(v); h.AddChild(sc);
        return v;
    }

    private static void Clear(Node n) { foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); } }

    public void Refresh()
    {
        Clear(_left); Clear(_mid);
        var ne = Ne; var rep = Rep;
        _left.AddChild(UiTheme.Text($"{Session.Content.Nations[Me].Name.ToUpperInvariant()} · ECONOMY", 14, UiTheme.Accent));
        _left.AddChild(UiTheme.Text($"GDP {ne.Gdp:0} bn ({MacroRules.YearOnYear(ne):+0.0%;-0.0%;0%} a year) · stability {ne.Stability:0}", 13));
        if (rep != null)
            _left.AddChild(UiTheme.Text($"Civilian needs met {rep.BasketSatisfaction:P0} · maintenance {rep.MaintenanceMet:P0} · electricity {rep.EnergySupply:0}/{rep.EnergyDemand:0} · imports {rep.ImportCost:0}/month", 12, UiTheme.Dim, wrap: true));
        else _left.AddChild(UiTheme.Text("Advance a month to see this month's production.", 12, UiTheme.Dim));

        // defence budget
        var db = new HBoxContainer();
        var dl = UiTheme.Text("", 12); dl.CustomMinimumSize = new Vector2(230, 0);
        void Label() => dl.Text = $"Defence {ne.DefenceShare:P1} of GDP · funds {ne.DefenceFunds:0.0} bn";
        Label();
        var sl = new HSlider { MinValue = 0.005, MaxValue = 0.12, Step = 0.0025, Value = ne.DefenceShare, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        sl.ValueChanged += v => { ne.DefenceShare = v; Label(); };
        db.AddChild(dl); db.AddChild(sl);
        _left.AddChild(db);

        // programmes
        _left.AddChild(UiTheme.Text("PRODUCTION PROGRAMMES", 13, UiTheme.Accent));
        foreach (var p in ne.Programmes.ToList())
        {
            var row = new HBoxContainer();
            var name = Session.Content.Designs.GetValueOrDefault(p.Design)?.Name ?? p.Design;
            row.AddChild(UiTheme.Text($"{name}: {p.LastMonth:0.##}/{p.RatePerMonth:0.##} a month · built {p.Built:0} · {(p.Status == "" ? "starting" : p.Status)}", 12,
                p.Status is "" or "on rate" ? UiTheme.Ink : UiTheme.Bad, wrap: true));
            row.GetChild<Label>(0).SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var rm = new Button { Text = "✕" }; rm.Pressed += () => { ne.Programmes.Remove(p); Refresh(); };
            row.AddChild(rm);
            _left.AddChild(row);
        }
        var add = new HBoxContainer();
        var pick = new OptionButton { ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var ids = Session.Content.Designs.Keys.OrderBy(k => Session.Content.Designs[k].Name).ToList();
        foreach (var id in ids) pick.AddItem(Session.Content.Designs[id].Name);
        var rate = new SpinBox { MinValue = 0.05, MaxValue = 500, Step = 0.05, Value = 1 };
        var go = new Button { Text = "Add" };
        go.Pressed += () => { ne.Programmes.Add(new Programme { Id = $"p{ne.Programmes.Count + 1}-{Session.World.Now.Ticks}", Design = ids[pick.Selected], RatePerMonth = rate.Value }); Refresh(); };
        add.AddChild(pick); add.AddChild(rate); add.AddChild(go);
        _left.AddChild(add);

        // construction
        _left.AddChild(UiTheme.Text("CONSTRUCTION", 13, UiTheme.Accent));
        foreach (var c in ne.Construction)
            _left.AddChild(UiTheme.Text($"{Cat.Facilities[c.Type].Name} in {Session.Content.Geo.Regions[c.Region].Name}{(c.AddLevels > 0 ? $" (+{c.AddLevels} levels)" : "")}: {c.MonthsLeft:0.#} months left", 12, UiTheme.Ink, wrap: true));
        var cb = new HBoxContainer();
        var types = Cat.Facilities.Values.Where(f => f.Capacity > 0 || f.SuppliesEnergy > 0 || f.Storage > 0).OrderBy(f => f.Name).ToList();
        var tp = new OptionButton { ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var t in types) tp.AddItem(t.Name);
        var regs = Session.Content.Geo.Regions.Values.Where(r => r.Nation == Me).ToList();
        var rp = new OptionButton { ClipText = true, CustomMinimumSize = new Vector2(120, 0) };
        foreach (var r in regs) rp.AddItem(r.Name);
        var build = new Button { Text = "Build" };
        build.Pressed += () => { Session.Econ.OrderConstruction(Session.World.Economy, Me, types[tp.Selected].Id, regs[rp.Selected].Id); Refresh(); };
        cb.AddChild(tp); cb.AddChild(rp); cb.AddChild(build);
        _left.AddChild(cb);
        if (tp.Selected >= 0) _left.AddChild(UiTheme.Text("Build bill: " + string.Join(", ", types[Math.Max(0, tp.Selected)].Build.Select(k => $"{k.Value:0} {Cat.Goods[k.Key].Name}")), 11, UiTheme.Dim, wrap: true));

        // fielded forces
        if (ne.Fleet.Count > 0)
        {
            _left.AddChild(UiTheme.Text("FIELDED FORCES", 13, UiTheme.Accent));
            foreach (var (d, f) in ne.Fleet)
                _left.AddChild(UiTheme.Text($"{Session.Content.Designs.GetValueOrDefault(d)?.Name ?? d}: {f.Count:0} · readiness {f.Readiness:P0}", 12, f.Readiness < 0.6 ? UiTheme.Bad : UiTheme.Ink));
        }

        // middle: bottlenecks and shortages
        _mid.AddChild(UiTheme.Text("BOTTLENECKS", 14, UiTheme.Accent));
        if (rep == null || rep.Bottlenecks.Count == 0) _mid.AddChild(UiTheme.Text("None this month.", 12, UiTheme.Dim));
        else foreach (var b in rep.Bottlenecks) _mid.AddChild(UiTheme.Text(b, 12, UiTheme.Ink, wrap: true));
        _mid.AddChild(UiTheme.Text("SHORTAGES AND WHY", 14, UiTheme.Accent));
        if (rep != null)
            foreach (var (g, q) in rep.Shortages.Where(k => Cat.Goods.ContainsKey(k.Key)).OrderByDescending(k => k.Value * Cat.Goods[k.Key].Price).Take(8))
                foreach (var line in rep.Explain(g, Cat).Take(6)) _mid.AddChild(UiTheme.Text(line, 11, line.StartsWith(" ") ? UiTheme.Dim : UiTheme.Bad, wrap: true));
        _mid.AddChild(UiTheme.Text("TOP IMPORTS LAST MONTH", 14, UiTheme.Accent));
        if (rep != null)
            foreach (var (g, q) in rep.Imported.Where(k => Cat.Goods.ContainsKey(k.Key)).OrderByDescending(k => k.Value * Cat.Goods[k.Key].Price).Take(10))
                _mid.AddChild(UiTheme.Text($"{Cat.Goods[g].Name}: {q:0.#}", 12));
        ShowTree();
    }

    /// <summary>The chain for the explorer's pick, annotated with this nation's capability.</summary>
    private void ShowTree()
    {
        if (_explore.Selected < 0) return;
        var sel = _exploreIds[_explore.Selected];
        var bills = Session.Econ.Solver.Bills;
        var roots = new Dictionary<string, double>();
        if (sel.StartsWith("design:"))
        {
            var (goods, asm) = bills.DesignBill(Session.Content.Designs[sel[7..]]);
            roots = goods;
            _tree.Text = "[b]Assembly:[/b] " + string.Join(", ", asm.Keys.Select(f => $"{Cat.Facilities[f].Name} {Mark(f)}")) + "\n";
        }
        else { roots[sel[5..]] = 1; _tree.Text = ""; }
        var rep = Rep;
        var sb = new System.Text.StringBuilder();
        void Walk(ChainNode n, int depth)
        {
            if (depth > 7) return;
            var g = Cat.Goods[n.Good];
            var shortG = rep?.Shortages.ContainsKey(n.Good) == true;
            var imported = rep?.Imported.ContainsKey(n.Good) == true;
            var facMark = n.Facility != null ? Mark(n.Facility) : "";
            var color = shortG ? "#ff4d5a" : imported ? "#ffb347" : "#d8f3ff";
            sb.Append(new string(' ', depth * 3)).Append($"[color={color}]{g.Name}[/color] {n.Qty:0.##}");
            if (n.Facility != null) sb.Append($" [color=#6a8fa6]← {Cat.Facilities[n.Facility].Name}[/color] {facMark}");
            if (n.Deposit != null) sb.Append($" [color=#6a8fa6]· deposit {Cat.Resources[n.Deposit].Name}[/color]");
            if (imported) sb.Append(" [color=#ffb347]⇄[/color]");
            sb.Append('\n');
            foreach (var c in n.Inputs) Walk(c, depth + 1);
        }
        foreach (var (g, q) in roots.OrderByDescending(k => k.Value * Cat.Goods[k.Key].Price)) Walk(bills.Tree(g, q), 0);
        _tree.Text += sb.ToString();
    }

    /// <summary>✓ with levels if the nation has this facility, ✗ if not.</summary>
    private static string Mark(string facility)
    {
        var lv = Session.World.Economy.Sites.Values.Where(s => s.Nation == Me && s.Type == facility && s.Active).Sum(s => s.Level);
        return lv > 0 ? $"[color=#5cf2b0]✓{lv}[/color]" : "[color=#ff4d5a]✗[/color]";
    }
}
