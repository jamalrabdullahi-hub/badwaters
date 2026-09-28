using BadWaters.Sim.Engineering;
using BadWaters.Sim.Theatre;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// Development tab, laid out like a naval design office: the blueprint on the left (drag parts on, move,
/// rotate, mirror), the weight/cost sheet in the middle, and Systems / Parts / Selected tabs on the right.
/// Limits are plausible game budgets (weight, space, power, cooling, crew, topside load); ratings are
/// abstract 0–10 game values. Saved designs go to mods/player and are usable in Theatre Ops.
/// </summary>
public partial class DevelopmentScreen : PanelContainer
{
    private static readonly (string Label, string Kind)[] Kinds = { ("Ship", "ship"), ("Ground", "ground"), ("Aircraft", "aircraft"), ("Munition", "munition") };
    public static readonly Dictionary<string, Color> FamilyColor = new()
    {
        ["sensors"] = new("7fe3ff"), ["payload"] = new("ff6b6b"), ["fuzing"] = new("ff9f6b"), ["guidance"] = new("c49bff"),
        ["navigation"] = new("9bb8ff"), ["computing"] = new("5cf2b0"), ["comms"] = new("5cd0f2"), ["ew"] = new("ffd36b"),
        ["power"] = new("f2e15c"), ["propulsion"] = new("ffb347"), ["materials"] = new("8aa0b3"), ["structure"] = new("6f8599"),
        ["thermal"] = new("6be3ff"), ["interfaces"] = new("b3c7d6"), ["reliability"] = new("9ad0a8"),
    };
    private static readonly string[] SheetOrder = { "Hull and fittings", "Machinery", "Sensors", "Weapons", "Combat systems", "Guns", "Point defence", "Launchers",
        "Masts", "Funnels", "Superstructure", "Aviation", "Boats", "Electronics", "Deck gear", "Vehicle" };

    private string _kind = "ship";
    private DesignDef _work = null!;
    private OptionButton _design = null!, _frame = null!, _family = null!, _partCat = null!;
    private readonly List<string> _designIds = new(), _frameIds = new(), _catIds = new(), _partCats = new();
    private ItemList _catalog = null!;
    private LineEdit _name = null!;
    private VBoxContainer _fitted = null!, _budgets = null!, _ratings = null!, _issues = null!, _sheet = null!, _selected = null!, _effect = null!;
    private GridContainer _partGrid = null!;
    private Blueprint _blueprint = null!;
    private DesignVisual _viz = null!;
    private Label _status = null!, _hullInfo = null!;
    private CheckBox _arcs = null!, _mirror = null!;
    private readonly Dictionary<string, Control> _tabs = new();
    public event Action? DesignsChanged;

    private static Catalog Cat => Session.Content.Catalog;

    public override void _Ready()
    {
        AnchorRight = 1; AnchorBottom = 1;
        AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.008f, 0.024f, 0.048f, 0.97f), UiTheme.Line, 6));
        // open on the requested design's own system type
        if (DevScreenshot.Arg("design") is { } want && Session.Content.Designs.TryGetValue(want, out var wd) && Cat.Frames.TryGetValue(wd.Frame, out var wf)) _kind = wf.Kind;
        var root = new HBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        AddChild(root);

        // ---- left: system type, blueprint, blueprint tools
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(430, 0) };
        root.AddChild(left);
        var kinds = new HBoxContainer();
        var group = new ButtonGroup();
        foreach (var (label, kind) in Kinds)
        {
            var b = new Button { Text = label, ToggleMode = true, ButtonGroup = group, ButtonPressed = kind == _kind, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            b.Pressed += () => { _kind = kind; RefreshDesigns(null); };
            kinds.AddChild(b);
        }
        left.AddChild(kinds);
        _blueprint = new Blueprint { SizeFlagsVertical = SizeFlags.ExpandFill, Design = () => _work };
        _blueprint.Changed += Recalc;
        _blueprint.SelectionChanged += ShowSelected;
        left.AddChild(_blueprint);
        _viz = new DesignVisual { SizeFlagsVertical = SizeFlags.ExpandFill, Visible = false };
        left.AddChild(_viz);
        var tools = new HBoxContainer();
        var zin = new Button { Text = "+" }; zin.Pressed += () => { _blueprint.Zoom *= 1.25f; _blueprint.QueueRedraw(); };
        var zout = new Button { Text = "−" }; zout.Pressed += () => { _blueprint.Zoom = Math.Max(0.5f, _blueprint.Zoom / 1.25f); _blueprint.QueueRedraw(); };
        _arcs = new CheckBox { Text = "Arcs", ButtonPressed = true }; _arcs.Toggled += on => { _blueprint.ShowArcs = on; _blueprint.QueueRedraw(); };
        _mirror = new CheckBox { Text = "Mirror", ButtonPressed = true, TooltipText = "Off-centre drops also place a twin on the other side" }; _mirror.Toggled += on => _blueprint.Mirror = on;
        var clear = new Button { Text = "Clear parts" }; clear.Pressed += () => { _work.Parts.Clear(); _blueprint.Selected = -1; Recalc(); ShowSelected(); };
        var shape = new CheckBox { Text = "Edit shape", TooltipText = "Drag outline vertices (mirrored). Cosmetic only: budgets use the standard hull." };
        shape.Toggled += on => { _blueprint.EditOutline = on; _blueprint.QueueRedraw(); };
        var resetShape = new Button { Text = "Reset shape" }; resetShape.Pressed += () => _blueprint.ResetOutline();
        tools.AddChild(zout); tools.AddChild(zin); tools.AddChild(_arcs); tools.AddChild(_mirror); tools.AddChild(shape); tools.AddChild(resetShape); tools.AddChild(clear);
        left.AddChild(tools);
        left.AddChild(UiTheme.Text("Drag parts from the Parts tab · drag to move · Q/E rotate 15° · R or right-click 90° · Del removes · wheel zooms", 11, UiTheme.Dim, wrap: true));

        // ---- middle: design sheet
        var ms = new ScrollContainer { CustomMinimumSize = new Vector2(470, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        root.AddChild(ms);
        var mid = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        mid.AddThemeConstantOverride("separation", 6);
        ms.AddChild(mid);
        var r1 = new HBoxContainer();
        _design = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
        _design.ItemSelected += i => Load(Session.Content.Designs[_designIds[(int)i]]);
        var variant = new Button { Text = "New variant" }; variant.Pressed += NewVariant;
        var save = new Button { Text = "Save design" }; save.Pressed += Save;
        r1.AddChild(_design); r1.AddChild(variant); r1.AddChild(save);
        mid.AddChild(r1);
        var r2 = new HBoxContainer();
        r2.AddChild(Field("Class name", 90));
        _name = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, PlaceholderText = "Design name" };
        _name.TextChanged += t => _work = _work with { Name = t };
        r2.AddChild(_name);
        mid.AddChild(r2);
        var r3 = new HBoxContainer();
        r3.AddChild(Field("Hull / frame", 90));
        _frame = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
        _frame.ItemSelected += i => { _work = _work with { Frame = _frameIds[(int)i] }; _blueprint.Zoom = 1; Recalc(); };
        r3.AddChild(_frame);
        mid.AddChild(r3);
        _hullInfo = UiTheme.Text("", 12, UiTheme.Dim);
        mid.AddChild(_hullInfo);
        mid.AddChild(UiTheme.Text("WEIGHT AND COST", 13, UiTheme.Accent));
        mid.AddChild(_sheet = new VBoxContainer());
        mid.AddChild(UiTheme.Text("BUDGETS", 13, UiTheme.Accent));
        mid.AddChild(_budgets = new VBoxContainer());
        mid.AddChild(UiTheme.Text("CAPABILITY · 0–10", 13, UiTheme.Accent));
        mid.AddChild(_ratings = new VBoxContainer());
        mid.AddChild(UiTheme.Text("ISSUES", 13, UiTheme.Accent));
        mid.AddChild(_issues = new VBoxContainer());
        _status = UiTheme.Text("", 12, UiTheme.Dim, wrap: true);
        _status.CustomMinimumSize = new Vector2(440, 0);
        mid.AddChild(_status);

        // ---- right: Systems / Parts / Selected
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(right);
        var tabs = new HBoxContainer();
        right.AddChild(tabs);
        var tg = new ButtonGroup();
        var pages = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        right.AddChild(pages);
        Control Tab(string name, Control page)
        {
            page.SizeFlagsVertical = SizeFlags.ExpandFill;
            page.Visible = name == "Parts";
            pages.AddChild(page);
            var b = new Button { Text = name, ToggleMode = true, ButtonGroup = tg, ButtonPressed = page.Visible, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            b.Pressed += () => { foreach (var c in pages.GetChildren()) ((Control)c).Visible = c == page; };
            tabs.AddChild(b);
            _tabs[name] = b;
            return page;
        }

        // Parts palette
        var pv = new VBoxContainer();
        var pr = new HBoxContainer();
        pr.AddChild(UiTheme.Text("Category", 12, UiTheme.Dim));
        _partCat = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _partCat.ItemSelected += _ => FillParts();
        pr.AddChild(_partCat);
        pv.AddChild(pr);
        var ps = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _partGrid = new GridContainer { Columns = 4 };
        ps.AddChild(_partGrid);
        pv.AddChild(ps);
        Tab("Parts", pv);

        // Systems (internal components)
        var sv = new VBoxContainer();
        sv.AddChild(UiTheme.Text("Internal systems (machinery, electronics, payload...)", 12, UiTheme.Dim));
        var fs = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _fitted = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        fs.AddChild(_fitted);
        sv.AddChild(fs);
        var addRow = new HBoxContainer();
        _family = new OptionButton();
        _family.AddItem("All families");
        foreach (var f in Families.All) _family.AddItem(f);
        _family.ItemSelected += _ => RefreshCatalog();
        var add = new Button { Text = "Fit selected ▸" };
        add.Pressed += () => { foreach (var i in _catalog.GetSelectedItems()) Fit(_catIds[i]); };
        addRow.AddChild(_family); addRow.AddChild(add);
        sv.AddChild(addRow);
        _catalog = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        _catalog.ItemActivated += i => Fit(_catIds[(int)i]);
        sv.AddChild(_catalog);
        Tab("Systems", sv);

        // Selected part
        var selScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _selected = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selScroll.AddChild(_selected);
        Tab("Selected", selScroll);

        // Components in effect
        var ev = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _effect = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        ev.AddChild(_effect);
        Tab("In effect", ev);

        RefreshDesigns(DevScreenshot.Arg("design"));
        if (int.TryParse(DevScreenshot.Arg("part"), out var sel) && sel < _work.Parts.Count) { _blueprint.Selected = sel; ShowSelected(); _blueprint.QueueRedraw(); }
        if (DevScreenshot.Arg("shape") != null) { _blueprint.EditOutline = true; _blueprint.QueueRedraw(); }
    }

    private static Label Field(string t, float w) { var l = UiTheme.Text(t, 13, UiTheme.Dim); l.CustomMinimumSize = new Vector2(w, 0); return l; }

    private void RefreshDesigns(string? select)
    {
        _design.Clear(); _designIds.Clear();
        foreach (var d in Session.Content.Designs.Values.Where(d => Cat.Frames.TryGetValue(d.Frame, out var f) && f.Kind == _kind).OrderBy(d => d.Name))
        {
            _designIds.Add(d.Id);
            _design.AddItem((d.Id.StartsWith("player-") ? "★ " : "") + d.Name);
        }
        if (_designIds.Count == 0) { Load(new DesignDef { Id = $"player-{_kind}-1", Name = "New design", Frame = Cat.Frames.Values.First(f => f.Kind == _kind).Id }); return; }
        var idx = Math.Max(0, select != null ? _designIds.IndexOf(select) : 0);
        _design.Select(idx);
        Load(Session.Content.Designs[_designIds[idx]]);
    }

    private void Load(DesignDef d)
    {
        _work = d with { Components = d.Components.ToList(), Parts = d.Parts.ToList(), Outline = d.Outline.ToList() };
        _name.Text = d.Name;
        _frame.Clear(); _frameIds.Clear();
        foreach (var f in Cat.Frames.Values.Where(f => f.Kind == _kind))
        {
            _frameIds.Add(f.Id);
            _frame.AddItem(f.Hull != null ? $"{f.Name} · {f.Hull.Length:0.#} m" : f.Name);
        }
        _frame.Select(Math.Max(0, _frameIds.IndexOf(d.Frame)));
        _partCat.Clear(); _partCats.Clear();
        _partCat.AddItem("All parts"); _partCats.Add("");
        foreach (var c in Cat.Parts.Values.Where(p => p.FitsKinds.Contains(_kind)).Select(p => p.Category).Distinct())
        {
            _partCats.Add(c);
            _partCat.AddItem(c);
        }
        _blueprint.Selected = -1; _blueprint.Zoom = 1;
        FillParts();
        RefreshCatalog();
        Recalc();
        ShowSelected();
    }

    private void FillParts()
    {
        foreach (var c in _partGrid.GetChildren()) { _partGrid.RemoveChild(c); c.QueueFree(); }
        var cat = _partCat.Selected > 0 ? _partCats[_partCat.Selected] : "";
        foreach (var p in Cat.Parts.Values.Where(p => p.FitsKinds.Contains(_kind) && (cat == "" || p.Category == cat)))
        {
            var tile = new PartTile { Part = p };
            var pp = p;
            tile.Pressed += () => { _work.Parts.Add(new Placement(pp.Id, 0, 0)); _blueprint.Selected = _work.Parts.Count - 1; Recalc(); ShowSelected(); };
            _partGrid.AddChild(tile);
        }
    }

    private void RefreshCatalog()
    {
        _catalog.Clear(); _catIds.Clear();
        var fam = _family.Selected <= 0 ? null : Families.All[_family.Selected - 1];
        foreach (var c in Cat.Components.Values.Where(c => (c.FitsKinds.Length == 0 || c.FitsKinds.Contains(_kind)) && (fam == null || c.Family == fam)).OrderBy(c => c.Family).ThenBy(c => c.Name))
        {
            _catIds.Add(c.Id);
            var i = _catalog.AddItem($"{c.Name}   {Adds(c.Adds)}");
            _catalog.SetItemCustomFgColor(i, FamilyColor.GetValueOrDefault(c.Family, UiTheme.Ink));
            _catalog.SetItemTooltip(i, $"{c.Family} · uses {string.Join(", ", c.Uses.Select(u => $"{u.Key} {u.Value:0.##}"))}");
        }
    }

    private static string Adds(Dictionary<string, double> a) =>
        a.Count == 0 ? "" : string.Join("  ", a.Select(x => $"{(x.Value >= 0 ? "+" : "")}{x.Value:0.#} {Rating(x.Key)}"));

    private static string Rating(string id) => Cat.Ratings.TryGetValue(id, out var r) ? r.Name : id;

    private void Fit(string id) { _work.Components.Add(id); Recalc(); }

    private void Recalc()
    {
        var frame = Cat.Frames.GetValueOrDefault(_work.Frame);
        var hasHull = frame?.Hull != null && frame.Kind != "munition"; // munitions keep the simple sections diagram
        _blueprint.Visible = hasHull; _viz.Visible = !hasHull;
        if (!hasHull) _viz.Show(_work);
        _blueprint.QueueRedraw();
        _hullInfo.Text = frame?.Hull is { } hl ? $"{hl.Length:0.#} m long · {hl.Width:0.#} m {(hl.Span > 0 ? "span" : "beam")} · {_work.Parts.Count} parts placed · {_work.Components.Count} internal systems" : "No blueprint for this kind: internal systems only.";
        var ev = new Evaluator(Cat).Evaluate(_work);

        // weight/cost sheet, RTW style
        Clear(_sheet);
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 18);
        foreach (var hd in new[] { "", "Weight (t)", "Cost" }) grid.AddChild(UiTheme.Text(hd, 11, UiTheme.Dim));
        var rows = (ev.Breakdown ?? new Dictionary<string, (double Weight, double Cost)>()).OrderBy(k => Array.IndexOf(SheetOrder, k.Key) is var i && i < 0 ? 99 : i);
        foreach (var (k, (w, c)) in rows)
        {
            var n = UiTheme.Text(k, 13); n.CustomMinimumSize = new Vector2(190, 0);
            grid.AddChild(n);
            grid.AddChild(Num(w)); grid.AddChild(Num(c));
        }
        var tw = ev.Budgets.TryGetValue("mass", out var m) ? m.Used : 0;
        grid.AddChild(UiTheme.Text("Total", 13, UiTheme.Accent)); grid.AddChild(Num(tw, UiTheme.Accent)); grid.AddChild(Num(ev.Cost, UiTheme.Accent));
        if (ev.Budgets.TryGetValue("mass", out var mm))
        {
            grid.AddChild(UiTheme.Text("Weight remaining", 13)); grid.AddChild(Num(mm.Available - mm.Used, mm.Available - mm.Used < 0 ? UiTheme.Bad : UiTheme.Good)); grid.AddChild(new Control());
        }
        if (ev.Budgets.TryGetValue("topside", out var ts))
        {
            grid.AddChild(UiTheme.Text("Topside load", 13)); grid.AddChild(Num(ts.Used, ts.Used > ts.Available ? UiTheme.Bad : UiTheme.Ink, $" of {ts.Available:0}")); grid.AddChild(new Control());
        }
        _sheet.AddChild(grid);

        Clear(_budgets);
        foreach (var (axis, (used, avail)) in ev.Budgets.Where(b => b.Value.Used > 0 || b.Value.Available > 0).OrderBy(b => b.Key == "topside" ? 1 : 0))
        {
            var row = new HBoxContainer();
            var n = UiTheme.Text(axis == "topside" ? "topside" : axis, 13); n.CustomMinimumSize = new Vector2(80, 0);
            var over = used > avail + 1e-9;
            var bar = Bar(avail <= 0 ? 1 : Math.Min(1, used / avail), over ? UiTheme.Bad : used / Math.Max(avail, 1e-9) >= 0.95 ? new Color("ffb347") : UiTheme.Accent);
            var v = UiTheme.Text($"{used:0.#} / {avail:0.#}", 12, over ? UiTheme.Bad : UiTheme.Dim); v.CustomMinimumSize = new Vector2(120, 0);
            row.AddChild(n); row.AddChild(bar); row.AddChild(v);
            _budgets.AddChild(row);
        }

        Clear(_ratings);
        foreach (var (r, v) in ev.Ratings.Where(r => r.Value > 0).OrderByDescending(r => r.Value))
        {
            var row = new HBoxContainer();
            var n = UiTheme.Text(Rating(r), 13); n.CustomMinimumSize = new Vector2(140, 0);
            row.AddChild(n); row.AddChild(Bar(v / 10, UiTheme.Accent)); row.AddChild(UiTheme.Text($"{v:0}", 13));
            _ratings.AddChild(row);
        }

        Clear(_issues);
        if (ev.Issues.Count == 0) _issues.AddChild(UiTheme.Text("✓ No issues. Ready to prototype.", 13, UiTheme.Good));
        foreach (var i in ev.Issues.Take(12)) _issues.AddChild(Wrap($"• {i.Text}", UiTheme.Bad));
        if (ev.Issues.Count > 12) _issues.AddChild(UiTheme.Text($"… and {ev.Issues.Count - 12} more", 12, UiTheme.Bad));

        Clear(_fitted);
        for (var i = 0; i < _work.Components.Count; i++)
        {
            var idx = i;
            if (!Cat.Components.TryGetValue(_work.Components[i], out var c)) continue;
            var h = new HBoxContainer();
            h.AddChild(new ColorRect { Color = FamilyColor.GetValueOrDefault(c.Family, UiTheme.Dim), CustomMinimumSize = new Vector2(6, 20) });
            h.AddChild(UiTheme.Text(c.Name, 13));
            var ad = UiTheme.Text(Adds(c.Adds), 11, UiTheme.Dim); ad.SizeFlagsHorizontal = SizeFlags.ExpandFill; ad.ClipText = true;
            h.AddChild(ad);
            var rm = new Button { Text = "✕", TooltipText = "Remove" };
            rm.Pressed += () => { _work.Components.RemoveAt(idx); Recalc(); };
            h.AddChild(rm);
            _fitted.AddChild(h);
        }

        Clear(_effect);
        var prof = SystemProfile.Build(Cat, _work);
        foreach (var (r, v) in ev.Ratings.Where(r => r.Value > 0).OrderByDescending(r => r.Value))
        {
            var agg = Cat.Ratings.TryGetValue(r, out var rule) ? rule.Aggregate : "sum";
            var fromParts = _work.Parts.Where(p => Cat.Parts.TryGetValue(p.Part, out var vp) && vp.Adds.ContainsKey(r)).GroupBy(p => Cat.Parts[p.Part].Name).Select(g => $"{g.Key} ×{g.Count()}");
            var parts = prof.For(r).Select(c => $"{c.Source} {(c.Value >= 0 ? "+" : "")}{c.Value:0.#}").Concat(fromParts);
            _effect.AddChild(Wrap($"{Rating(r)} {v:0} ({agg}) ← {string.Join(", ", parts)}", UiTheme.Ink));
        }
    }

    private static Label Num(double v, Color? c = null, string suffix = "")
    {
        var l = UiTheme.Text(Math.Abs(v) < 100 ? $"{v:0.##}{suffix}" : $"{v:#,0}{suffix}", 13, c ?? UiTheme.Ink);
        l.HorizontalAlignment = HorizontalAlignment.Right;
        l.CustomMinimumSize = new Vector2(90, 0);
        return l;
    }

    private void ShowSelected()
    {
        Clear(_selected);
        var i = _blueprint.Selected;
        if (i < 0 || i >= _work.Parts.Count || !Cat.Parts.TryGetValue(_work.Parts[i].Part, out var p))
        {
            _selected.AddChild(UiTheme.Text("Click a part on the blueprint to select it.", 12, UiTheme.Dim));
            return;
        }
        if (_tabs.TryGetValue("Selected", out var tb)) ((Button)tb).EmitSignal(BaseButton.SignalName.Pressed);
        if (_tabs.TryGetValue("Selected", out var tb2)) ((Button)tb2).ButtonPressed = true;
        var pl = _work.Parts[i];
        _selected.AddChild(UiTheme.Text(p.Name, 18, UiTheme.Accent));
        _selected.AddChild(UiTheme.Text($"{p.Category}{(p.Topside ? " · counts as topside weight" : "")}{(p.Arc > 0 ? $" · firing arc ±{p.Arc:0}°" : "")}", 12, UiTheme.Dim));
        if (p.Arc > 0 && Cat.Frames.GetValueOrDefault(_work.Frame)?.Hull is { } hh)
        {
            var (cov, _) = ArcGeometry.Coverage(_work, i, Cat, hh.Width * 3);
            _selected.AddChild(UiTheme.Text($"Arc clear: {cov:P0} of ±{p.Arc:0}° · effectiveness {0.4 + 0.6 * cov:P0}", 12, cov < 0.35 ? UiTheme.Bad : cov < 0.8 ? new Color("ffb347") : UiTheme.Good));
        }
        _selected.AddChild(UiTheme.Text($"Height class {ArcGeometry.Height(p):0} (taller parts block lower mounts' arcs)", 12, UiTheme.Dim));
        _selected.AddChild(UiTheme.Text($"Position {pl.X:0.#} m {(pl.X >= 0 ? "starboard" : "port")} · {Math.Abs(pl.Y):0.#} m {(pl.Y >= 0 ? "forward" : "aft")} · heading {pl.Rot:0}°", 12, UiTheme.Ink));
        var uses = new Dictionary<string, double>(p.Uses);
        if (p.Component != null && Cat.Components.TryGetValue(p.Component, out var c))
        {
            foreach (var (k, v) in c.Uses) uses[k] = uses.GetValueOrDefault(k) + v;
            _selected.AddChild(UiTheme.Text($"Includes: {c.Name} ({Adds(c.Adds)})", 12, UiTheme.Ink));
        }
        _selected.AddChild(UiTheme.Text("Uses: " + string.Join(" · ", uses.Select(u => $"{u.Key} {u.Value:0.#}")), 12, UiTheme.Ink));
        if (p.Adds.Count > 0) _selected.AddChild(UiTheme.Text("Adds: " + Adds(p.Adds), 12, UiTheme.Good));
        _selected.AddChild(UiTheme.Text($"Cost {p.Cost:0.#}", 12, UiTheme.Ink));
        var row = new HBoxContainer();
        foreach (var (t, a) in new[] { ("⟲ 15°", -15.0), ("⟳ 15°", 15.0), ("↻ 90°", 90.0), ("180°", 180.0) })
        {
            var b = new Button { Text = t };
            b.Pressed += () => _blueprint.Rotate(a);
            row.AddChild(b);
        }
        _selected.AddChild(row);
        var row2 = new HBoxContainer();
        var mir = new Button { Text = "Mirror copy" }; mir.Pressed += () => _blueprint.MirrorSelected();
        var cen = new Button { Text = "Centreline" }; cen.Pressed += () => { _work.Parts[i] = _work.Parts[i] with { X = 0 }; Recalc(); ShowSelected(); };
        var del = new Button { Text = "Remove" }; del.Pressed += () => _blueprint.Remove();
        row2.AddChild(mir); row2.AddChild(cen); row2.AddChild(del);
        _selected.AddChild(row2);
    }

    private static Control Bar(double frac, Color c)
    {
        var bar = new ProgressBar { MaxValue = 1, Value = frac, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 10), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.05f, 0.12f, 0.2f) });
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = c });
        return bar;
    }

    private static Label Wrap(string t, Color c)
    {
        var l = UiTheme.Text(t, 12, c, wrap: true);
        l.CustomMinimumSize = new Vector2(420, 0);
        return l;
    }

    private static void Clear(Node n) { foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); } }

    private void NewVariant()
    {
        var n = 1;
        while (Session.Content.Designs.ContainsKey($"player-{_kind}-{n}")) n++;
        _work = _work with { Id = $"player-{_kind}-{n}", Name = _work.Name + " (variant)", Components = _work.Components.ToList(), Parts = _work.Parts.ToList(), Outline = _work.Outline.ToList() };
        _name.Text = _work.Name;
        _status.Text = "New variant created. Change parts and systems, then Save design.";
    }

    private void Save()
    {
        if (!_work.Id.StartsWith("player-")) NewVariant(); // core designs are never overwritten
        Session.SaveDesign(_work with { Components = _work.Components.ToList(), Parts = _work.Parts.ToList(), Outline = _work.Outline.ToList() });
        _status.Text = $"Saved {_work.Name} to mods/player/designs.json. It is now available in Theatre Ops.";
        RefreshDesigns(_work.Id);
        DesignsChanged?.Invoke();
    }
}

public partial class DesignVisual : Control
{
    private DesignDef? _d;
    private static readonly string[] MunitionOrder = { "sensors", "payload", "fuzing", "guidance", "navigation", "computing", "comms", "ew", "power", "propulsion" };

    public void Show(DesignDef d) { _d = d; QueueRedraw(); }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.03f, 0.08f, 0.14f));
        for (var x = 0f; x < Size.X; x += 24) DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), new Color(0.5f, 0.85f, 1, 0.05f));
        for (var y = 0f; y < Size.Y; y += 24) DrawLine(new Vector2(0, y), new Vector2(Size.X, y), new Color(0.5f, 0.85f, 1, 0.05f));
        if (_d == null || !Session.Content.Catalog.Frames.TryGetValue(_d.Frame, out var frame)) return;
        var cat = Session.Content.Catalog;
        var font = ThemeDB.FallbackFont;
        var parts = _d.Components.Where(cat.Components.ContainsKey).Select(id => cat.Components[id]).ToList();
        DrawString(font, new Vector2(12, 20), frame.Name.ToUpperInvariant(), HorizontalAlignment.Left, -1, 13, UiTheme.Dim);

        if (frame.Kind == "munition")
        {
            var order = parts.OrderBy(p => Array.IndexOf(MunitionOrder, p.Family) is var i && i < 0 ? 99 : i).ToList();
            float x0 = Size.X * 0.12f, x1 = Size.X * 0.86f, cy = Size.Y * 0.55f, h = Math.Clamp((float)frame.Budgets.GetValueOrDefault("volume", 6) * 5, 24, 56);
            var weights = order.Select(p => (float)Math.Max(0.6, p.Uses.GetValueOrDefault("volume", 0.6))).ToList();
            var total = weights.Sum();
            // nose on the right, fins on the left
            DrawColoredPolygon(new[] { new Vector2(x1, cy - h / 2), new Vector2(x1 + h * 1.4f, cy), new Vector2(x1, cy + h / 2) }, new Color(0.1f, 0.22f, 0.34f));
            DrawColoredPolygon(new[] { new Vector2(x0, cy - h / 2), new Vector2(x0 - 16, cy - h / 2 - 22), new Vector2(x0 + 34, cy - h / 2 - 22), new Vector2(x0 + 46, cy - h / 2) }, new Color(0.1f, 0.22f, 0.34f));
            DrawColoredPolygon(new[] { new Vector2(x0, cy + h / 2), new Vector2(x0 - 16, cy + h / 2 + 22), new Vector2(x0 + 34, cy + h / 2 + 22), new Vector2(x0 + 46, cy + h / 2) }, new Color(0.1f, 0.22f, 0.34f));
            var x = x1;
            for (var i = 0; i < order.Count; i++)
            {
                var w = (x1 - x0) * weights[i] / total;
                var c = DevelopmentScreen.FamilyColor.GetValueOrDefault(order[i].Family, UiTheme.Dim);
                DrawRect(new Rect2(x - w, cy - h / 2, w, h), new Color(c, 0.35f));
                DrawRect(new Rect2(x - w, cy - h / 2, w, h), c, false, 1.5f);
                var label = order[i].Name.Length > 14 ? order[i].Name[..13] + "…" : order[i].Name;
                var ly = i % 2 == 0 ? cy - h / 2 - 8 : cy + h / 2 + 18;
                DrawString(font, new Vector2(x - w, ly), label, HorizontalAlignment.Left, Math.Max(w + 40, 60), 11, c);
                x -= w;
            }
            return;
        }

        // other systems: tiles per component, grouped by family
        var cols = Math.Max(4, (int)(Size.X / 150));
        var tw = (Size.X - 24) / cols; var th = 34f;
        var k = 0;
        foreach (var p in parts.OrderBy(p => p.Family))
        {
            var r = new Rect2(12 + k % cols * tw, 30 + k / cols * (th + 6), tw - 6, th);
            var c = DevelopmentScreen.FamilyColor.GetValueOrDefault(p.Family, UiTheme.Dim);
            DrawRect(r, new Color(c, 0.25f)); DrawRect(r, c, false, 1.5f);
            DrawString(font, r.Position + new Vector2(6, 21), p.Name, HorizontalAlignment.Left, tw - 16, 12, UiTheme.Ink);
            k++;
        }
    }
}
