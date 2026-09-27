using BadWaters.Sim.Engineering;
using BadWaters.Sim.Theatre;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// Development tab: pick a system type, pick a design, swap components and see budgets, ratings,
/// issues and which components produce each rating. Designing means choosing game parts; ratings are
/// abstract 0–10 game values. Saved designs go to mods/player and are usable in Theatre Ops.
/// </summary>
public partial class DevelopmentScreen : PanelContainer
{
    private static readonly (string Label, string Kind)[] Kinds = { ("Munition", "munition"), ("Ground system", "ground"), ("Ship", "ship"), ("Aircraft", "aircraft") };
    public static readonly Dictionary<string, Color> FamilyColor = new()
    {
        ["sensors"] = new("7fe3ff"), ["payload"] = new("ff6b6b"), ["fuzing"] = new("ff9f6b"), ["guidance"] = new("c49bff"),
        ["navigation"] = new("9bb8ff"), ["computing"] = new("5cf2b0"), ["comms"] = new("5cd0f2"), ["ew"] = new("ffd36b"),
        ["power"] = new("f2e15c"), ["propulsion"] = new("ffb347"), ["materials"] = new("8aa0b3"), ["structure"] = new("6f8599"),
        ["thermal"] = new("6be3ff"), ["interfaces"] = new("b3c7d6"), ["reliability"] = new("9ad0a8"),
    };

    private string _kind = "munition";
    private DesignDef _work = null!;
    private ItemList _designs = null!, _catalog = null!;
    private readonly List<string> _designIds = new(), _catIds = new();
    private LineEdit _name = null!;
    private OptionButton _frame = null!, _family = null!;
    private readonly List<string> _frameIds = new();
    private VBoxContainer _fitted = null!, _budgets = null!, _ratings = null!, _issues = null!, _effect = null!;
    private DesignVisual _viz = null!;
    private Label _status = null!;
    public event Action? DesignsChanged;

    private static Catalog Cat => Session.Content.Catalog;

    public override void _Ready()
    {
        AnchorRight = 1; AnchorBottom = 1;
        AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.02f, 0.05f, 0.09f, 0.97f), UiTheme.Line, 6));
        var root = new HBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        AddChild(root);

        // left: type + design list
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(290, 0) };
        root.AddChild(left);
        left.AddChild(UiTheme.Text("DEVELOPMENT", 20, UiTheme.Accent));
        var kinds = new GridContainer { Columns = 2 };
        var group = new ButtonGroup();
        foreach (var (label, kind) in Kinds)
        {
            var b = new Button { Text = label, ToggleMode = true, ButtonGroup = group, ButtonPressed = kind == _kind, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            b.Pressed += () => { _kind = kind; RefreshDesigns(null); };
            kinds.AddChild(b);
        }
        left.AddChild(kinds);
        left.AddChild(UiTheme.Text("Designs", 13, UiTheme.Dim));
        _designs = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        _designs.ItemSelected += i => Load(Session.Content.Designs[_designIds[(int)i]]);
        left.AddChild(_designs);
        var row = new HBoxContainer();
        var variant = new Button { Text = "New variant", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        variant.Pressed += NewVariant;
        var save = new Button { Text = "Save design", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        save.Pressed += Save;
        row.AddChild(variant); row.AddChild(save);
        left.AddChild(row);
        _status = UiTheme.Text("", 12, UiTheme.Dim, wrap: true);
        _status.CustomMinimumSize = new Vector2(280, 0);
        left.AddChild(_status);

        // centre: name, frame, visual, fitted parts, catalogue
        var mid = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(mid);
        var top = new HBoxContainer();
        _name = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, PlaceholderText = "Design name" };
        _name.TextChanged += t => _work = _work with { Name = t };
        _frame = new OptionButton { CustomMinimumSize = new Vector2(260, 0) };
        _frame.ItemSelected += i => { _work = _work with { Frame = _frameIds[(int)i] }; Recalc(); };
        top.AddChild(_name); top.AddChild(_frame);
        mid.AddChild(top);
        _viz = new DesignVisual { CustomMinimumSize = new Vector2(0, 190) };
        mid.AddChild(_viz);
        mid.AddChild(UiTheme.Text("Fitted components", 13, UiTheme.Dim));
        var fs = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.2f };
        _fitted = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        fs.AddChild(_fitted);
        mid.AddChild(fs);
        var addRow = new HBoxContainer();
        addRow.AddChild(UiTheme.Text("Add part", 13, UiTheme.Dim));
        _family = new OptionButton();
        _family.AddItem("All families");
        foreach (var f in Families.All) _family.AddItem(f);
        _family.ItemSelected += _ => RefreshCatalog();
        addRow.AddChild(_family);
        var add = new Button { Text = "Fit selected ▸" };
        add.Pressed += () => { foreach (var i in _catalog.GetSelectedItems()) Fit(_catIds[i]); };
        addRow.AddChild(add);
        mid.AddChild(addRow);
        _catalog = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        _catalog.ItemActivated += i => Fit(_catIds[(int)i]);
        mid.AddChild(_catalog);

        // right: evaluation
        var rs = new ScrollContainer { CustomMinimumSize = new Vector2(400, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 4);
        rs.AddChild(right);
        root.AddChild(rs);
        right.AddChild(UiTheme.Text("BUDGETS", 13, UiTheme.Accent));
        right.AddChild(_budgets = new VBoxContainer());
        right.AddChild(UiTheme.Text("CAPABILITY · 0–10", 13, UiTheme.Accent));
        right.AddChild(_ratings = new VBoxContainer());
        right.AddChild(UiTheme.Text("ISSUES", 13, UiTheme.Accent));
        right.AddChild(_issues = new VBoxContainer());
        right.AddChild(UiTheme.Text("COMPONENTS IN EFFECT", 13, UiTheme.Accent));
        right.AddChild(_effect = new VBoxContainer());

        RefreshDesigns(DevScreenshot.Arg("design"));
    }

    private void RefreshDesigns(string? select)
    {
        _designs.Clear(); _designIds.Clear();
        foreach (var d in Session.Content.Designs.Values.Where(d => Cat.Frames.TryGetValue(d.Frame, out var f) && f.Kind == _kind).OrderBy(d => d.Name))
        {
            _designIds.Add(d.Id);
            _designs.AddItem((d.Id.StartsWith("player-") ? "★ " : "") + d.Name);
        }
        var idx = select != null ? _designIds.IndexOf(select) : 0;
        if (_designIds.Count == 0) { Load(new DesignDef { Id = $"player-{_kind}-1", Name = "New design", Frame = Cat.Frames.Values.First(f => f.Kind == _kind).Id }); return; }
        idx = Math.Max(0, idx);
        _designs.Select(idx);
        Load(Session.Content.Designs[_designIds[idx]]);
    }

    private void Load(DesignDef d)
    {
        _work = d with { Components = d.Components.ToList() };
        _name.Text = d.Name;
        _frame.Clear(); _frameIds.Clear();
        foreach (var f in Cat.Frames.Values.Where(f => f.Kind == _kind))
        {
            _frameIds.Add(f.Id);
            _frame.AddItem(f.Name);
        }
        _frame.Select(Math.Max(0, _frameIds.IndexOf(d.Frame)));
        RefreshCatalog();
        Recalc();
    }

    private void RefreshCatalog()
    {
        _catalog.Clear(); _catIds.Clear();
        var fam = _family.Selected <= 0 ? null : Families.All[_family.Selected - 1];
        foreach (var c in Cat.Components.Values.Where(c => (c.FitsKinds.Length == 0 || c.FitsKinds.Contains(_kind)) && (fam == null || c.Family == fam)).OrderBy(c => c.Family).ThenBy(c => c.Name))
        {
            _catIds.Add(c.Id);
            var i = _catalog.AddItem($"{c.Name}   {Adds(c)}");
            _catalog.SetItemCustomFgColor(i, FamilyColor.GetValueOrDefault(c.Family, UiTheme.Ink));
            _catalog.SetItemTooltip(i, $"{c.Family} · uses {string.Join(", ", c.Uses.Select(u => $"{u.Key} {u.Value:0.##}"))}");
        }
    }

    private static string Adds(ComponentTemplate c) =>
        c.Adds.Count == 0 ? "" : string.Join("  ", c.Adds.Select(a => $"{(a.Value >= 0 ? "+" : "")}{a.Value:0.#} {Rating(a.Key)}"));

    private static string Rating(string id) => Cat.Ratings.TryGetValue(id, out var r) ? r.Name : id;

    private void Fit(string id) { _work.Components.Add(id); Recalc(); }

    private void Recalc()
    {
        var ev = new Evaluator(Cat).Evaluate(_work);
        _viz.Show(_work);

        Clear(_fitted);
        for (var i = 0; i < _work.Components.Count; i++)
        {
            var idx = i;
            var c = Cat.Components[_work.Components[i]];
            var h = new HBoxContainer();
            h.AddChild(new ColorRect { Color = FamilyColor.GetValueOrDefault(c.Family, UiTheme.Dim), CustomMinimumSize = new Vector2(6, 22) });
            h.AddChild(UiTheme.Text(c.Name, 14));
            var add = UiTheme.Text(Adds(c), 12, UiTheme.Dim);
            add.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            h.AddChild(add);
            var rm = new Button { Text = "✕", TooltipText = "Remove" };
            rm.Pressed += () => { _work.Components.RemoveAt(idx); Recalc(); };
            h.AddChild(rm);
            _fitted.AddChild(h);
        }

        Clear(_budgets);
        foreach (var (axis, (used, avail)) in ev.Budgets.Where(b => b.Value.Used > 0 || b.Value.Available > 0))
        {
            var h = new HBoxContainer();
            var n = UiTheme.Text(axis, 13); n.CustomMinimumSize = new Vector2(70, 0);
            var over = used > avail + 1e-9;
            var bar = Bar(avail <= 0 ? 1 : Math.Min(1, used / avail), over ? UiTheme.Bad : used / Math.Max(avail, 1e-9) >= 0.95 ? new Color("ffb347") : UiTheme.Accent);
            var v = UiTheme.Text($"{used:0.##} / {avail:0.##}", 12, over ? UiTheme.Bad : UiTheme.Dim); v.CustomMinimumSize = new Vector2(110, 0);
            h.AddChild(n); h.AddChild(bar); h.AddChild(v);
            _budgets.AddChild(h);
        }

        Clear(_ratings);
        foreach (var (r, v) in ev.Ratings.Where(r => r.Value > 0).OrderByDescending(r => r.Value))
        {
            var h = new HBoxContainer();
            var n = UiTheme.Text(Rating(r), 13); n.CustomMinimumSize = new Vector2(120, 0);
            h.AddChild(n); h.AddChild(Bar(v / 10, UiTheme.Accent)); h.AddChild(UiTheme.Text($"{v:0}", 13));
            _ratings.AddChild(h);
        }

        Clear(_issues);
        if (ev.Issues.Count == 0) _issues.AddChild(UiTheme.Text("✓ No issues. Ready to prototype.", 13, UiTheme.Good));
        foreach (var i in ev.Issues) _issues.AddChild(Wrap($"• {i.Text}", UiTheme.Bad));

        Clear(_effect);
        var prof = SystemProfile.Build(Cat, _work);
        foreach (var (r, v) in ev.Ratings.Where(r => r.Value > 0).OrderByDescending(r => r.Value))
        {
            var agg = Cat.Ratings.TryGetValue(r, out var rule) ? rule.Aggregate : "sum";
            var parts = prof.For(r).Select(c => $"{c.Source} {(c.Value >= 0 ? "+" : "")}{c.Value:0.#}");
            _effect.AddChild(Wrap($"{Rating(r)} {v:0} ({agg}) ← {string.Join(", ", parts)}", UiTheme.Ink));
        }
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
        l.CustomMinimumSize = new Vector2(380, 0);
        return l;
    }

    private static void Clear(Node n) { foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); } }

    private void NewVariant()
    {
        var n = 1;
        while (Session.Content.Designs.ContainsKey($"player-{_kind}-{n}")) n++;
        _work = _work with { Id = $"player-{_kind}-{n}", Name = _work.Name + " (variant)", Components = _work.Components.ToList(), Lineage = _work.Lineage };
        _name.Text = _work.Name;
        _status.Text = "New variant created. Swap parts, then Save design.";
    }

    private void Save()
    {
        if (!_work.Id.StartsWith("player-")) NewVariant(); // core designs are never overwritten
        Session.SaveDesign(_work with { Components = _work.Components.ToList() });
        _status.Text = $"Saved {_work.Name} to mods/player/designs.json. It is now available in Theatre Ops.";
        RefreshDesigns(_work.Id);
        DesignsChanged?.Invoke();
    }
}

/// <summary>A 2D drawing of a design built from its parts: munitions as a body split into sections, other systems as part tiles.</summary>
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
