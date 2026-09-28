using BadWaters.Sim.Economy;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// Trade & Logistics: sanctions (any nation can sanction any other for any good; the USA and China can make theirs
/// global, which every other seller must follow), trade wars, supply contracts, and where imports came from.
/// </summary>
public partial class TradeScreen : PanelContainer
{
    private VBoxContainer _left = null!, _mid = null!, _right = null!;
    private static string Me => Session.World.Player;
    private static EconomyState St => Session.World.Economy;
    private static EconomyCatalog Cat => Session.Content.Economy;
    private static bool Enforcer => Session.Content.Geo.Rules.GlobalEnforcers.Contains(Me);

    public override void _Ready()
    {
        AnchorRight = 1; AnchorBottom = 1;
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 12);
        AddChild(h);
        _left = Col(h, 470); _mid = Col(h, 420); _right = Col(h, 0);
        Refresh();
    }

    private static VBoxContainer Col(HBoxContainer h, int w)
    {
        var sc = new ScrollContainer { CustomMinimumSize = new Vector2(w, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = w == 0 ? SizeFlags.ExpandFill : SizeFlags.Fill };
        var v = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        v.AddThemeConstantOverride("separation", 4);
        sc.AddChild(v); h.AddChild(sc);
        return v;
    }
    private static void Clear(Node n) { foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); } }
    private static string Good(string g) => g == Sanction.All ? "everything" : g == Sanction.Strategic ? "strategic goods" : Cat.Goods.GetValueOrDefault(g)?.Name ?? g;
    private static string Who(string id) => Session.Content.Nations.GetValueOrDefault(id)?.Name ?? Session.Content.Geo.Suppliers.GetValueOrDefault(id)?.Name ?? (id == EconomySolver.WorldMarket ? "world market" : id);

    public void Refresh()
    {
        Clear(_left); Clear(_mid); Clear(_right);
        // ---- our sanctions and the form
        _left.AddChild(UiTheme.Text("SANCTIONS WE IMPOSE", 14, UiTheme.Accent));
        _left.AddChild(UiTheme.Text(Enforcer
            ? "As a global enforcer, a global sanction binds every other seller (except the other enforcer)."
            : "Our sanctions stop our own sales. Only the USA and China can make a sanction global.", 11, UiTheme.Dim, wrap: true));
        foreach (var x in St.Sanctions.Where(x => x.By == Me).ToList())
        {
            var row = new HBoxContainer();
            var l = UiTheme.Text($"{Who(x.Target)}: {Good(x.Good)}{(x.Global ? " · GLOBAL" : "")}", 12); l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var lift = new Button { Text = "Lift" }; lift.Pressed += () => { St.Lift(x.By, x.Target, x.Good); Refresh(); };
            row.AddChild(l); row.AddChild(lift); _left.AddChild(row);
        }
        var others = Session.Content.Nations.Keys.Where(n => n != Me && St.Nations.ContainsKey(n)).OrderBy(Who).ToList();
        var goods = new List<string> { Sanction.All, Sanction.Strategic };
        goods.AddRange(Cat.Goods.Values.Where(g => g.Storable).OrderBy(g => g.Name).Select(g => g.Id));
        var f1 = new HBoxContainer();
        var target = new OptionButton { ClipText = true, CustomMinimumSize = new Vector2(150, 0) };
        foreach (var n in others) target.AddItem(Who(n));
        var good = new OptionButton { ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var g in goods) good.AddItem(Good(g));
        f1.AddChild(target); f1.AddChild(good);
        _left.AddChild(f1);
        var f2 = new HBoxContainer();
        var global = new CheckBox { Text = "Global", Disabled = !Enforcer, TooltipText = "Only the USA and China can impose global sanctions." };
        var impose = new Button { Text = "Impose sanction" };
        impose.Pressed += () => { St.Sanction(Me, others[target.Selected], goods[good.Selected], global.ButtonPressed && Enforcer); Refresh(); };
        var war = new Button { Text = "Trade war", TooltipText = "Both sides sanction each other on this good (or everything)." };
        war.Pressed += () => { St.TradeWar(Me, others[target.Selected], new[] { goods[good.Selected] }); Refresh(); };
        f2.AddChild(global); f2.AddChild(impose); f2.AddChild(war);
        _left.AddChild(f2);

        _left.AddChild(UiTheme.Text("SANCTIONS ON US", 14, UiTheme.Accent));
        var onUs = St.Sanctions.Where(x => x.Target == Me).ToList();
        if (onUs.Count == 0) _left.AddChild(UiTheme.Text("None.", 12, UiTheme.Dim));
        foreach (var x in onUs) _left.AddChild(UiTheme.Text($"{Who(x.By)}: {Good(x.Good)}{(x.Global ? " · GLOBAL (only the other enforcer may still sell)" : "")}", 12, x.Global ? UiTheme.Bad : UiTheme.Ink, wrap: true));

        // ---- contracts and sources
        var ne = St.Nations[Me];
        _mid.AddChild(UiTheme.Text("SUPPLY CONTRACTS", 14, UiTheme.Accent));
        _mid.AddChild(UiTheme.Text("Procurement is automatic (world market, suppliers and any unsanctioned nation's spare stock). Contracts reserve a supply.", 11, UiTheme.Dim, wrap: true));
        foreach (var c in ne.Contracts.ToList())
        {
            var row = new HBoxContainer();
            var l = UiTheme.Text($"{Who(c.From)} → {Good(c.Good)}: {c.PerMonth:0.#}/month", 12); l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var rm = new Button { Text = "✕" }; rm.Pressed += () => { ne.Contracts.Remove(c); Refresh(); };
            row.AddChild(l); row.AddChild(rm); _mid.AddChild(row);
        }
        var cf = new HBoxContainer();
        var from = new OptionButton { ClipText = true, CustomMinimumSize = new Vector2(120, 0) };
        foreach (var n in others) from.AddItem(Who(n));
        var cg = new OptionButton { ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var cgoods = goods.Skip(2).ToList();
        foreach (var g in cgoods) cg.AddItem(Good(g));
        var qty = new SpinBox { MinValue = 1, MaxValue = 100000, Value = 50 };
        var add = new Button { Text = "Sign" };
        add.Pressed += () => { ne.Contracts.Add(new Contract { Id = $"c{ne.Contracts.Count + 1}", From = others[from.Selected], Good = cgoods[cg.Selected], PerMonth = qty.Value }); Refresh(); };
        cf.AddChild(from); cf.AddChild(cg); cf.AddChild(qty); cf.AddChild(add);
        _mid.AddChild(cf);
        _mid.AddChild(UiTheme.Text("WHERE IMPORTS CAME FROM", 14, UiTheme.Accent));
        var rep = Session.Econ.LastReports.GetValueOrDefault(Me);
        if (rep == null) _mid.AddChild(UiTheme.Text("Advance a month.", 12, UiTheme.Dim));
        else
        {
            foreach (var (k, q) in rep.Imported.Where(k => k.Key.Contains(':')).OrderByDescending(k => k.Value).Take(10))
                _mid.AddChild(UiTheme.Text($"{(k.StartsWith("contract:") ? "contract · " : "")}{Who(k[(k.IndexOf(':') + 1)..])}: {q:0}", 12));
            _mid.AddChild(UiTheme.Text($"Import bill {rep.ImportCost:0} a month", 12, UiTheme.Dim));
        }

        // ---- the world's sanctions
        _right.AddChild(UiTheme.Text("SANCTIONS IN FORCE WORLDWIDE", 14, UiTheme.Accent));
        foreach (var x in St.Sanctions.OrderBy(x => x.Target).ThenBy(x => x.By))
            _right.AddChild(UiTheme.Text($"{Who(x.By)} → {Who(x.Target)}: {Good(x.Good)}{(x.Global ? " · global" : "")}", 12, x.Global ? UiTheme.Bad : UiTheme.Ink));
    }
}
