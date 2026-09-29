using BadWaters.Sim.Command;
using BadWaters.Sim.Policy;
using BadWaters.Sim.Theatre;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// Theatre Ops · Mission creation (M1 wizard). One mission mixes air, ground and naval fires and support, all timed to
/// one munition ToT. Pages: objective, forces (with pylon loadouts and munition availability), support, timing, EMCON,
/// routes and patterns, ROE, review. Committing builds a player Operation on the shared board (one operation system).
/// </summary>
public partial class TheatreOps
{
    private sealed class AirRow
    {
        public string Preset = ""; public int Count = 2; public Mission Role = Mission.Strike; public Formation Formation = Formation.Wedge;
        /// <summary>Per aircraft; null = the role's default loadout.</summary>
        public Dictionary<string, StoreLoad>? Loadout;
    }
    private sealed class FireRow { public string Launcher = ""; public int Rounds = 2; }
    private sealed class MissionDraft
    {
        public string Name = "Operation";
        public string Purpose = "strike";
        public string Target = "";
        public string Aimpoint = "";
        public (double Lon, double Lat)? Point;
        public List<AirRow> Air = new();
        public List<AirRow> Support = new();
        /// <summary>Spare aircraft on ground alert: launched against the same target if the package takes losses.</summary>
        public List<AirRow> Spares = new();
        public List<FireRow> Fires = new();
        public double TotMin = 90; // minutes from now
        public double IpKm = 60;
        public string EmconPackage = "limited", EmconSupport = "limited";
        public string Roe = "tight";
        public bool AbortOnLosses = true; public double AbortAt = 0.3;
        public bool HoldIfUnsure = true; public bool RecallIfTankerLost;
        public int EditRow = -1; // air row whose loadout is open
    }

    private static readonly string[] Pages = { "1 Objective", "2 Forces", "3 Support", "4 Timing", "5 EMCON", "6 Routes & patterns", "7 ROE", "8 Review" };
    private static readonly (string Key, string Name)[] Purposes = { ("strike", "Strike"), ("sead", "SEAD / DEAD"), ("counter-air", "Counter-air (CAP)"), ("recon", "Recon / ISR"), ("area-defence", "Area defence"), ("sea-denial", "Sea denial (anti-ship)") };
    private static readonly Mission[] PackageRoles = { Mission.Strike, Mission.SEAD, Mission.Escort, Mission.CAP, Mission.Recon };
    private MissionDraft _md = new();
    private int _mPage;
    private string _mResult = "";

    private bool StationMission => _md.Purpose is "counter-air" or "area-defence" || _md.Target == "@pt";

    /// <summary>Open the wizard on a target (site id), or at a map point for a station mission, with a template.</summary>
    public void NewMission(string? siteId, string template, (double Lon, double Lat)? point = null)
    {
        _md = new MissionDraft
        {
            Name = $"Operation {Board.Ops.Count(o => o.Author == Author.Player) + 1}",
            Purpose = template switch { "sead" => "sead", "cap" or "aew" => "counter-air", "recon" => "recon", _ => "strike" },
            Target = siteId ?? (point != null ? "@pt" : Scenario.Sites.FirstOrDefault(s => s.Side != CommandSide && TargetPolicy.IsLegitimate(s.Class))?.Id ?? ""),
            Point = point,
        };
        TemplateRows(template);
        _mPage = siteId != null || point != null ? 1 : 0;
        _mResult = "";
        Open("MISSION");
    }

    private void TemplateRows(string t)
    {
        var idle = Trial.Air!.IdleByType(CommandSide).Select(x => (x.Preset, x.Count, P: Session.Content.PresetInfo(x.Preset)!)).Where(x => x.P != null).ToList();
        string? Best(string rating, Func<string, bool> code) => idle.Where(x => code(Symbols.For(x.Preset, "air"))).OrderByDescending(x => x.P.R(rating)).ThenByDescending(x => x.Count).Select(x => x.Preset).FirstOrDefault();
        bool Combat(string c) => c is "FTR" or "STK" or "5GEN" or "BMR" or "SB";
        void Add(List<AirRow> l, string? p, int n, Mission m, Formation f) { if (p != null) l.Add(new AirRow { Preset = p, Count = n, Role = m, Formation = f }); }
        _md.Air.Clear(); _md.Support.Clear(); _md.Fires.Clear();
        switch (t)
        {
            case "cap": Add(_md.Air, Best("air-defence", Combat), 2, Mission.CAP, Formation.LineAbreast); break;
            case "aew": Add(_md.Air, Best("air-defence", Combat), 2, Mission.CAP, Formation.LineAbreast); Add(_md.Support, Best("detection", c => c == "AEW"), 1, Mission.AEW, Formation.Trail); break;
            case "recon": Add(_md.Air, Best("detection", c => c == "MPA") ?? Best("detection", Combat), 1, Mission.Recon, Formation.Trail); break;
            default:
                Add(_md.Air, Best("strike", Combat), 4, Mission.Strike, Formation.Box);
                if (t == "sead") Add(_md.Air, Best("strike", Combat), 2, Mission.SEAD, Formation.LineAbreast);
                Add(_md.Air, Best("air-defence", Combat), 2, Mission.Escort, Formation.Wedge);
                Add(_md.Support, Best("reach", c => c == "TKR"), 1, Mission.Tanker, Formation.Trail);
                break;
        }
    }

    // ---------------- shared helpers ----------------
    /// <summary>A paragraph: wraps to the window width (use L for labels inside a row).</summary>
    private static Label Para(string text, Color? c = null) { var l = L(text, c); l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.SizeFlagsHorizontal = SizeFlags.ExpandFill; l.CustomMinimumSize = new Vector2(880, 0); return l; }
    private static Label L(string text, Color? c = null, int size = 11) { var l = new Label { Text = text }; l.AddThemeColorOverride("font_color", c ?? UiTheme.TacInk); l.AddThemeFontSizeOverride("font_size", size); return l; }
    private static Label Hd(string text) => L(text.ToUpperInvariant(), UiTheme.TacAccent, 11);
    private static Button B(string text, Action a, bool on = false, string tip = "")
    {
        var b = new Button { Text = text, ToggleMode = on, ButtonPressed = on, TooltipText = tip };
        b.AddThemeFontSizeOverride("font_size", 11);
        b.Pressed += a;
        return b;
    }
    private HBoxContainer Seg(string label, string[] opts, string cur, Action<string> set, float labelW = 150)
    {
        var h = new HBoxContainer();
        var l = L(label, UiTheme.TacDim); l.CustomMinimumSize = new Vector2(labelW, 0); l.AutowrapMode = TextServer.AutowrapMode.Off;
        h.AddChild(l);
        foreach (var o in opts) h.AddChild(B(o, () => { set(o); RefreshMission(); }, o == cur));
        return h;
    }
    private OptionButton Opt(IEnumerable<(string Id, string Text)> items, string cur, Action<string> set, float w = 0)
    {
        var o = new OptionButton { ClipText = true, CustomMinimumSize = new Vector2(w, 0) };
        o.AddThemeFontSizeOverride("font_size", 11);
        var ids = new List<string>();
        foreach (var (id, t) in items) { ids.Add(id); o.AddItem(t); }
        o.Select(Math.Max(0, ids.IndexOf(cur)));
        o.ItemSelected += i => { set(ids[(int)i]); RefreshMission(); };
        return o;
    }
    private SpinBox Spin(double min, double max, double v, Action<double> set, string suffix = "", double step = 1)
    {
        var s = new SpinBox { MinValue = min, MaxValue = max, Value = v, Step = step, Suffix = suffix, CustomMinimumSize = new Vector2(84, 0) };
        s.ValueChanged += x => set(x);
        return s;
    }
    private void RefreshMission() { if (_windows.TryGetValue("MISSION", out var w)) w.Refresh?.Invoke(); }

    private string PresetName(string p) => Session.Content.PresetInfo(p)?.Name ?? p;
    private StoresCatalog? Cat => Trial.Air?.Stores;
    private bool ShipTarget => _md.Target.StartsWith("ship:");
    private (double Lon, double Lat) Aim() =>
        _md.Target == "@pt" && _md.Point is { } pt ? pt
        : ShipTarget && Trial.Intel?.Pictures[CommandSide].Contacts.GetValueOrDefault(_md.Target[5..]) is { } c ? (c.Lon, c.Lat)
        : Trial.Site(_md.Target) is { } s ? (s.Lon, s.Lat) : (0, 0);

    /// <summary>Base the planner will most likely fly this row from: closest base with enough idle aircraft of the type.</summary>
    private string LikelyBase(AirRow r)
    {
        var aim = Aim();
        return Trial.Air!.Idle(CommandSide).Where(u => u.Preset == r.Preset).GroupBy(u => u.Base).Where(g => g.Count() >= r.Count)
            .Select(g => Trial.Site(g.Key)).Where(s => s != null).OrderBy(s => Pacing.DistanceKm(s!.Lon, s.Lat, aim.Lon, aim.Lat)).FirstOrDefault()?.Id ?? "";
    }
    private Dictionary<string, StoreLoad> LoadOf(AirRow r) => r.Loadout ?? Cat?.Default(r.Preset, r.Role) ?? new();
    private int TheatreStock(string store) => Trial.Air!.Munitions.Theatre(Trial.Air.BasesOf(CommandSide), store);

    // ---------------- window ----------------
    private void FillMission(TacWindow w)
    {
        var steps = new HBoxContainer();
        for (var i = 0; i < Pages.Length; i++) { var k = i; steps.AddChild(B((i < _mPage ? "✓ " : "") + Pages[i], () => { _mPage = k; RefreshMission(); }, i == _mPage)); }
        w.Body.AddChild(steps);
        w.Body.AddChild(L($"{_md.Name} · author YOU · draft", UiTheme.TacDim));
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 5);
        w.Body.AddChild(body);
        switch (_mPage)
        {
            case 0: PageObjective(body); break;
            case 1: PageForces(body); break;
            case 2: PageSupport(body); break;
            case 3: PageTiming(body); break;
            case 4: PageEmcon(body); break;
            case 5: PageRoutes(body); break;
            case 6: PageRoe(body); break;
            default: PageReview(body); break;
        }
        var nav = new HBoxContainer();
        if (_mPage > 0) nav.AddChild(B("◂ BACK", () => { _mPage--; RefreshMission(); }));
        nav.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        nav.AddChild(B("PREVIEW ON MAP", () => { Validate(preview: true); RefreshMission(); }));
        if (_mPage < Pages.Length - 1) nav.AddChild(B("NEXT ▸", () => { _mPage++; RefreshMission(); }));
        else nav.AddChild(B("COMMIT & EXECUTE", CommitMission));
        w.Body.AddChild(nav);
    }

    private void PageObjective(VBoxContainer v)
    {
        v.AddChild(Hd("Objective"));
        var name = new LineEdit { Text = _md.Name, CustomMinimumSize = new Vector2(320, 0) };
        name.TextChanged += t => _md.Name = t;
        v.AddChild(name);
        v.AddChild(Seg("Purpose", Purposes.Select(p => p.Name).ToArray(), Purposes.First(p => p.Key == _md.Purpose).Name, n => _md.Purpose = Purposes.First(p => p.Name == n).Key));
        var targets = new List<(string, string)>();
        if (_md.Point is { } pt) targets.Add(("@pt", $"Map point {pt.Lat:0.00}, {pt.Lon:0.00} (station)"));
        var pic0 = Trial.Intel?.Pictures[CommandSide];
        if (pic0 != null)
            foreach (var c in pic0.Contacts.Values.Where(c => c.Kind == "ship" && Trial.Time - c.LastSeen < IntelRules.StaleAfter && !(Trial.Naval?.IsSunk(c.Key) ?? true)))
                targets.Add(("ship:" + c.Key, $"⚓ {c.Label} (ship contact · seen {(Trial.Time - c.LastSeen) / 60:0} min ago)"));
        foreach (var s in Scenario.Sites.Where(s => TargetPolicy.IsLegitimate(s.Class)).OrderBy(s => s.Side == CommandSide).ThenBy(s => s.Name))
            targets.Add((s.Id, s.Side == CommandSide ? $"{s.Name} (own · station over it)" : s.Name));
        var h = new HBoxContainer(); h.AddChild(L("Target", UiTheme.TacDim)); h.AddChild(Opt(targets, _md.Target, t => { _md.Target = t; _md.Aimpoint = ""; }, 360)); v.AddChild(h);
        if (Trial.Bases.TryGetValue(_md.Target, out var b) && b.Site.Side != CommandSide)
        {
            var pic = Trial.Intel?.Pictures[CommandSide];
            var aims = new List<(string, string)> { ("", "Aimpoint: doctrine (auto)") };
            aims.AddRange(b.Components.Values.Select(c => (c.Def.Id, $"{c.Def.Name} · est. {pic?.Belief(b.Site.Id, c.Def.Id).Integrity:0}%")));
            var a = new HBoxContainer(); a.AddChild(L("Aimpoint", UiTheme.TacDim)); a.AddChild(Opt(aims, _md.Aimpoint, x => _md.Aimpoint = x, 360)); v.AddChild(a);
        }
        v.AddChild(Para("Only military targets are listed: bases and sites, and ship contacts your side currently holds. Population centres, civil shelters and neutral shipping are never targets.", UiTheme.Good));
        if (ShipTarget) v.AddChild(Para("Ship target: weapons fly to where your picture last saw it. If it moves out of the seeker's search basket they miss, so strike soon after a fresh contact. Use anti-ship loadouts (page 2 → LOADOUT → Anti-ship).", UiTheme.TacDim));
        v.AddChild(Para("Start from a template:", UiTheme.TacDim));
        var t = new HBoxContainer();
        foreach (var (k, n) in new[] { ("strike", "Strike package"), ("sead", "SEAD strike"), ("cap", "CAP"), ("aew", "AEW + CAP"), ("recon", "Recon") }) t.AddChild(B(n, () => { TemplateRows(k); RefreshMission(); }));
        v.AddChild(t);
    }

    private void PageForces(VBoxContainer v)
    {
        v.AddChild(Hd("Air · package"));
        AirTable(v, _md.Air, PackageRoles, allowLoadout: true);
        v.AddChild(Hd("Ground & naval fires (timed to the same ToT)"));
        FireTable(v, ground: true);
        FireTable(v, ground: false);
        if (_md.EditRow >= 0 && _md.EditRow < _md.Air.Count) LoadoutEditor(v, _md.Air[_md.EditRow]);
        v.AddChild(Para("Committing takes these units from the AI pool until the mission ends, is released or aborts.", UiTheme.TacDim));
    }

    private void AirTable(VBoxContainer v, List<AirRow> rows, Mission[] roles, bool allowLoadout)
    {
        var idle = Trial.Air!.IdleByType(CommandSide).OrderBy(x => x.Name).ToList();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i]; var idx = i;
            var h = new HBoxContainer();
            h.AddChild(Opt(idle.Select(x => (x.Preset, $"{x.Name} ({x.Count} ready)")), r.Preset, p => { r.Preset = p; r.Loadout = null; }, 250));
            h.AddChild(Spin(1, 8, r.Count, x => r.Count = (int)x, "×"));
            h.AddChild(Opt(roles.Select(m => (m.ToString(), m.ToString())), r.Role.ToString(), m => { r.Role = Enum.Parse<Mission>(m); r.Loadout = null; }, 90));
            if (allowLoadout && Cat?.Template(r.Preset) != null)
                h.AddChild(B(_md.EditRow == idx && rows == _md.Air ? "LOADOUT ▴" : "LOADOUT ▾", () => { _md.EditRow = _md.EditRow == idx ? -1 : idx; RefreshMission(); }));
            h.AddChild(B("✕", () => { rows.RemoveAt(idx); if (_md.EditRow >= rows.Count) _md.EditRow = -1; RefreshMission(); }));
            v.AddChild(h);
            if (Cat != null && Cat.Template(r.Preset) != null)
            {
                var load = LoadOf(r);
                var bse = LikelyBase(r);
                var short_ = load.Values.Where(l => l.Qty > 0).GroupBy(l => l.Store).Where(g => Trial.Air.Munitions.At(bse, g.Key) < g.Sum(x => x.Qty) * r.Count).Select(g => Cat.Stores[g.Key].Name).ToList();
                v.AddChild(Para($"    carries (each): {Cat.Describe(load)} · from {Trial.Site(bse)?.Name ?? "no base with enough idle aircraft"}" + (short_.Count > 0 ? $" · SHORT: {string.Join(", ", short_)}" : ""), short_.Count > 0 ? UiTheme.Bad : UiTheme.TacDim));
            }
        }
        if (idle.Count > 0) v.AddChild(B("+ add aircraft", () => { rows.Add(new AirRow { Preset = idle[0].Preset, Role = roles[0] }); RefreshMission(); }));
    }

    /// <summary>Pylon-by-pylon loadout for one row, with base and theatre-wide availability of each store.</summary>
    private void LoadoutEditor(VBoxContainer v, AirRow r)
    {
        var cat = Cat!; var t = cat.Template(r.Preset)!;
        r.Loadout ??= LoadOf(r);
        var bse = LikelyBase(r);
        var box = new PanelContainer();
        var bv = new VBoxContainer(); box.AddChild(bv);
        bv.AddChild(Hd($"Loadout · {r.Count}× {PresetName(r.Preset)} ({t.Name}) · arming at {Trial.Site(bse)?.Name ?? "—"}"));
        var pre = new HBoxContainer();
        pre.AddChild(L("Presets", UiTheme.TacDim));
        foreach (var name in t.Loadouts.Keys) pre.AddChild(B(name, () => { r.Loadout = cat.Default(r.Preset, name); RefreshMission(); }));
        pre.AddChild(B("Clean", () => { r.Loadout = new(); RefreshMission(); }));
        bv.AddChild(pre);
        var head = new HBoxContainer();
        foreach (var (txt, wd) in new[] { ("STATION", 130f), ("STORE", 230f), ("QTY", 84f), ("NEED", 60f), ("AT BASE", 70f), ("THEATRE", 70f) }) { var l = L(txt, UiTheme.TacDim, 10); l.CustomMinimumSize = new Vector2(wd, 0); head.AddChild(l); }
        bv.AddChild(head);
        foreach (var st in t.Stations)
        {
            var h = new HBoxContainer();
            var sl = L($"{st.Id} · {st.Name}"); sl.CustomMinimumSize = new Vector2(130, 0); h.AddChild(sl);
            var cur = r.Loadout.TryGetValue(st.Id, out var ld) ? ld : new StoreLoad("", 0);
            var fits = new List<(string, string)> { ("", "— empty —") };
            fits.AddRange(cat.Fits(r.Preset, st).Select(s => (s.Id, $"{s.Name} [{s.Kind}]")));
            var sid = st.Id;
            h.AddChild(Opt(fits, cur.Store, s => { if (s == "") r.Loadout.Remove(sid); else r.Loadout[sid] = new StoreLoad(s, Math.Max(1, Math.Min(st.MaxQty, cur.Qty))); }, 230));
            if (cur.Store != "")
            {
                h.AddChild(Spin(1, st.MaxQty, cur.Qty, q => { r.Loadout[sid] = new StoreLoad(cur.Store, (int)q); RefreshMission(); }));
                var need = r.Loadout.Values.Where(x => x.Store == cur.Store).Sum(x => x.Qty) * r.Count;
                var atBase = Trial.Air!.Munitions.At(bse, cur.Store);
                foreach (var (txt, c) in new[] { ($"{need}", UiTheme.TacInk), ($"{atBase}", atBase >= need ? UiTheme.Good : UiTheme.Bad), ($"{TheatreStock(cur.Store)}", UiTheme.TacInk) })
                { var l = L(txt, c); l.CustomMinimumSize = new Vector2(c == UiTheme.TacInk && txt == $"{need}" ? 60 : 70, 0); h.AddChild(l); }
            }
            bv.AddChild(h);
        }
        var bad = cat.Validate(r.Preset, r.Loadout);
        bv.AddChild(Para(bad.Count > 0 ? "✕ " + string.Join("; ", bad) : $"Each aircraft: {cat.Describe(r.Loadout)}. Arming draws {r.Count}× that from the base when the mission launches.", bad.Count > 0 ? UiTheme.Bad : UiTheme.TacDim));
        bv.AddChild(Para("Game ratings: accuracy and punch per store set strike results; the best air-to-air missile carried sets air combat odds.", UiTheme.TacDim));
        v.AddChild(box);
    }

    private void FireTable(VBoxContainer v, bool ground)
    {
        var launchers = Trial.Launchers.Where(l => l.Def.Side == CommandSide && l.IsShip != ground).ToList();
        if (launchers.Count == 0) { v.AddChild(Para(ground ? "  no ground launchers" : "  no naval launchers", UiTheme.TacDim)); return; }
        var aim = Aim();
        foreach (var fr in _md.Fires.Where(f => Trial.Launchers.First(l => l.Def.Id == f.Launcher).IsShip != ground).ToList())
        {
            var l = Trial.Launchers.First(x => x.Def.Id == fr.Launcher);
            var h = new HBoxContainer();
            h.AddChild(Opt(launchers.Select(x => (x.Def.Id, $"{(x.IsShip ? "⚓ " : "")}{x.Def.Name} · {x.Missile.Name}")), fr.Launcher, id => fr.Launcher = id, 300));
            h.AddChild(Spin(1, 16, fr.Rounds, x => fr.Rounds = (int)x, " rds"));
            var theatre = Trial.Launchers.Where(x => x.Def.Side == CommandSide && x.Missile.DesignId == l.Missile.DesignId).Sum(x => x.Stock);
            var d = Pacing.DistanceKm(l.Def.Lon, l.Def.Lat, aim.Lon, aim.Lat);
            h.AddChild(L($"launcher {l.Stock} · theatre {theatre}{(d > l.ReachKm ? $" · OUT OF REACH ({d:0}/{l.ReachKm:0} km)" : "")}", l.Stock < fr.Rounds || d > l.ReachKm ? UiTheme.Bad : UiTheme.TacDim));
            h.AddChild(B("✕", () => { _md.Fires.Remove(fr); RefreshMission(); }));
            v.AddChild(h);
        }
        v.AddChild(B(ground ? "+ add ground fires" : "+ add naval fires", () =>
        {
            var pick = launchers.OrderBy(x => Pacing.DistanceKm(x.Def.Lon, x.Def.Lat, aim.Lon, aim.Lat) > x.ReachKm).First();
            _md.Fires.Add(new FireRow { Launcher = pick.Def.Id });
            RefreshMission();
        }));
    }

    private void PageSupport(VBoxContainer v)
    {
        v.AddChild(Hd("Rear support (tankers, AEW)"));
        AirTable(v, _md.Support, new[] { Mission.Tanker, Mission.AEW, Mission.Jamming }, allowLoadout: false);
        var hasT = _md.Support.Any(r => r.Role == Mission.Tanker);
        v.AddChild(Para(hasT ? "✓ Tanker: +300 km combat radius for the package; orbits 45% of the way to the objective." : "No tanker: aircraft fly on internal fuel (combat radius only).", hasT ? UiTheme.Good : UiTheme.TacDim));
        v.AddChild(Para(_md.Support.Any(r => r.Role == Mission.AEW) ? "✓ AEW orbit on the way in: better detection and +5% air combat for flights within 400 km." : "No AEW of our own in this mission (theatre AEW may still cover it).", UiTheme.TacDim));
        v.AddChild(Para(_md.Support.Any(r => r.Role == Mission.Jamming)
            ? $"✓ Stand-off jamming: orbits 65% of the way in; enemy radars within {AirPacing.JamKm:0} km work at 60% (range and detection) and SAM losses drop. It is heard by enemy passive sensors and is a target: escort it."
            : "No jamming support: add an EW aircraft (role Jamming) to blunt enemy radars around the target.", UiTheme.TacDim));
        v.AddChild(Hd("Spare aircraft (ground alert)"));
        AirTable(v, _md.Spares, new[] { Mission.Strike, Mission.SEAD, Mission.Escort }, allowLoadout: true);
        v.AddChild(Para("Spares stay on the ground, out of the AI pool. IF the package takes any losses THEN they launch against the same target, 15 minutes behind the main ToT.", UiTheme.TacDim));
        v.AddChild(Hd("Rescue (combat search and rescue)"));
        var aim = Aim();
        var helis = Trial.Air!.Idle(CommandSide).Where(u => (Session.Content.PresetInfo(u.Preset)?.R("rescue") ?? 0) > 0)
            .Select(u => (u, b: Trial.Site(u.Base))).Where(x => x.b != null).Select(x => (x.u, x.b, d: Pacing.DistanceKm(x.b!.Lon, x.b.Lat, aim.Lon, aim.Lat))).OrderBy(x => x.d).ToList();
        var cover = helis.Where(x => x.d <= AirPacing.RescueKm).ToList();
        v.AddChild(Para(cover.Count > 0
            ? $"✓ {cover.Count} rescue helicopter(s) on alert within {AirPacing.RescueKm:0} km of the target ({string.Join(", ", cover.Take(3).Select(x => $"{x.u.Name} at {x.b!.Name}, {x.d:0} km"))}). They launch by themselves to recover downed crews."
            : helis.Count > 0 ? $"⚠ Nearest rescue helicopter is {helis[0].d:0} km away ({helis[0].u.Name} at {helis[0].b!.Name}): crews downed near the target won't be recovered." : "⚠ No rescue helicopters in theatre.",
            cover.Count > 0 ? UiTheme.Good : new Color("ff9a3c")));
    }

    private void PageTiming(VBoxContainer v)
    {
        v.AddChild(Hd("Timing · everything worked back from one munition ToT"));
        var h = new HBoxContainer();
        h.AddChild(L("Munition ToT in", UiTheme.TacDim)); h.AddChild(Spin(10, 600, _md.TotMin, x => _md.TotMin = x, " min", 5));
        h.AddChild(L($"= T+{T(Trial.Time + _md.TotMin * 60)}", UiTheme.TacAccent));
        v.AddChild(h);
        if (StationMission) v.AddChild(Para("Station missions: aircraft take off as soon as ready and hold the station 90 minutes.", UiTheme.TacDim));
        var r = Validate(preview: false);
        foreach (var (name, a, b) in r.Timeline.OrderBy(x => x.A))
            v.AddChild(Para($"{name,-36} T+{T(a)} → T+{T(b)}"));
        if (r.Issues.Count > 0) v.AddChild(Para("• " + string.Join("\n• ", r.Issues), UiTheme.Bad));
    }

    private void PageEmcon(VBoxContainer v)
    {
        v.AddChild(Hd("EMCON · emission control"));
        var opts = new[] { "silent", "limited", "active" };
        v.AddChild(Seg("Package (strike, escort)", opts, _md.EmconPackage, x => _md.EmconPackage = x, 190));
        v.AddChild(Seg("Support (tanker, AEW)", opts, _md.EmconSupport, x => _md.EmconSupport = x, 190));
        v.AddChild(Para("Silent: no radar detection, and enemy passive sensors can't hear the flight.\nLimited / active: detects normally; AEW and ships that emit are heard at 350 km.\nA silent AEW is pointless: it only sees with its radar.", UiTheme.TacDim));
    }

    private void PageRoutes(VBoxContainer v)
    {
        v.AddChild(Hd("Routes & flight pattern"));
        var h = new HBoxContainer(); h.AddChild(L("IP distance before target", UiTheme.TacDim)); h.AddChild(Spin(20, 150, _md.IpKm, x => _md.IpKm = x, " km", 10)); v.AddChild(h);
        foreach (var r in _md.Air.Concat(_md.Support))
        {
            var row = new HBoxContainer();
            var l = L($"{r.Count}× {PresetName(r.Preset)} ({r.Role})"); l.CustomMinimumSize = new Vector2(260, 0); row.AddChild(l);
            row.AddChild(Opt(Formations.Select(f => (f.ToString(), f.ToString())), r.Formation.ToString(), f => r.Formation = Enum.Parse<Formation>(f), 130));
            row.AddChild(L(FormationRules.Mod(r.Formation).Text, UiTheme.TacDim, 10));
            v.AddChild(row);
        }
        v.AddChild(Para("Routes are computed: ingress bends off the direct line, IP on the attack axis, egress on the other side. PREVIEW ON MAP draws them.", UiTheme.TacDim));
    }

    private void PageRoe(VBoxContainer v)
    {
        v.AddChild(Hd("Rules of engagement"));
        v.AddChild(Seg("Weapons", new[] { "hold", "tight", "free" }, _md.Roe, x => _md.Roe = x));
        v.AddChild(Para("hold: fly the route, don't release · tight: identified hostiles and assigned targets only · free: engage any hostile", UiTheme.TacDim));
        var a = new CheckBox { Text = "IF losses exceed", ButtonPressed = _md.AbortOnLosses }; a.Toggled += x => _md.AbortOnLosses = x;
        var ah = new HBoxContainer(); ah.AddChild(a); ah.AddChild(Spin(10, 90, _md.AbortAt * 100, x => _md.AbortAt = x / 100, " %", 5)); ah.AddChild(L("THEN abort and return to base")); v.AddChild(ah);
        var u = new CheckBox { Text = "IF target confidence < 50% THEN hold weapons", ButtonPressed = _md.HoldIfUnsure }; u.Toggled += x => _md.HoldIfUnsure = x; v.AddChild(u);
        var tk = new CheckBox { Text = "IF tanker unavailable THEN abort", ButtonPressed = _md.RecallIfTankerLost }; tk.Toggled += x => _md.RecallIfTankerLost = x; v.AddChild(tk);
        v.AddChild(Hd("Fixed rules"));
        v.AddChild(Para("Population centres and civil shelters are never targets · neutral and civil traffic is never engaged · no nuclear play.", UiTheme.TacDim));
    }

    private void PageReview(VBoxContainer v)
    {
        v.AddChild(Hd("Review"));
        var r = Validate(preview: false);
        v.AddChild(Para($"{_md.Name} · {Purposes.First(p => p.Key == _md.Purpose).Name} → {(StationMission ? "station" : ShipTarget ? "ship contact" : Trial.Site(_md.Target)?.Name)} · ToT T+{T(Trial.Time + _md.TotMin * 60)}"));
        foreach (var a in _md.Air) v.AddChild(Para($"  {a.Count}× {PresetName(a.Preset)} · {a.Role} · {a.Formation} · {Cat?.Describe(LoadOf(a)) ?? ""}"));
        foreach (var a in _md.Support) v.AddChild(Para($"  {a.Count}× {PresetName(a.Preset)} · {a.Role}"));
        foreach (var a in _md.Spares) v.AddChild(Para($"  spare: {a.Count}× {PresetName(a.Preset)} · {a.Role} (launch on losses)"));
        foreach (var f in _md.Fires) { var l = Trial.Launchers.First(x => x.Def.Id == f.Launcher); v.AddChild(Para($"  {l.Def.Name} · {f.Rounds}× {l.Missile.Name}")); }
        v.AddChild(Para($"EMCON package {_md.EmconPackage}, support {_md.EmconSupport} · ROE {_md.Roe}"));
        v.AddChild(Para(r.Issues.Count == 0 ? "✓ Plan valid: every element has a route and a time." : "• " + string.Join("\n• ", r.Issues), r.Issues.Count == 0 ? UiTheme.Good : UiTheme.Bad));
        if (_mResult != "") v.AddChild(Para(_mResult, UiTheme.TacAccent));
    }

    // ---------------- build, validate, commit ----------------
    private sealed record MissionCheck(List<string> Issues, List<(string Name, double A, double B)> Timeline);

    private (AtoEntry? Package, AtoEntry? Support, List<AtoEntry> Fires) Specs()
    {
        var tot = Trial.Time + _md.TotMin * 60;
        var aim = Aim();
        var station = StationMission;
        FlightRequest Req(AirRow r) => new(r.Preset, r.Count, station && r.Role is Mission.Strike or Mission.SEAD or Mission.Escort ? Mission.CAP : r.Role, r.Formation) { Loadout = r.Loadout };
        var hasTanker = _md.Support.Any(r => r.Role == Mission.Tanker);
        AtoEntry? pkg = _md.Air.Count == 0 ? null : station
            ? new AtoEntry { Side = CommandSide, Name = _md.Name, StationLon = aim.Lon, StationLat = aim.Lat, Start = Trial.Time, Duration = 5400, Flights = _md.Air.Select(Req).ToList() }
            : new AtoEntry { Side = CommandSide, Name = _md.Name, Target = _md.Target, Tot = tot, IpKm = _md.IpKm, Aimpoint = _md.Aimpoint, Tanker = hasTanker, Flights = _md.Air.Select(Req).ToList() };
        AtoEntry? sup = null;
        if (_md.Support.Count > 0)
        {
            var aew = _md.Support.Where(r => r.Role == Mission.AEW).Select(Req).ToList();
            var tank = _md.Support.Where(r => r.Role is Mission.Tanker or Mission.Jamming).Select(Req).ToList();
            // tankers ride with the strike timing; AEW orbits on the way in
            sup = station || tank.Count == 0
                ? new AtoEntry { Side = CommandSide, Name = _md.Name + " support", StationLon = aim.Lon, StationLat = aim.Lat, Start = Trial.Time, Duration = 5400, Flights = aew.Concat(tank).ToList() }
                : new AtoEntry { Side = CommandSide, Name = _md.Name + " support", Target = _md.Target, Tot = tot, Tanker = true, Flights = tank.Concat(aew).ToList() };
        }
        var fires = _md.Fires.Select(f => new AtoEntry { Side = CommandSide, Launcher = f.Launcher, Target = _md.Target, Tot = tot, Count = f.Rounds, Aimpoint = ShipTarget ? "" : _md.Aimpoint }).ToList();
        return (pkg, sup, station ? new List<AtoEntry>() : fires);
    }

    private AtoEntry? SpareSpec()
    {
        if (_md.Spares.Count == 0 || StationMission) return null;
        return new AtoEntry { Side = CommandSide, Name = _md.Name + " spares", Target = _md.Target, Tot = Trial.Time + _md.TotMin * 60 + 900, IpKm = _md.IpKm, Aimpoint = _md.Aimpoint,
            Tanker = _md.Support.Any(r => r.Role == Mission.Tanker), Flights = _md.Spares.Select(r => new FlightRequest(r.Preset, r.Count, r.Role, r.Formation) { Loadout = r.Loadout }).ToList() };
    }

    /// <summary>Launch time for a fire mission on a ship contact so the rounds arrive at the ToT.</summary>
    private double ShipLaunchAt(AtoEntry f)
    {
        var l = Trial.Launchers.First(x => x.Def.Id == f.Launcher);
        var aim = Aim();
        return Math.Max(Trial.Time, f.Tot - Pacing.DistanceKm(l.Def.Lon, l.Def.Lat, aim.Lon, aim.Lat) / Pacing.SpeedKmPerSec(l.Missile.R("speed")));
    }

    private MissionCheck Validate(bool preview)
    {
        var issues = new List<string>(); var tl = new List<(string, double, double)>();
        if (_md.Air.Count + _md.Fires.Count == 0) issues.Add("Add aircraft or fires on page 2.");
        if (!Trial.AtWar && !StationMission) issues.Add(InterceptTrial.NotAtWar);
        if (!StationMission && Trial.Site(_md.Target) is { } s && !TargetPolicy.IsLegitimate(s.Class)) issues.Add(TargetPolicy.ProtectedMessage);
        if (StationMission && _md.Fires.Count > 0) issues.Add("Fires need a target: station missions carry no fire missions.");
        var (pkg, sup, fires) = Specs();
        if (preview) Preview.Clear();
        foreach (var (spec, label) in new[] { (pkg, "package"), (sup, "support") }.Where(x => x.Item1 != null))
        {
            var r = Trial.Air!.Plan(spec!);
            issues.AddRange(r.Issues.Select(i => $"{label}: {i}"));
            foreach (var f in r.Flights) tl.Add(($"{f.Callsign} {f.Aircraft.Count}× {f.Type.Name} {f.Mission}", f.Takeoff, f.LandAt));
            if (preview) Preview.AddRange(r.Flights);
        }
        if (SpareSpec() is { } sp)
        {
            var r = Trial.Air!.Plan(sp);
            issues.AddRange(r.Issues.Select(i => $"spares: {i}"));
            foreach (var f in r.Flights) tl.Add(($"(spare, on losses) {f.Callsign} {f.Aircraft.Count}× {f.Type.Name}", f.Takeoff, f.LandAt));
        }
        foreach (var f in fires.Where(_ => ShipTarget))
        {
            var l = Trial.Launchers.First(x => x.Def.Id == f.Launcher);
            var aim = Aim(); var d = Pacing.DistanceKm(l.Def.Lon, l.Def.Lat, aim.Lon, aim.Lat);
            if (!NavalRules.CanHitShips(l.Missile)) issues.Add($"fires: {l.Missile.Name} can't find a ship.");
            else if (d > l.ReachKm) issues.Add($"fires: {l.Def.Name} is {d:0} km from the ship, reach {l.ReachKm:0} km.");
            else if (l.Stock < f.Count) issues.Add($"fires: {l.Def.Name} has {l.Stock} rounds.");
            else tl.Add(($"{l.Def.Name} {f.Count}× {l.Missile.Name} (anti-ship)", ShipLaunchAt(f), f.Tot));
        }
        foreach (var f in fires.Where(_ => !ShipTarget))
        {
            var r = Trial.Air!.Plan(f);
            issues.AddRange(r.Issues.Select(i => $"fires: {i}"));
            var l = Trial.Launchers.First(x => x.Def.Id == f.Launcher);
            if (r.Ok) tl.Add(($"{l.Def.Name} {f.Count}× {l.Missile.Name}", f.Tot - 60, f.Tot));
        }
        if (preview) Layer.QueueRedraw();
        return new MissionCheck(issues, tl);
    }

    private void CommitMission()
    {
        var chk = Validate(preview: false);
        if (chk.Issues.Count > 0) { _mResult = "Not committed: fix the issues above."; RefreshMission(); return; }
        var (pkg, sup, fires) = Specs();
        var op = new Operation
        {
            Id = Board.NewId(CommandSide), Name = _md.Name, Side = CommandSide, Author = Author.Player, Locked = true,
            Purpose = new List<string> { _md.Purpose }, Targets = StationMission ? new List<string>() : new List<string> { _md.Target }, Start = Trial.Time,
            Doctrine = new Doctrine { Roe = _md.Roe, Emcon = _md.EmconPackage, RiskTolerance = _md.AbortAt },
            Rationale = "player mission",
        };
        var n = 0;
        if (pkg != null) op.Elements.Add(new OpElement { Id = $"e{++n}", Kind = ElementKind.Air, Role = "package", Spec = pkg, Emcon = _md.EmconPackage });
        if (sup != null) op.Elements.Add(new OpElement { Id = $"e{++n}", Kind = ElementKind.Air, Role = "support", Spec = sup, Emcon = _md.EmconSupport });
        foreach (var f in fires)
        {
            var el = new OpElement { Id = $"e{++n}", Kind = ElementKind.Fires, Role = "fires", Spec = f };
            if (ShipTarget) { el.Held = true; op.Rules.Add(new OpRule(new OpCondition("time-after", Value: ShipLaunchAt(f)), new OpAction("activate", el.Id))); }
            op.Elements.Add(el);
        }
        if (SpareSpec() is { } spare)
        {
            var el = new OpElement { Id = $"e{++n}", Kind = ElementKind.Air, Role = "spares", Spec = spare, Held = true, Emcon = _md.EmconPackage };
            op.Elements.Add(el);
            op.Rules.Add(new OpRule(new OpCondition("losses-above", Value: 0.001), new OpAction("activate", el.Id)));
        }
        if (_md.AbortOnLosses) op.Rules.Add(new OpRule(new OpCondition("losses-above", Value: _md.AbortAt), new OpAction("abort")));
        if (_md.HoldIfUnsure && !StationMission) op.Rules.Add(new OpRule(new OpCondition("target-confidence-below", Value: 0.5), new OpAction("hold-weapons")));
        if (_md.RecallIfTankerLost) op.Rules.Add(new OpRule(new OpCondition("tanker-unavailable"), new OpAction("abort")));
        if (_md.Roe == "hold") op.WeaponsHeld = true;
        var conflicts = Board.Add(op);
        _mResult = conflicts.Count > 0 ? $"Not committed: assets already in other operations ({string.Join(", ", conflicts)})." : $"Committed: {op.Name} is on the FRAG list (author YOU). It executes now.";
        Preview.Clear();
        RefreshMission();
        RefreshWindows(force: true);
    }
}
