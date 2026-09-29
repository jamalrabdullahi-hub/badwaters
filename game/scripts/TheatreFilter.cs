using Godot;

namespace BadWaters.Game;

/// <summary>
/// Theatre Ops · map filter (F1 layout): a drawer with presets, search, sides, an entity tree (domain → class →
/// sub-class, tri-state) and the overlays. View only: hidden units keep fighting; the picked unit always shows.
/// </summary>
public partial class TheatreOps
{
    /// <summary>Entity taxonomy: domain → (class, symbology codes).</summary>
    private static readonly (string Domain, (string Name, string[] Codes)[] Classes)[] Taxonomy =
    {
        ("Air", new[] { ("Fighters", new[] { "FTR", "5GEN" }), ("Strike fighters", new[] { "STK" }), ("Bombers", new[] { "BMR", "SB" }), ("AEW & control", new[] { "AEW" }),
            ("Tankers", new[] { "TKR" }), ("Maritime patrol", new[] { "MPA" }), ("Electronic warfare", new[] { "EW" }), ("Transport", new[] { "TPT" }),
            ("Drones", new[] { "MALE", "HALE", "UCAV" }), ("Helicopters", new[] { "HEL" }) }),
        ("Naval", new[] { ("Carriers", new[] { "CV" }), ("Amphibious", new[] { "LHD" }), ("Destroyers / cruisers", new[] { "DDG" }), ("Frigates", new[] { "FFG" }),
            ("Corvettes / patrol", new[] { "PC" }), ("Replenishment", new[] { "AOR" }), ("Uncrewed", new[] { "USV" }), ("Diesel submarines", new[] { "SSK" }), ("Nuclear submarines", new[] { "SSN" }) }),
        ("Ground forces", new[] { ("Air-defence batteries", new[] { "SAMB" }), ("Missile launchers (TEL)", new[] { "TEL" }), ("Radar sites", new[] { "RDR" }), ("Garrisons / C2", new[] { "C2" }) }),
        ("Munitions in flight", new[] { ("Ballistic", new[] { "SRBM", "MRBM" }), ("Hypersonic glide", new[] { "HGV" }), ("Cruise", new[] { "CM" }), ("Anti-ship", new[] { "ASM" }),
            ("Interceptors", new[] { "SAM", "AAM" }), ("Loitering", new[] { "LM" }) }),
        ("Facilities", new[] { ("Air bases", new[] { "AB" }), ("Naval bases & ports", new[] { "NB", "PRT" }), ("Missile & AD sites", new[] { "MB" }),
            ("Sensors, C2 & space", new[] { "GS" }), ("Logistics", new[] { "AMO", "POL" }), ("Industry & energy", new[] { "IND", "PWR", "YD" }) }),
        ("Protected (civil)", new[] { ("Cities & towns", new[] { "CITY" }) }),
        ("Neutral / civil traffic", new[] { ("Merchant shipping", new[] { "MER" }) }),
    };
    private static readonly (string Group, (string Key, string Name)[] Items)[] OverlayTree =
    {
        ("Courses & routes", new[] { ("routes", "Flight routes"), ("ship-courses", "Ship courses"), ("tracks", "Missile tracks"), ("logistics", "Supply routes & dry points") }),
        ("Air traffic", new[] { ("enemy-air", "Enemy air contacts") }),
        ("Sensors & weapons", new[] { ("radar", "Radar coverage"), ("reach", "Air-defence reach"), ("strike", "Strike reach"), ("jamming", "Jamming cover") }),
        ("Labels & naming", new[] { ("labels", "All labels"), ("names", "Unit names"), ("callsigns", "Callsigns"), ("base-names", "Base & site names") }),
        ("Planning", new[] { ("reports", "Report pins") }),
    };
    private static readonly string[] Presets = { "All", "Air war", "Maritime", "Strike planning", "Minimal" };
    private readonly HashSet<string> _hiddenCodes = new();
    private Tree? _filterTree;
    private bool _syncing;

    /// <summary>Should this entity be drawn? Class filter + side filter; the picked unit always shows.</summary>
    public bool ShowEntity(string code, string side, string id)
    {
        if (id != "" && Picked is { } p && p.Id == id) return true;
        return !_hiddenCodes.Contains(code) && Show("side-" + side);
    }

    private void ApplyPreset(string name)
    {
        _hiddenCodes.Clear();
        foreach (var k in Overlay.Keys.ToList()) Overlay[k] = true;
        IEnumerable<string> Dom(string d) => Taxonomy.First(t => t.Domain == d).Classes.SelectMany(c => c.Codes);
        switch (name)
        {
            case "Air war": foreach (var c in Dom("Naval").Concat(Dom("Facilities")).Except(new[] { "AB" })) _hiddenCodes.Add(c); Overlay["ship-courses"] = false; break;
            case "Maritime": foreach (var c in Dom("Ground forces").Concat(Dom("Facilities")).Except(new[] { "NB", "PRT" })) _hiddenCodes.Add(c);
                foreach (var c in new[] { "FTR", "5GEN", "STK", "BMR", "SB", "TPT", "HEL" }) _hiddenCodes.Add(c); Overlay["reach"] = false; break;
            case "Strike planning": foreach (var c in new[] { "TKR", "TPT", "HEL", "AOR", "USV" }) _hiddenCodes.Add(c); Overlay["tracks"] = false; break;
            case "Minimal": foreach (var c in Dom("Facilities")) _hiddenCodes.Add(c); foreach (var k in new[] { "radar", "reach", "strike", "names", "base-names", "reports" }) Overlay[k] = false; break;
        }
        FillFilterTree();
        Layer.QueueRedraw();
    }

    private void FillFilter(TacWindow w)
    {
        if (_filterTree != null) return; // built once; the tree keeps its own state
        var search = new LineEdit { PlaceholderText = "⌕ filter the list… e.g. carrier, launcher" };
        search.TextChanged += t => FilterSearch(t);
        w.Body.AddChild(search);
        var pr = new HFlowContainer();
        foreach (var p in Presets) { var b = new Button { Text = p }; b.AddThemeFontSizeOverride("font_size", 11); b.Pressed += () => ApplyPreset(p); pr.AddChild(b); }
        w.Body.AddChild(pr);
        _filterTree = new Tree { HideRoot = true, CustomMinimumSize = new Vector2(300, 520), SizeFlagsVertical = SizeFlags.ExpandFill };
        _filterTree.AddThemeFontSizeOverride("font_size", 11);
        _filterTree.ItemEdited += FilterEdited;
        w.Body.AddChild(_filterTree);
        w.Line("View only: hidden units keep fighting and alerts still fire. Your selected unit always shows.", UiTheme.TacDim);
        FillFilterTree();
    }

    private TreeItem Check(TreeItem parent, string text, bool on, string meta)
    {
        var it = _filterTree!.CreateItem(parent);
        it.SetCellMode(0, TreeItem.TreeCellMode.Check);
        it.SetEditable(0, true);
        it.SetText(0, text);
        it.SetChecked(0, on);
        it.SetMetadata(0, meta);
        return it;
    }

    private void FillFilterTree()
    {
        if (_filterTree == null) return;
        _syncing = true;
        _filterTree.Clear();
        var root = _filterTree.CreateItem();
        var sides = _filterTree.CreateItem(root); sides.SetText(0, "SIDES"); sides.SetSelectable(0, false);
        foreach (var s in new[] { "blue", "red" }) Check(sides, s == CommandSide ? $"{SideName(s)} (you · blue)" : Trial.AtWar ? $"{SideName(s)} (enemy · at war · red)" : $"{SideName(s)} (hostile · not at war · orange)", Show("side-" + s), "side-" + s);
        Check(sides, $"Neutral shipping (green) · {Trial.Neutrals.Count}", Show("side-neutral"), "side-neutral");
        var ent = _filterTree.CreateItem(root); ent.SetText(0, "ENTITIES"); ent.SetSelectable(0, false);
        foreach (var (dom, classes) in Taxonomy)
        {
            var d = Check(ent, dom, true, "dom:" + dom);
            foreach (var (name, codes) in classes) Check(d, name, codes.All(c => !_hiddenCodes.Contains(c)), "cls:" + string.Join(",", codes));
            SyncParent(d);
            d.Collapsed = dom != "Naval" && dom != "Air";
        }
        var ov = _filterTree.CreateItem(root); ov.SetText(0, "OVERLAYS"); ov.SetSelectable(0, false);
        foreach (var (g, items) in OverlayTree)
        {
            var gi = Check(ov, g, true, "grp");
            foreach (var (k, n) in items) Check(gi, n, Show(k), "ov:" + k);
            SyncParent(gi);
        }
        _syncing = false;
    }

    /// <summary>Tri-state parent: all on ■, some on ▪ (indeterminate), none □.</summary>
    private static void SyncParent(TreeItem parent)
    {
        var kids = parent.GetChildren().ToList();
        var on = kids.Count(k => k.IsChecked(0));
        parent.SetIndeterminate(0, on > 0 && on < kids.Count);
        parent.SetChecked(0, on == kids.Count);
    }

    private void FilterEdited()
    {
        if (_syncing || _filterTree?.GetEdited() is not { } it) return;
        _syncing = true;
        var on = it.IsChecked(0);
        void Apply(TreeItem x, bool v)
        {
            x.SetChecked(0, v); x.SetIndeterminate(0, false);
            var meta = x.GetMetadata(0).AsString();
            if (meta.StartsWith("cls:")) foreach (var c in meta[4..].Split(',')) { if (v) _hiddenCodes.Remove(c); else _hiddenCodes.Add(c); }
            else if (meta.StartsWith("ov:")) Overlay[meta[3..]] = v;
            else if (meta.StartsWith("side-")) Overlay[meta] = v;
            foreach (var k in x.GetChildren()) Apply(k, v);
        }
        Apply(it, on);
        if (it.GetParent() is { } p && p.GetCellMode(0) == TreeItem.TreeCellMode.Check) SyncParent(p);
        _syncing = false;
        Layer.QueueRedraw();
    }

    private void FilterSearch(string text)
    {
        if (_filterTree?.GetRoot() is not { } root) return;
        text = text.Trim().ToLowerInvariant();
        foreach (var group in root.GetChildren())
            foreach (var d in group.GetChildren())
            {
                var any = false;
                foreach (var c in d.GetChildren())
                {
                    var match = text == "" || c.GetText(0).ToLowerInvariant().Contains(text) || d.GetText(0).ToLowerInvariant().Contains(text);
                    c.Visible = match; any |= match;
                }
                d.Visible = any || text == "" || d.GetText(0).ToLowerInvariant().Contains(text);
                if (text != "" && any) d.Collapsed = false;
            }
    }
}
