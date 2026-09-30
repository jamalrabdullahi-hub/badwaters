using Godot;

namespace BadWaters.Game;

/// <summary>
/// Diplomacy: war and peace between nations. There are no fixed theatres: a war fights wherever the two nations'
/// forces can reach each other (a fight zone built from the persistent world), and peace stops the fighting but
/// keeps the damage.
/// </summary>
public partial class DiplomacyScreen : PanelContainer
{
    private VBoxContainer _v = null!;
    private static string Me => Session.World.Player;

    public override void _Ready()
    {
        AnchorRight = 1; AnchorBottom = 1;
        var sc = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(sc);
        _v = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _v.AddThemeConstantOverride("separation", 4);
        sc.AddChild(_v);
        Refresh();
    }

    public void Refresh()
    {
        if (_v == null) return;
        foreach (var c in _v.GetChildren()) { _v.RemoveChild(c); c.QueueFree(); }
        _v.AddChild(UiTheme.Text("WARS", 15, UiTheme.Accent));
        if (Session.World.Wars.Count == 0) _v.AddChild(UiTheme.Text("The world is at peace.", 13, UiTheme.Dim));
        foreach (var w in Session.World.Wars.ToList())
        {
            var h = new HBoxContainer();
            var zone = Session.Zone(w.Key);
            var fp = zone == null ? "" : $" · fight zone {zone.Sites.Count(s => s.Class != "population-centre")} bases, {zone.Units.Count} aircraft and ships";
            var l = UiTheme.Text($"{Session.NationName(w.A)} vs {Session.NationName(w.B)} · since {w.Started:d MMM yyyy}{fp}", 13, UiTheme.Bad);
            l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            h.AddChild(l);
            if (w.Involves(Me))
            {
                var p = new Button { Text = "Make peace", TooltipText = "The fighting stops; the damage and losses stay in the world." };
                p.Pressed += () => { Session.MakePeace(w.A, w.B); Refresh(); };
                h.AddChild(p);
            }
            _v.AddChild(h);
        }
        _v.AddChild(UiTheme.Text("NATIONS", 15, UiTheme.Accent));
        _v.AddChild(UiTheme.Text("A war fights wherever the two nations' forces can reach each other. Nations without forces in reach can be at war with nothing to fight.", 12, UiTheme.Dim, wrap: true));
        var world = Session.Content.WorldOob;
        var withForces = world?.Sites.Select(s => s.Side).Concat(world.Units.Select(u => u.Side)).ToHashSet() ?? new();
        foreach (var n in Session.Content.Nations.Values.OrderByDescending(n => withForces.Contains(n.Id)).ThenBy(n => n.Name))
        {
            if (n.Id == Me) continue;
            var h = new HBoxContainer();
            var atWar = Session.World.AtWar(Me, n.Id);
            var reach = withForces.Contains(n.Id) && withForces.Contains(Me) && Session.Zone(BadWaters.Sim.Command.FightZone.Key(Me, n.Id)) != null;
            var l = UiTheme.Text($"{n.Name}{(atWar ? " · AT WAR" : "")}{(withForces.Contains(n.Id) ? reach ? " · forces within reach of yours" : " · forces out of reach" : "")}", 13, atWar ? UiTheme.Bad : UiTheme.Ink);
            l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            h.AddChild(l);
            var b = new Button { Text = atWar ? "Make peace" : "Declare war", TooltipText = atWar ? "Stop the fighting (the damage stays)." : reach ? "Forces within reach of each other start fighting at once." : "No forces in reach: a war with nothing to fight yet." };
            var id = n.Id;
            b.Pressed += () => { if (atWar) Session.MakePeace(Me, id); else Session.DeclareWar(Me, id); Refresh(); };
            h.AddChild(b);
            _v.AddChild(h);
        }
    }
}
