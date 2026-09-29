using System.Text.Json;
using BadWaters.Sim.Policy;
using BadWaters.Sim.Theatre;
using Godot;

namespace BadWaters.Game;

/// <summary>An operation template (data/core/theatre/op_templates.json): purpose, target kind, forces by class, support, fires.</summary>
public sealed record OpTemplateRow
{
    public string Class { get; init; } = "fighter";
    public int Count { get; init; } = 2;
    public string Role { get; init; } = "CAP";
    public string Formation { get; init; } = "Wedge";
}
public sealed record OpTemplateFires { public int Ground { get; init; } public int Naval { get; init; } }
public sealed record OpTemplate
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Group { get; init; } = "";
    public string Purpose { get; init; } = "strike";
    public string Target { get; init; } = "airbase";
    public string Aim { get; init; } = "";
    public double Tot { get; init; } = 90;
    public string Loadout { get; init; } = "";
    public List<OpTemplateRow> Air { get; init; } = new();
    public List<OpTemplateRow> Support { get; init; } = new();
    public List<OpTemplateRow> Spares { get; init; } = new();
    public OpTemplateFires Fires { get; init; } = new();
    public string Emcon { get; init; } = "limited";
    public string Roe { get; init; } = "tight";
}
public sealed record OpTemplateFile { public List<OpTemplate> Templates { get; init; } = new(); public List<string> DayFrag { get; init; } = new(); }

/// <summary>
/// Theatre Ops · operation templates: a library of mission types (counter-air, SEAD/DEAD, strike, maritime, logistics
/// interdiction, ISR, EW, support, major operations). Applying one fills the Mission wizard with forces picked from the
/// idle pool by class and a target chosen from your own picture; "Generate day's FRAG" commits a whole coordinated set.
/// </summary>
public partial class TheatreOps
{
    private static OpTemplateFile? _tpl;
    public static OpTemplateFile Templates => _tpl ??= LoadTemplates();
    private static OpTemplateFile LoadTemplates()
    {
        var f = Path.Combine(Session.DataDir(), "theatre", "op_templates.json");
        return File.Exists(f) ? JsonSerializer.Deserialize<OpTemplateFile>(File.ReadAllText(f), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new() : new();
    }

    private static readonly Dictionary<string, string[]> ClassCodes = new()
    {
        ["fighter"] = new[] { "FTR", "5GEN", "STK" }, ["strike"] = new[] { "STK", "5GEN", "FTR" }, ["stealth"] = new[] { "5GEN", "STK" },
        ["bomber"] = new[] { "BMR", "SB" }, ["aew"] = new[] { "AEW" }, ["tanker"] = new[] { "TKR" }, ["jamming"] = new[] { "EW" }, ["patrol"] = new[] { "MPA" },
    };
    private static readonly Dictionary<string, string> ClassRating = new()
    {
        ["fighter"] = "air-defence", ["strike"] = "strike", ["stealth"] = "signature", ["bomber"] = "payload", ["aew"] = "detection", ["tanker"] = "reach", ["jamming"] = "jamming", ["patrol"] = "detection",
    };

    /// <summary>Fill the draft from a template. variant picks the 1st, 2nd... candidate target so a FRAG spreads out.</summary>
    private bool ApplyOpTemplate(OpTemplate t, int variant = 0, double totOffsetMin = 0, double scale = 1)
    {
        _md = new MissionDraft { Name = $"{t.Name} {Board.Ops.Count(o => o.Author == BadWaters.Sim.Command.Author.Player) + 1}", Purpose = t.Purpose == "fires" ? "strike" : t.Purpose, TotMin = t.Tot + totOffsetMin, EmconPackage = t.Emcon, EmconSupport = t.Emcon == "silent" ? "limited" : t.Emcon, Roe = t.Roe };
        if (!PickTarget(t, variant)) return false;
        if (Trial.Bases.TryGetValue(_md.Target, out var b) && t.Aim != "")
            _md.Aimpoint = b.Components.Values.FirstOrDefault(c => c.Def.Effect == t.Aim)?.Def.Id ?? "";
        var taken = new Dictionary<string, int>();
        // range only counts on a tanker the template asks for AND one is actually free
        var tanker = t.Support.Any(x => x.Class == "tanker") && PickPreset("tanker", 1, taken) != null;
        List<AirRow> Rows(List<OpTemplateRow> src)
        {
            var list = new List<AirRow>();
            foreach (var r in src)
            {
                var n = Math.Max(1, (int)Math.Round(r.Count * scale));
                var p = PickPreset(r.Class, n, taken, tanker);
                if (p == null) continue;
                taken[p] = taken.GetValueOrDefault(p) + n;
                var row = new AirRow { Preset = p, Count = n, Role = Enum.Parse<Mission>(r.Role), Formation = Enum.TryParse<Formation>(r.Formation, out var f) ? f : Formation.Wedge };
                if (t.Loadout != "" && row.Role is Mission.Strike && Cat?.Template(p)?.Loadouts.ContainsKey(t.Loadout) == true) row.Loadout = Cat.Default(p, t.Loadout);
                list.Add(row);
            }
            return list;
        }
        _md.Air = Rows(t.Air); _md.Support = Rows(t.Support); _md.Spares = Rows(t.Spares);
        var aim = Aim();
        foreach (var (count, ship) in new[] { (t.Fires.Ground, false), (t.Fires.Naval, true) })
            foreach (var l in Trial.Launchers.Where(l => l.Def.Side == CommandSide && l.IsShip == ship && l.Stock > 0 && Pacing.DistanceKm(l.Def.Lon, l.Def.Lat, aim.Lon, aim.Lat) <= l.ReachKm
                                                          && (!ShipTarget || NavalRules.CanHitShips(l.Missile)) && Board.Authority.IsFree(l.Def.Id)).Take(count))
                _md.Fires.Add(new FireRow { Launcher = l.Def.Id, Rounds = Math.Min(2, l.Stock) });
        return _md.Air.Count + _md.Support.Count + _md.Fires.Count > 0;
    }

    /// <summary>Can count idle aircraft of this type reach the objective from one base (tanker adds range)?</summary>
    private bool Reaches(string preset, int count, bool tanker)
    {
        var info = Session.Content.PresetInfo(preset);
        if (info == null) return false;
        var aim = Aim();
        var radius = AirPacing.RadiusKm(info.R("reach")) + (tanker ? AirPacing.TankerBonusKm : 0);
        return Trial.Air!.Idle(CommandSide).Where(u => u.Preset == preset).GroupBy(u => u.Base).Where(g => g.Count() >= count)
            .Select(g => Trial.Site(g.Key)).Any(b => b != null && Pacing.DistanceKm(b.Lon, b.Lat, aim.Lon, aim.Lat) <= radius * 0.95);
    }

    private string? PickPreset(string cls, int count, Dictionary<string, int> taken, bool tanker = false)
    {
        var support = cls is "tanker" or "aew" or "jamming";
        var codes = ClassCodes.GetValueOrDefault(cls, new[] { "FTR" });
        var rating = ClassRating.GetValueOrDefault(cls, "air-defence");
        return Trial.Air!.IdleByType(CommandSide)
            .Select(x => (x.Preset, Left: x.Count - taken.GetValueOrDefault(x.Preset), P: Session.Content.PresetInfo(x.Preset), Code: Symbols.For(x.Preset, "air")))
            .Where(x => x.P != null && x.Left >= count && codes.Contains(x.Code) && (support || Reaches(x.Preset, count, tanker)))
            .OrderBy(x => Array.IndexOf(codes, x.Code)).ThenByDescending(x => x.P!.R(rating)).ThenByDescending(x => x.Left)
            .Select(x => x.Preset).FirstOrDefault();
    }

    private bool PickTarget(OpTemplate t, int variant)
    {
        var enemy = CommandSide == "blue" ? "red" : "blue";
        var own = Scenario.Sites.Where(s => s.Side == CommandSide && s.Type is "airbase" or "navalbase" or "garrison").ToList();
        var foe = Scenario.Sites.Where(s => s.Side == enemy && s.Type is "airbase" or "navalbase" or "garrison").ToList();
        if (own.Count == 0) return false;
        (double, double) Centre(IEnumerable<SiteDef> ss) => (ss.Average(s => s.Lon), ss.Average(s => s.Lat));
        var home = Centre(own);
        double FromHome(double lon, double lat) => own.Min(o => Pacing.DistanceKm(o.Lon, o.Lat, lon, lat)); // closest to any of our bases
        string? Nth(IEnumerable<string> ids) { var l = ids.ToList(); return l.Count == 0 ? null : l[variant % l.Count]; }
        string? site = t.Target switch
        {
            "airbase" or "navalbase" or "garrison" or "depot" => Nth(Scenario.Sites.Where(s => s.Side == enemy && s.Type == t.Target && TargetPolicy.IsLegitimate(s.Class)).OrderBy(s => FromHome(s.Lon, s.Lat)).Select(s => s.Id)),
            "sam" => Nth(Trial.Batteries.Where(b => b.Def.Side == enemy && !b.IsShip && b.BaseId != "").Select(b => Trial.Site(b.BaseId)!).Distinct().OrderBy(s => FromHome(s.Lon, s.Lat)).Select(s => s.Id)),
            "corridor" => Nth(Trial.Logistics?.Corridors.Values.Where(c => c.Def.Side == enemy && c.Def.Mode is "road" or "rail").OrderBy(c => FromHome(Trial.Logistics.Nodes[c.Def.B].Lon, Trial.Logistics.Nodes[c.Def.B].Lat)).Select(c => "corridor:" + c.Def.Id) ?? Enumerable.Empty<string>()),
            "ship" => Nth(Trial.Intel?.Pictures[CommandSide].Contacts.Values.Where(c => c.Kind == "ship" && Trial.Time - c.LastSeen < IntelRules.StaleAfter && !(Trial.Naval?.IsSunk(c.Key) ?? true)).OrderBy(c => FromHome(c.Lon, c.Lat)).Select(c => "ship:" + c.Key) ?? Enumerable.Empty<string>()),
            _ => null,
        };
        if (site != null) { _md.Target = site; _md.Point = null; return true; }
        if (t.Target.StartsWith("station"))
        {
            var near = own.OrderBy(o => foe.Count == 0 ? 0 : foe.Min(f => Pacing.DistanceKm(o.Lon, o.Lat, f.Lon, f.Lat))).Skip(variant % own.Count).First();
            var f0 = foe.OrderBy(f => Pacing.DistanceKm(near.Lon, near.Lat, f.Lon, f.Lat)).FirstOrDefault();
            (double Lon, double Lat) p = t.Target switch
            {
                "station-own" => (near.Lon, near.Lat),
                "station-sea" when Scenario.Neutral.Count > 0 => (Scenario.Neutral[variant % Scenario.Neutral.Count][1][0], Scenario.Neutral[variant % Scenario.Neutral.Count][1][1]),
                _ when f0 != null => (near.Lon + (f0.Lon - near.Lon) * 0.4, near.Lat + (f0.Lat - near.Lat) * 0.4),
                _ => (near.Lon, near.Lat),
            };
            _md.Point = p; _md.Target = "@pt";
            return true;
        }
        return false;
    }

    /// <summary>Template picker for the Objective page.</summary>
    private void TemplatePicker(VBoxContainer v)
    {
        var h = new HBoxContainer();
        h.AddChild(L("Operation template", UiTheme.TacDim));
        var o = new OptionButton { ClipText = true, CustomMinimumSize = new Vector2(420, 0) };
        o.AddThemeFontSizeOverride("font_size", 11);
        o.AddItem("— pick a template (fills forces, target, timing) —");
        var list = Templates.Templates.OrderBy(t => t.Group).ToList();
        foreach (var t in list) o.AddItem($"{t.Group} · {t.Name}");
        o.ItemSelected += i =>
        {
            if (i <= 0) return;
            if (!ApplyOpTemplate(list[(int)i - 1])) _mResult = $"No suitable target or forces for {list[(int)i - 1].Name} right now.";
            _mPage = 1;
            RefreshMission();
        };
        h.AddChild(o);
        v.AddChild(h);
    }

    /// <summary>Build and commit a whole day's FRAG from the template set; returns what happened.</summary>
    public string GenerateDayFrag()
    {
        var done = new List<string>(); var skipped = new List<string>();
        var i = 0;
        foreach (var id in Templates.DayFrag)
        {
            var t = Templates.Templates.FirstOrDefault(x => x.Id == id);
            if (t == null) continue;
            var ok = false; var why = "no suitable target or idle forces";
            foreach (var (variant, scale) in new[] { (0, 1.0), (1, 1.0), (2, 1.0), (0, 0.5), (1, 0.5), (2, 0.5) })
            {
                if (ok) break;
                if (!ApplyOpTemplate(t, variant, i * 10, scale)) continue;
                var chk = Validate(preview: false);
                if (chk.Issues.Count > 0) { why = chk.Issues[0]; continue; }
                CommitMission();
                ok = _mResult.StartsWith("Committed");
                if (ok) Board.Tick(Trial.Time); // launch now so the next template sees these aircraft as tasked
            }
            if (ok) done.Add(t.Name); else skipped.Add($"{t.Name} ({why})");
            i++;
        }
        var msg = $"Day's FRAG: {done.Count} operations committed ({string.Join(", ", done)})" + (skipped.Count > 0 ? $"; not possible now: {string.Join("; ", skipped)}" : "") + ".";
        Trial.Report("ato", msg);
        _md = new MissionDraft();
        return msg;
    }
}
