using BadWaters.Sim.Engineering;
using Godot;

namespace BadWaters.Game;

/// <summary>
/// Component designer: pick an archetype (aircraft engine, radar array, launch cell block, generator...), set its
/// parameters, see the resulting component's weight, power, cooling, ratings, tech and cost live, and save it.
/// A saved component appears in the Systems list and as a drawn part in the Parts palette. Simple game rules.
/// </summary>
public partial class ComponentDesigner : HBoxContainer
{
    public event Action? Saved;
    private OptionButton _arch = null!;
    private LineEdit _name = null!;
    private VBoxContainer _params = null!, _stats = null!, _mine = null!;
    private PartTile _preview = null!;
    private Label _status = null!;
    private readonly List<string> _archIds = new();
    private readonly Dictionary<string, double> _values = new();
    private ComponentArchetype A => Session.Content.Archetypes[_archIds[Math.Max(0, _arch.Selected)]];

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 14);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
        AddChild(left);
        left.AddChild(UiTheme.Text("COMPONENT DESIGNER", 16, UiTheme.Accent));
        left.AddChild(UiTheme.Text("Design an engine, radar, launcher block, generator... Stats follow simple game rules.", 12, UiTheme.Dim, wrap: true));
        _arch = new OptionButton();
        foreach (var a in Session.Content.Archetypes.Values) { _archIds.Add(a.Id); _arch.AddItem($"{a.Name} ({string.Join("/", a.FitsKinds)})"); }
        _arch.ItemSelected += _ => { _values.Clear(); BuildParams(); };
        left.AddChild(_arch);
        _name = new LineEdit { PlaceholderText = "Component name" };
        left.AddChild(_name);
        _preview = new PartTile { CustomMinimumSize = new Vector2(0, 220) };
        left.AddChild(_preview);
        var save = new Button { Text = "Save component" };
        save.Pressed += Save;
        left.AddChild(save);
        _status = UiTheme.Text("", 12, UiTheme.Dim, wrap: true);
        left.AddChild(_status);
        left.AddChild(UiTheme.Text("YOUR COMPONENTS", 13, UiTheme.Accent));
        _mine = new VBoxContainer();
        left.AddChild(_mine);

        var ps = new ScrollContainer { CustomMinimumSize = new Vector2(460, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _params = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        ps.AddChild(_params);
        AddChild(ps);

        var ss = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _stats = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        ss.AddChild(_stats);
        AddChild(ss);
        BuildParams();
    }

    private void BuildParams()
    {
        foreach (var c in _params.GetChildren()) { _params.RemoveChild(c); c.QueueFree(); }
        _params.AddChild(UiTheme.Text("PARAMETERS", 13, UiTheme.Accent));
        if (_name.Text == "" || _name.Text.StartsWith("New ")) _name.Text = $"New {A.Name.ToLowerInvariant()}";
        foreach (var p in A.Params)
        {
            var row = new VBoxContainer();
            if (p.Kind == "choice")
            {
                var h = new HBoxContainer();
                var l = UiTheme.Text(p.Name, 13); l.CustomMinimumSize = new Vector2(170, 0);
                var o = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                foreach (var ch in p.Choices) o.AddItem(ch.Name + (ch.Adds.Count > 0 ? "  (" + string.Join(", ", ch.Adds.Select(a => $"{(a.Value >= 0 ? "+" : "")}{a.Value:0.#} {a.Key}")) + ")" : ""));
                var idx = (int)_values.GetValueOrDefault(p.Id, Math.Max(0, p.Choices.FindIndex(c => c.Id == p.DefaultChoice)));
                o.Select(idx); _values[p.Id] = idx;
                var pp = p;
                o.ItemSelected += i => { _values[pp.Id] = i; Refresh(); };
                h.AddChild(l); h.AddChild(o);
                row.AddChild(h);
            }
            else
            {
                var h = new HBoxContainer();
                var l = UiTheme.Text(p.Name, 13); l.CustomMinimumSize = new Vector2(170, 0);
                var v = _values.GetValueOrDefault(p.Id, p.Default); _values[p.Id] = v;
                var val = UiTheme.Text($"{v:0.##}", 13, UiTheme.Accent); val.CustomMinimumSize = new Vector2(50, 0);
                var sl = new HSlider { MinValue = p.Min, MaxValue = p.Max, Step = p.Step, Value = v, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
                var pp = p;
                sl.ValueChanged += x => { _values[pp.Id] = x; val.Text = $"{x:0.##}"; Refresh(); };
                h.AddChild(l); h.AddChild(sl); h.AddChild(val);
                row.AddChild(h);
                var eff = string.Join(", ", p.Adds.Select(a => $"{(a.Value >= 0 ? "+" : "")}{a.Value:0.##} {a.Key}").Concat(p.ScaleUses.Select(u => $"{u.Key} ∝ size^{u.Value:0.#}")).Concat(p.Uses.Select(u => $"{(u.Value >= 0 ? "+" : "")}{u.Value:0.##} {u.Key}")));
                if (eff != "") row.AddChild(UiTheme.Text($"   per step above {p.Default:0.##}: {eff}", 11, UiTheme.Dim, wrap: true));
            }
            _params.AddChild(row);
        }
        Refresh();
        RefreshMine();
    }

    private ComponentTemplate Current(string id) => ComponentBuilder.Build(A, id, _name.Text.Trim() == "" ? A.Name : _name.Text.Trim(), _values);

    /// <summary>How big to draw it: the first size-like parameter relative to its default.</summary>
    private double SizeFactor()
    {
        var p = A.Params.FirstOrDefault(x => x.Kind == "slider" && x.Id is "size" or "aperture" or "output" or "power" or "capacity" or "cells");
        if (p == null || p.Default == 0) return 1;
        var f = _values.GetValueOrDefault(p.Id, p.Default) / p.Default;
        return p.Id == "cells" ? Math.Sqrt(f) : f;
    }

    public static VisualPart VisualFor(ComponentArchetype a, ComponentTemplate c, double factor)
    {
        var src = Session.Content.Catalog.Parts.GetValueOrDefault(a.Visual);
        var shapes = (src?.Shapes ?? new List<ShapePrim>()).Select(s => s with
        {
            X = s.X * factor, Y = s.Y * factor, W = s.W * factor, H = s.H * factor, R = s.R * factor, P = s.P.Select(v => v * factor).ToArray(),
        }).ToList();
        return new VisualPart { Id = "part-" + c.Id, Name = c.Name, Category = a.Category, FitsKinds = a.FitsKinds, Shapes = shapes, Component = c.Id, Topside = a.Topside, Arc = src?.Arc ?? 0 };
    }

    private void Refresh()
    {
        var c = Current("preview");
        _preview.Part = VisualFor(A, c, SizeFactor());
        _preview.QueueRedraw();
        foreach (var x in _stats.GetChildren()) { _stats.RemoveChild(x); x.QueueFree(); }
        _stats.AddChild(UiTheme.Text("RESULT", 13, UiTheme.Accent));
        _stats.AddChild(UiTheme.Text($"{c.Name} · family {c.Family} · fits {string.Join(", ", c.FitsKinds)}", 13));
        _stats.AddChild(UiTheme.Text("Uses (negative = provides)", 12, UiTheme.Dim));
        foreach (var (k, v) in c.Uses) _stats.AddChild(UiTheme.Text($"  {k,-8} {v:0.###}", 13, v < 0 ? UiTheme.Good : UiTheme.Ink));
        _stats.AddChild(UiTheme.Text("Ratings", 12, UiTheme.Dim));
        foreach (var (k, v) in c.Adds.OrderByDescending(a => a.Value)) _stats.AddChild(UiTheme.Text($"  {k,-14} {(v >= 0 ? "+" : "")}{v:0.##}", 13, v < 0 ? UiTheme.Bad : UiTheme.Ink));
        _stats.AddChild(UiTheme.Text("Technology needed", 12, UiTheme.Dim));
        foreach (var (k, v) in c.Requires) _stats.AddChild(UiTheme.Text($"  {k} level {v}", 13));
        _stats.AddChild(UiTheme.Text($"Cost {c.Cost:0.##} · factory {c.Factory ?? "—"}{(c.Port != null ? $" · mount {c.Port}" : "")}", 13, UiTheme.Accent));
    }

    private void Save()
    {
        var n = 1;
        while (Session.Content.Catalog.Components.ContainsKey($"player-comp-{n}")) n++;
        var c = Current($"player-comp-{n}");
        Session.SaveComponent(c, VisualFor(A, c, SizeFactor()));
        _status.Text = $"Saved {c.Name}. It's in the Systems list and the Parts palette ({A.Category}).";
        RefreshMine();
        Saved?.Invoke();
    }

    private void RefreshMine()
    {
        foreach (var x in _mine.GetChildren()) { _mine.RemoveChild(x); x.QueueFree(); }
        foreach (var c in Session.Content.Catalog.Components.Values.Where(c => c.Id.StartsWith("player-comp-")))
            _mine.AddChild(UiTheme.Text($"• {c.Name} ({c.Family}) · {string.Join(", ", c.Adds.Where(a => a.Value > 0).Select(a => $"+{a.Value:0.#} {a.Key}"))}", 12, UiTheme.Ink, wrap: true));
        if (_mine.GetChildCount() == 0) _mine.AddChild(UiTheme.Text("None yet.", 12, UiTheme.Dim));
    }
}
