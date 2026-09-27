using BadWaters.Sim.Policy;
using BadWaters.Sim.Theatre;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// Theatre Ops · FIRES tab: surface-to-surface missile scheduling for ground launchers and ships.
/// Pick launcher, target, aimpoint and rounds; set a TOT (or sync it to an air package) and the
/// launch time is worked back from the missile's flight time. Also "fire now" and a fire-mission table.
/// </summary>
public partial class TheatreOps
{
    private SpinBox _fTot = null!;
    private OptionButton _fSync = null!;
    private readonly List<string> _fSyncIds = new();
    private VBoxContainer _fireTable = null!, _launcherBoard = null!;
    private Label _fireCalc = null!;

    private void BuildFires(VBoxContainer v)
    {
        v.AddChild(UiTheme.Text("SCHEDULE A FIRE MISSION", 13, UiTheme.Accent));
        _launcher = new OptionButton { ClipText = true };
        _launcher.ItemSelected += _ => { FillTargets(); FireCalc(); };
        v.AddChild(_launcher);
        var r1 = new HBoxContainer();
        _target = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
        _target.ItemSelected += _ => { FillAim(); FireCalc(); };
        _count = new SpinBox { MinValue = 1, MaxValue = 8, Value = 2, Suffix = "rds" };
        r1.AddChild(_target); r1.AddChild(_count);
        v.AddChild(r1);
        _aim = new OptionButton { ClipText = true };
        v.AddChild(_aim);
        var r2 = new HBoxContainer();
        r2.AddChild(UiTheme.Text("TOT T+", 12, UiTheme.Dim));
        _fTot = new SpinBox { MinValue = 1, MaxValue = 300, Value = 60, Suffix = "min" };
        _fTot.ValueChanged += _ => FireCalc();
        r2.AddChild(_fTot);
        r2.AddChild(UiTheme.Text("  sync to", 12, UiTheme.Dim));
        _fSync = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
        _fSync.ItemSelected += i =>
        {
            if (i <= 0) return;
            var p = Trial.Air?.Packages.FirstOrDefault(x => x.Id == _fSyncIds[(int)i]);
            if (p == null) return;
            _fTot.Value = p.Tot / 60;
            var ti = _targetIds.IndexOf(p.Target.Id);
            if (ti >= 0) { _target.Select(ti); FillAim(); }
            FireCalc();
        };
        r2.AddChild(_fSync);
        v.AddChild(r2);
        _fireCalc = UiTheme.Text("", 12, UiTheme.Dim, wrap: true);
        _fireCalc.CustomMinimumSize = new Vector2(ConsoleW - 40, 0);
        v.AddChild(_fireCalc);
        var r3 = new HBoxContainer();
        var sched = new Button { Text = "Schedule for TOT", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        sched.Pressed += () => ScheduleFire();
        var now = new Button { Text = "Fire now", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        now.Pressed += () =>
        {
            if (_launcher.Selected < 0 || _target.Selected < 0) return;
            var r = Trial.Launch(_launcherIds[_launcher.Selected], _targetIds[_target.Selected], (int)_count.Value, _aim.Selected > 0 ? _aimIds[_aim.Selected] : "");
            Result(r.Ok, r.Message);
            FillLaunchers();
        };
        r3.AddChild(sched); r3.AddChild(now);
        v.AddChild(r3);
        _fireResult = UiTheme.Text("", 12, UiTheme.Dim, wrap: true);
        _fireResult.CustomMinimumSize = new Vector2(ConsoleW - 40, 0);
        v.AddChild(_fireResult);
        v.AddChild(UiTheme.Text("FIRE MISSIONS", 13, UiTheme.Accent));
        _fireTable = new VBoxContainer();
        v.AddChild(_fireTable);
        v.AddChild(UiTheme.Text("LAUNCHERS", 13, UiTheme.Accent));
        _launcherBoard = new VBoxContainer();
        v.AddChild(_launcherBoard);
    }

    private void Result(bool ok, string text)
    {
        _fireResult.Text = text;
        _fireResult.AddThemeColorOverride("font_color", ok ? UiTheme.Good : UiTheme.Bad);
    }

    private AtoEntry? FireEntry() =>
        _launcher.Selected < 0 || _target.Selected < 0 ? null
        : new AtoEntry { Side = CommandSide, Launcher = _launcherIds[_launcher.Selected], Target = _targetIds[_target.Selected], Count = (int)_count.Value, Tot = _fTot.Value * 60, Aimpoint = _aim.Selected > 0 ? _aimIds[_aim.Selected] : "" };

    /// <summary>Live read-out: distance, flight time, launch time for the chosen TOT (or why it can't work).</summary>
    private void FireCalc()
    {
        if (Trial?.Air == null || FireEntry() is not { } e) { _fireCalc.Text = ""; return; }
        var r = Trial.Air.Plan(e);
        _fireCalc.Text = string.Join("\n", r.Issues);
        _fireCalc.AddThemeColorOverride("font_color", r.Ok ? UiTheme.Ink : new Color("ffb347"));
    }

    private void ScheduleFire()
    {
        if (Trial.Air == null || FireEntry() is not { } e) return;
        var r = Trial.Air.Commit(e);
        Result(r.Ok, r.Ok ? "Scheduled. " + string.Join(" ", r.Issues) : string.Join("\n", r.Issues));
        UpdateFires();
    }

    private void FillSync()
    {
        _fSync.Clear(); _fSyncIds.Clear();
        _fSync.AddItem("— package —"); _fSyncIds.Add("");
        foreach (var p in Trial.Air?.Packages.Where(p => p.Side == CommandSide) ?? Enumerable.Empty<Package>())
        {
            _fSyncIds.Add(p.Id);
            _fSync.AddItem($"{p.Name} · {p.Target.Name} · TOT {AirTasking.Clock(p.Tot)}");
        }
    }

    private void UpdateFires()
    {
        if (Trial.Air == null) return;
        if (_fSync.ItemCount != 1 + Trial.Air.Packages.Count(p => p.Side == CommandSide)) FillSync();
        var side = ViewSide == "all" ? CommandSide : ViewSide;
        Clear(_fireTable);
        var grid = new GridContainer { Columns = 8 };
        grid.AddThemeConstantOverride("h_separation", 10);
        foreach (var h in new[] { "Launcher", "Missile", "Rds", "Target · aim", "Launch", "TOT", "State", "" }) grid.AddChild(UiTheme.Text(h, 11, UiTheme.Dim));
        foreach (var f in Trial.Air.Fires.Where(f => f.Side == side).OrderBy(f => f.State != FireState.Planned).ThenBy(f => f.Tot).Take(24))
        {
            var col = f.State == FireState.Failed ? UiTheme.Bad : f.State == FireState.Launched ? UiTheme.Dim : UiTheme.Ink;
            var aim = f.Aimpoint != "" && Trial.Bases.TryGetValue(f.Target.Id, out var tb) ? tb.Components[f.Aimpoint].Def.Name : "doctrine";
            foreach (var v in new[] { (f.Launcher.IsShip ? "⚓ " : "▲ ") + f.Launcher.Def.Name, f.Launcher.Missile.Name, $"{f.Count}", $"{f.Target.Name} · {aim}", AirTasking.Clock(f.LaunchAt), AirTasking.Clock(f.Tot), f.State.ToString() })
            {
                var l = UiTheme.Text(v, 11, col);
                l.ClipText = true;
                l.CustomMinimumSize = new Vector2(v.Length > 20 ? 150 : 0, 0);
                l.TooltipText = f.Note;
                grid.AddChild(l);
            }
            if (f.State == FireState.Planned && f.Side == CommandSide)
            {
                var c = new Button { Text = "Cancel" };
                c.AddThemeFontSizeOverride("font_size", 11);
                var ff = f;
                c.Pressed += () => { Trial.Air.Cancel(ff); UpdateFires(); };
                grid.AddChild(c);
            }
            else grid.AddChild(new Control());
        }
        _fireTable.AddChild(grid);

        Clear(_launcherBoard);
        foreach (var l in Trial.Launchers.Where(l => l.Def.Side == side))
        {
            var tasked = Trial.Air.Fires.Where(f => f.Launcher == l && f.State == FireState.Planned).Sum(f => f.Count);
            var pads = l.BaseId != "" && Trial.Bases.TryGetValue(l.BaseId, out var b) && !b.PadsOk;
            var t = UiTheme.Text($"{(l.IsShip ? "⚓" : "▲")} {l.Def.Name} · {l.Missile.Name} · {l.Stock} left · {tasked} tasked · reach {l.ReachKm:0} km{(pads ? " · PADS DOWN" : "")}", 12, pads ? UiTheme.Bad : UiTheme.Ink);
            t.ClipText = true;
            _launcherBoard.AddChild(t);
        }
    }

    private void UpdateContacts()
    {
        Clear(_contacts);
        var pic = Trial.Intel?.Pictures.GetValueOrDefault(ViewSide == "all" ? CommandSide : ViewSide);
        if (pic == null) return;
        foreach (var c in pic.Contacts.Values.OrderBy(c => Trial.Time - c.LastSeen).Take(30))
        {
            var age = Trial.Time - c.LastSeen;
            var l = UiTheme.Text($"{c.Kind,-8} {c.Label}{(c.IdLevel < 2 ? " ?" : "")} · {c.Source} · {(age < 60 ? "now" : $"{age / 60:0} min ago")}", 12, age > IntelRules.StaleAfter ? UiTheme.Dim : UiTheme.Ink);
            l.ClipText = true;
            _contacts.AddChild(l);
        }
        if (pic.Contacts.Count == 0) _contacts.AddChild(UiTheme.Text("Nothing held. Put up AEW, patrol aircraft or wait for a satellite pass.", 12, UiTheme.Dim));
    }
}
