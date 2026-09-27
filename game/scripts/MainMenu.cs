using BadWaters.Data;
using Godot;

namespace BadWaters.Game;

/// <summary>New-campaign screen: pick a start date (1980 / 2000 / 2020), then a great or regional power.</summary>
public partial class MainMenu : Control
{
    private List<NationDef> _nations = new();
    private List<StartDateDef> _dates = new();
    private int _year = 2020;
    private string? _nation;
    private readonly ButtonGroup _dateGroup = new();
    private readonly ButtonGroup _nationGroup = new();
    private VBoxContainer _greatList = null!;
    private GridContainer _regionalList = null!;
    private Label _briefTitle = null!, _brief = null!;
    private Button _start = null!;

    public override void _Ready()
    {
        Theme = UiTheme.Build();
        LoadContent();
        // dev overrides are applied before the UI is built so the date buttons start in sync
        if (int.TryParse(DevScreenshot.Arg("year"), out var y)) _year = y;
        _nation = DevScreenshot.Arg("select");
        BuildUi();
        RefreshNations();
        DevScreenshot.MaybeCapture(this);
    }

    private static string DataDir()
    {
        // In the editor the project lives in <repo>/game; content lives in <repo>/data/core.
        var project = ProjectSettings.GlobalizePath("res://");
        var repo = Path.GetFullPath(Path.Combine(project, ".."));
        var dir = Path.Combine(repo, "data", "core");
        return Directory.Exists(dir) ? dir : Path.Combine(project, "data", "core");
    }

    private void LoadContent()
    {
        var loader = new ContentLoader(DataDir(), Directory.Exists(Path.Combine(DataDir(), "..", "..", "mods")) ? Directory.GetDirectories(Path.Combine(DataDir(), "..", "..", "mods")) : null);
        _nations = loader.Load<NationDef>("nations.json");
        _dates = loader.Load<StartDateDef>("start_dates.json");
        foreach (var e in loader.Errors) GD.PushError(e);
    }

    private void BuildUi()
    {
        AddChild(new ColorRect { Color = UiTheme.Bg, AnchorRight = 1, AnchorBottom = 1 });

        var root = new MarginContainer { AnchorRight = 1, AnchorBottom = 1 };
        foreach (var side in new[] { "left", "right", "top", "bottom" }) root.AddThemeConstantOverride($"margin_{side}", 36);
        AddChild(root);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 18);
        root.AddChild(col);

        col.AddChild(UiTheme.Text("BAD WATERS", 64, UiTheme.Accent));
        col.AddChild(UiTheme.Text("Naval, air and strategic command · 2D strategy", 18, UiTheme.Dim));

        // start dates
        col.AddChild(UiTheme.Text("START DATE", 13, UiTheme.Dim));
        var dates = new HBoxContainer();
        dates.AddThemeConstantOverride("separation", 12);
        col.AddChild(dates);
        foreach (var d in _dates.OrderBy(d => d.Year))
        {
            var b = new Button { Text = $"{d.Year}\n{d.Title}", ToggleMode = true, ButtonGroup = _dateGroup, CustomMinimumSize = new Vector2(240, 72), ButtonPressed = d.Year == _year };
            b.TooltipText = d.Blurb;
            var year = d.Year;
            b.Pressed += () => { _year = year; RefreshNations(); };
            dates.AddChild(b);
        }

        // nations
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 18);
        col.AddChild(row);

        var great = new PanelContainer { CustomMinimumSize = new Vector2(320, 0) };
        row.AddChild(great);
        var gv = new VBoxContainer();
        gv.AddThemeConstantOverride("separation", 8);
        great.AddChild(gv);
        gv.AddChild(UiTheme.Text("GREAT POWERS", 13, UiTheme.Dim));
        _greatList = new VBoxContainer();
        _greatList.AddThemeConstantOverride("separation", 6);
        gv.AddChild(_greatList);

        var regional = new PanelContainer { CustomMinimumSize = new Vector2(460, 0) };
        row.AddChild(regional);
        var rv = new VBoxContainer();
        rv.AddThemeConstantOverride("separation", 8);
        regional.AddChild(rv);
        rv.AddChild(UiTheme.Text("REGIONAL POWERS", 13, UiTheme.Dim));
        _regionalList = new GridContainer { Columns = 2 };
        _regionalList.AddThemeConstantOverride("h_separation", 6);
        _regionalList.AddThemeConstantOverride("v_separation", 6);
        rv.AddChild(_regionalList);

        var brief = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(brief);
        var bv = new VBoxContainer();
        bv.AddThemeConstantOverride("separation", 10);
        brief.AddChild(bv);
        bv.AddChild(UiTheme.Text("BRIEFING", 13, UiTheme.Dim));
        _briefTitle = UiTheme.Text("Choose a nation", 28);
        bv.AddChild(_briefTitle);
        _brief = UiTheme.Text("Pick a start date and a nation to see its situation.", 15, UiTheme.Dim, wrap: true);
        bv.AddChild(_brief);
        bv.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        _start = new Button { Text = "Take command ▸", Disabled = true, CustomMinimumSize = new Vector2(0, 48) };
        _start.Pressed += OnStart;
        bv.AddChild(_start);
    }

    private void RefreshNations()
    {
        foreach (var c in _greatList.GetChildren()) c.QueueFree();
        foreach (var c in _regionalList.GetChildren()) c.QueueFree();
        foreach (var n in _nations)
        {
            var available = n.AvailableIn(_year);
            var b = new Button
            {
                Text = n.DisplayName(_year), ToggleMode = true, ButtonGroup = _nationGroup, Disabled = !available,
                Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 40),
                TooltipText = available ? n.Summary : $"Not playable in {_year}.",
                ButtonPressed = available && n.Id == _nation,
            };
            var id = n.Id;
            b.Pressed += () => Select(id);
            if (n.Tier == "great") _greatList.AddChild(b); else _regionalList.AddChild(b);
        }
        if (_nation != null && !_nations.Single(n => n.Id == _nation).AvailableIn(_year)) _nation = null;
        UpdateBrief();
    }

    private void Select(string id)
    {
        _nation = id;
        UpdateBrief();
    }

    private void UpdateBrief()
    {
        var n = _nations.FirstOrDefault(x => x.Id == _nation);
        _start.Disabled = n == null;
        if (n == null) { _briefTitle.Text = "Choose a nation"; _brief.Text = "Pick a start date and a nation to see its situation."; return; }
        _briefTitle.Text = $"{n.DisplayName(_year)} · {_year}";
        _brief.Text = $"{n.Summary}\n\n{(n.Tier == "great" ? "Great power" : "Regional power")}. The campaign runs from {_year} to 2050.";
    }

    private void OnStart()
    {
        if (_nation == null) return;
        Session.Start(_nation, _year);
        GetTree().ChangeSceneToFile("res://scenes/Shell.tscn");
    }
}
