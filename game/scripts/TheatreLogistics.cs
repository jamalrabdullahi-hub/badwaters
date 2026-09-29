using BadWaters.Sim.Theatre;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// Theatre Ops · LOGISTICS window: Overview (theatre totals, days of supply, worst-off points, active routes and the
/// transport pool), Nodes (every depot, base, battery, launcher and ship), Routes (every corridor) and Orders (manual
/// shipments, the AI officer switch). Routes are drawn on the map; vehicles are only counted.
/// </summary>
public partial class TheatreOps
{
    private string _lgTab = "OVERVIEW";
    private string _lgKind = "all";
    private string _oItem = "avfuel", _oFrom = "", _oTo = "", _oMode = "auto";
    private double _oQty = 100;
    private string _oResult = "";
    private LogisticsService? Lg => Trial.Logistics;
    private static readonly string[] Classes = { "avfuel", "diesel", "propellant", "spares" };

    private void FillLogistics(TacWindow w)
    {
        if (Lg == null) { w.Line("This theatre has no logistics model.", UiTheme.TacDim); return; }
        var tabs = new HBoxContainer();
        foreach (var t in new[] { "OVERVIEW", "NODES", "ROUTES", "ORDERS" })
        {
            var b = new Button { Text = t, ToggleMode = true, ButtonPressed = _lgTab == t };
            b.AddThemeFontSizeOverride("font_size", 11);
            b.Pressed += () => { _lgTab = t; w.Refresh?.Invoke(); };
            tabs.AddChild(b);
        }
        var ai = new CheckBox { Text = "AI logistics officer", ButtonPressed = Lg.Auto.GetValueOrDefault(CommandSide), TooltipText = "On: the AI keeps every supply point topped up (reserve depots first, never raiding a base below its keep level). Off: only your orders move supplies." };
        ai.Toggled += on => Lg.Auto[CommandSide] = on;
        tabs.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        tabs.AddChild(ai);
        w.Body.AddChild(tabs);
        switch (_lgTab)
        {
            case "NODES": LgNodes(w); break;
            case "ROUTES": LgRoutes(w); break;
            case "ORDERS": LgOrders(w); break;
            default: LgOverview(w); break;
        }
    }

    private IEnumerable<SupplyNode> MyNodes => Lg!.Nodes.Values.Where(n => n.Side == CommandSide);

    private string DaysText(double d) => double.IsInfinity(d) ? "—" : d >= 99 ? "99+ d" : $"{d:0.0} d";

    private void LgOverview(TacWindow w)
    {
        var lg = Lg!;
        w.Head("Theatre stocks");
        foreach (var item in Classes)
        {
            var total = MyNodes.Sum(n => lg.Get(n, item));
            var use = MyNodes.Sum(n => lg.DailyUse(n, item));
            var shortN = MyNodes.Count(n => n.Short.Contains(item));
            w.Line($"{lg.ItemName(item),-18} {lg.Amount(item, total),16} · uses {lg.Amount(item, use)}/day · {DaysText(use <= 0 ? double.PositiveInfinity : total / use)}{(shortN > 0 ? $" · {shortN} point(s) DRY" : "")}",
                shortN > 0 ? UiTheme.Bad : UiTheme.TacInk);
        }
        w.Head("Munitions in theatre (top)");
        var mun = MyNodes.SelectMany(n => lg.ItemsAt(n).Where(i => i.StartsWith("store:") || i.StartsWith("design:")).Select(i => (i, q: lg.Get(n, i))))
            .GroupBy(x => x.i).Select(g => (g.Key, Q: g.Sum(x => x.q))).OrderByDescending(x => x.Q).Take(10);
        foreach (var (i, q) in mun) w.Line($"  {lg.ItemName(i),-28} {q:0}");
        w.Head("Worst off");
        var worst = MyNodes.SelectMany(n => lg.ItemsAt(n).Where(i => lg.BaselineOf(n, i) > 0).Select(i => (n, i, share: lg.Get(n, i) / lg.BaselineOf(n, i))))
            .Where(x => x.share < 0.5 || x.n.Short.Contains(x.i)).OrderBy(x => x.share).Take(8).ToList();
        if (worst.Count == 0) w.Line("  every supply point above half its load", UiTheme.Good);
        foreach (var (n, i, share) in worst) w.Line($"  {n.Name}: {lg.ItemName(i)} {share:P0} of load{(n.Short.Contains(i) ? " · DRY" : "")}", n.Short.Contains(i) ? UiTheme.Bad : new Color("ff9a3c"));
        w.Head("Active routes");
        var routes = lg.ActiveRoutes().Where(r => r.C.Def.Side == CommandSide).OrderByDescending(r => r.Tons).Take(10).ToList();
        if (routes.Count == 0) w.Line("  nothing moving", UiTheme.TacDim);
        foreach (var (c, mode, veh, tons) in routes)
        {
            var m = lg.Def.Modes[mode];
            w.Line($"  {c.Def.Name} · {veh} {m.Vehicle} · {m.Cap:0,0} t avg capacity · {tons:0,0} t moving{(c.Damage > 0 ? $" · route {c.Damage:P0} damaged" : "")}");
        }
        w.Head("Transport pool");
        w.Line("  " + string.Join(" · ", lg.Def.Modes.Select(kv => $"{kv.Value.Vehicle}: {lg.Pool.GetValueOrDefault((CommandSide, kv.Key)) - lg.VehiclesOut(CommandSide, kv.Key)}/{lg.Pool.GetValueOrDefault((CommandSide, kv.Key))} free")));
    }

    private void LgNodes(TacWindow w)
    {
        var lg = Lg!;
        var h = new HBoxContainer();
        foreach (var k in new[] { "all", "depot", "airbase", "navalbase", "garrison", "battery", "launcher", "ship" })
        {
            var b = new Button { Text = k, ToggleMode = true, ButtonPressed = _lgKind == k };
            b.AddThemeFontSizeOverride("font_size", 10);
            b.Pressed += () => { _lgKind = k; w.Refresh?.Invoke(); };
            h.AddChild(b);
        }
        w.Body.AddChild(h);
        foreach (var n in MyNodes.Where(n => _lgKind == "all" || n.Kind == _lgKind).OrderBy(n => n.Kind != "depot").ThenBy(n => n.Name))
        {
            var pen = n.Factor < 0.999 ? $" · delivery efficiency {n.Factor:P0} ({n.Elev:0} m, {n.OffRouteKm:0} km off route)" : " · on the route network";
            w.Line($"{n.Name} [{n.Kind}]{pen}", n.Short.Count > 0 ? UiTheme.Bad : UiTheme.TacAccent);
            var parts = lg.ItemsAt(n).Where(i => lg.Get(n, i) > 0 || lg.BaselineOf(n, i) > 0).OrderBy(i => Array.IndexOf(Classes, i) is var x && x < 0 ? 9 : x)
                .Select(i => $"{lg.ItemName(i)} {lg.Amount(i, lg.Get(n, i))}/{lg.Amount(i, lg.BaselineOf(n, i))}{(lg.DailyUse(n, i) > 0 ? $" ({DaysText(lg.DaysLeft(n, i))})" : "")}{(n.Short.Contains(i) ? " DRY" : "")}");
            w.Line("   " + string.Join(" · ", parts), UiTheme.TacInk);
            var needs = n.Kind switch
            {
                "battery" => $"   needs {lg.Def.Use.BatterySparesDay * 7:0} spare parts and {lg.Amount("diesel", lg.Def.Use.GroundDieselDay * 7)} diesel a week",
                "launcher" => $"   needs {lg.Def.Use.LauncherSparesDay * 7:0} spare parts a week{(Trial.Launchers.FirstOrDefault(l => l.Def.Id == n.Id) is { } l && lg.IsLiquid(l.Def.Missile) ? $"; {lg.Amount("propellant", lg.Def.Use.PropellantPerRound)} liquid propellant per round" : "")}",
                "ship" => $"   burns {lg.Amount("diesel", lg.Def.Use.ShipDieselDay)} diesel a day under way",
                _ => "",
            };
            if (needs != "") w.Line(needs, UiTheme.TacDim);
        }
    }

    private void LgRoutes(TacWindow w)
    {
        var lg = Lg!;
        var active = lg.ActiveRoutes().ToList();
        w.Line($"{"ROUTE",-34}{"MODE",-9}{"KM",6}  {"DAMAGE",7}  ACTIVE", UiTheme.TacDim);
        foreach (var c in lg.Corridors.Values.Where(c => c.Def.Side == CommandSide).OrderBy(c => c.Def.Mode).ThenBy(c => c.Def.Name))
        {
            var a = active.Where(x => x.C == c).ToList();
            var act = a.Count == 0 ? "—" : string.Join(", ", a.Select(x => $"{x.Vehicles} {lg.Def.Modes[x.Mode].Vehicle}"));
            var b = new Button { Text = $"{Trunc(c.Def.Name, 33),-34}{c.Def.Mode,-9}{c.Def.Km,6:0}  {(c.Damage > 0 ? $"{c.Damage:P0}" : "—"),7}  {act}", Flat = true, Alignment = HorizontalAlignment.Left, TooltipText = $"{lg.Nodes[c.Def.A].Name} ↔ {lg.Nodes[c.Def.B].Name}" };
            b.AddThemeFontSizeOverride("font_size", 11);
            b.AddThemeColorOverride("font_color", c.Damage > 0 ? new Color("ff9a3c") : a.Count > 0 ? UiTheme.TacAccent : UiTheme.TacInk);
            var cc = c;
            b.Pressed += () => GetViewport().GetCamera2D()?.SetPosition(Session.Project((lg.Nodes[cc.Def.A].Lon + lg.Nodes[cc.Def.B].Lon) / 2, (lg.Nodes[cc.Def.A].Lat + lg.Nodes[cc.Def.B].Lat) / 2));
            w.Body.AddChild(b);
        }
    }

    private void LgOrders(TacWindow w)
    {
        var lg = Lg!;
        var nodes = MyNodes.OrderBy(n => n.Kind != "depot").ThenBy(n => n.Name).ToList();
        if (_oFrom == "" || !lg.Nodes.ContainsKey(_oFrom)) _oFrom = nodes.FirstOrDefault(n => n.Reserve)?.Id ?? nodes[0].Id;
        if (_oTo == "" || !lg.Nodes.ContainsKey(_oTo)) _oTo = nodes.First(n => n.Id != _oFrom).Id;
        OptionButton Pick(IEnumerable<(string Id, string Text)> items, string cur, Action<string> set)
        {
            var o = new OptionButton { ClipText = true, CustomMinimumSize = new Vector2(260, 0) };
            o.AddThemeFontSizeOverride("font_size", 11);
            var ids = new List<string>();
            foreach (var (id, t) in items) { ids.Add(id); o.AddItem(t); }
            o.Select(Math.Max(0, ids.IndexOf(cur)));
            o.ItemSelected += i => { set(ids[(int)i]); w.Refresh?.Invoke(); };
            return o;
        }
        w.Head("Manual shipment");
        var from = lg.Nodes[_oFrom];
        var r1 = new HBoxContainer(); r1.AddChild(UiTheme.Text("From", 12, UiTheme.Dim));
        r1.AddChild(Pick(nodes.Select(n => (n.Id, $"{n.Name} [{n.Kind}]")), _oFrom, v => _oFrom = v));
        r1.AddChild(UiTheme.Text("To", 12, UiTheme.Dim));
        r1.AddChild(Pick(nodes.Where(n => n.Id != _oFrom).Select(n => (n.Id, $"{n.Name} [{n.Kind}]")), _oTo, v => _oTo = v));
        w.Body.AddChild(r1);
        var items = lg.ItemsAt(from).Where(i => lg.Get(from, i) > 0).ToList();
        if (!items.Contains(_oItem)) _oItem = items.FirstOrDefault() ?? "avfuel";
        var r2 = new HBoxContainer();
        r2.AddChild(Pick(items.Select(i => (i, $"{lg.ItemName(i)} · {lg.Amount(i, lg.Get(from, i))} at source")), _oItem, v => _oItem = v));
        var q = new SpinBox { MinValue = 1, MaxValue = Math.Max(1, lg.Get(from, _oItem)), Value = Math.Min(_oQty, Math.Max(1, lg.Get(from, _oItem))), CustomMinimumSize = new Vector2(110, 0) };
        q.ValueChanged += v => _oQty = v;
        r2.AddChild(q);
        r2.AddChild(Pick(new[] { "auto", "road", "rail", "sea", "air", "heli" }.Select(m => (m, m == "auto" ? "mode: auto" : lg.Def.Modes[m].Name)), _oMode, v => _oMode = v));
        var go = new Button { Text = "SEND" };
        go.Pressed += () =>
        {
            var (ok, msg, _) = lg.Order(CommandSide, _oItem, _oQty, _oFrom, _oTo, _oMode == "auto" ? null : _oMode, player: true);
            _oResult = (ok ? "✓ " : "✕ ") + msg;
            w.Refresh?.Invoke();
        };
        r2.AddChild(go);
        w.Body.AddChild(r2);
        var to = lg.Nodes[_oTo];
        var path = lg.Route(CommandSide, _oFrom, _oTo, _oMode is "air" or "heli" ? "air" : null);
        w.Line(path == null ? "No route between these points." : $"Route: {string.Join(" → ", path.Select(c => c.Def.Name))} · destination efficiency {to.Factor:P0} ({to.Elev:0} m, {to.OffRouteKm:0} km off route)", path == null ? UiTheme.Bad : UiTheme.TacDim);
        if (_oResult != "") w.Line(_oResult, _oResult.StartsWith("✓") ? UiTheme.Good : UiTheme.Bad);
        w.Head("Shipments");
        var list = lg.Shipments.Where(s => s.Side == CommandSide).OrderBy(s => s.State).ThenBy(s => s.Arrive).Take(40).ToList();
        if (list.Count == 0) w.Line("  none", UiTheme.TacDim);
        foreach (var s in list)
        {
            var h = new HBoxContainer();
            var txt = $"{(s.Player ? "YOU" : "AI "),-4}{s.State,-10}{lg.Amount(s.Item, s.Qty)} {lg.ItemName(s.Item)} · {lg.Nodes[s.From].Name} → {lg.Nodes[s.To].Name} · {s.Vehicles} {lg.Def.Modes[s.Mode].Vehicle}" +
                      (s.State == ShipmentState.Moving ? $" · arrives in {(s.Arrive - Trial.Time) / 3600:0.0} h" : s.Note != "" ? $" · {s.Note}" : "");
            var l = UiTheme.Text(txt, 11, s.State == ShipmentState.Lost ? UiTheme.Bad : s.State == ShipmentState.Delivered ? UiTheme.Dim : s.Player ? UiTheme.Accent : UiTheme.Ink);
            l.SizeFlagsHorizontal = SizeFlags.ExpandFill; l.ClipText = true;
            h.AddChild(l);
            if (s.State == ShipmentState.Queued)
            {
                var c = new Button { Text = "cancel" }; c.AddThemeFontSizeOverride("font_size", 10);
                var ss = s; c.Pressed += () => { lg.Cancel(ss); w.Refresh?.Invoke(); };
                h.AddChild(c);
            }
            w.Body.AddChild(h);
        }
    }

    /// <summary>Supply routes on the map: active corridors thicker by tonnage, damaged ones dashed; dry points flagged.</summary>
    public void DrawLogistics(TheatreLayer layer, float zoom, float w)
    {
        if (Lg == null || !Show("logistics")) return;
        var active = Lg.ActiveRoutes().ToDictionary(r => (r.C.Def.Id, r.Mode), r => r);
        foreach (var c in Lg.Corridors.Values.Where(c => ViewSide == "all" || c.Def.Side == ViewSide))
        {
            var a = Lg.Nodes[c.Def.A]; var b = Lg.Nodes[c.Def.B];
            var pa = Session.Project(a.Lon, a.Lat); var pb = Session.Project(b.Lon, b.Lat);
            var col = c.Def.Mode switch { "rail" => new Color("c8c8d0"), "sea" => new Color("7fb7d8"), "air" => new Color("a8d8ff"), _ => new Color("d9b36a") };
            var on = active.Where(x => x.Key.Id == c.Def.Id).Select(x => x.Value).ToList();
            var tons = on.Sum(x => x.Tons);
            var width = w * (on.Count > 0 ? 1.4f + (float)Math.Min(3, tons / 400) : 0.8f);
            var alpha = on.Count > 0 ? 0.85f : c.Def.Mode == "air" ? 0.08f : 0.25f;
            if (c.Damage > 0) layer.DrawDashedLine(pa, pb, new Color("ff9a3c", 0.8f), width, 5 / zoom);
            else layer.DrawLine(pa, pb, new Color(col, alpha), width);
            if (on.Count > 0 && zoom > 2.2f && Show("names"))
                layer.Labels.Add(((pa + pb) / 2, $"{c.Def.Name} · {string.Join(", ", on.Select(x => $"{x.Vehicles} {Lg.Def.Modes[x.Mode].Vehicle}"))}", new Color(col, 0.95f)));
        }
        foreach (var n in Lg.Nodes.Values.Where(n => n.Short.Count > 0 && (ViewSide == "all" || n.Side == ViewSide)))
        {
            var p = Session.Project(n.Lon, n.Lat) + new Vector2(10, -10) / zoom;
            layer.DrawCircle(p, 5 / zoom, TheatreLayer.Red);
            layer.Labels.Add((p + new Vector2(0, -8 / zoom), "DRY: " + string.Join(", ", n.Short.Select(Lg.ItemName).Take(2)), TheatreLayer.Red));
        }
    }
}
