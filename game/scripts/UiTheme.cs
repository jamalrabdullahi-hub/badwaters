using Godot;

namespace BadWaters.Game;

/// <summary>
/// Final palette: "Ops Floor · War Room colours", deep navy (darkened on user feedback). The single source of UI colour.
/// </summary>
public static class UiTheme
{
    public static readonly Color Bg = new("010408");
    public static readonly Color Panel = new(0.012f, 0.035f, 0.065f, 0.96f);
    public static readonly Color Panel2 = new(0.022f, 0.058f, 0.100f, 0.96f);
    public static readonly Color Line = new(0.30f, 0.60f, 0.85f, 0.22f);
    public static readonly Color Ink = new("d8f3ff");
    public static readonly Color Dim = new("6a8fa6");
    public static readonly Color Accent = new("4fb0e0");
    public static readonly Color AccentInk = new("02121c");
    public static readonly Color Bad = new("ff4d5a");
    public static readonly Color Good = new("5cf2b0");

    public static StyleBoxFlat Box(Color bg, Color border, int radius = 6, int borderWidth = 1)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        s.SetBorderWidthAll(borderWidth);
        s.SetCornerRadiusAll(radius);
        s.ContentMarginLeft = s.ContentMarginRight = 8;
        s.ContentMarginTop = s.ContentMarginBottom = 4;
        return s;
    }

    public static Theme Build()
    {
        var t = new Theme();
        t.SetColor("font_color", "Label", Ink);
        t.SetColor("font_color", "Button", Ink);
        t.SetColor("font_hover_color", "Button", Accent);
        t.SetColor("font_pressed_color", "Button", AccentInk);
        t.SetColor("font_disabled_color", "Button", new Color(Dim, 0.45f));
        t.SetStylebox("normal", "Button", Box(Panel2, Line));
        t.SetStylebox("hover", "Button", Box(Panel2, Accent));
        t.SetStylebox("pressed", "Button", Box(Accent, Accent));
        t.SetStylebox("disabled", "Button", Box(new Color(Panel2, 0.5f), new Color(Line, 0.1f)));
        t.SetStylebox("focus", "Button", Box(new Color(0, 0, 0, 0), Accent, 6, 2));
        t.SetStylebox("panel", "PanelContainer", Box(Panel, Line));
        // checkboxes: no button fill, just the tick
        foreach (var st in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
            t.SetStylebox(st, "CheckBox", new StyleBoxFlat { BgColor = st.StartsWith("hover") ? new Color(Accent, 0.08f) : new Color(0, 0, 0, 0), ContentMarginLeft = 2, ContentMarginTop = 1, ContentMarginBottom = 1 });
        t.SetColor("font_pressed_color", "CheckBox", Ink);
        t.SetColor("font_hover_pressed_color", "CheckBox", Accent);
        t.SetFontSize("font_size", "Label", Base);
        foreach (var type in new[] { "Button", "LineEdit", "OptionButton", "CheckBox", "PopupMenu", "TabBar", "ItemList", "TextEdit", "SpinBox", "TooltipLabel" }) t.SetFontSize("font_size", type, Base);
        return t;
    }

    /// <summary>Base UI font size (was 15; reduced on user feedback).</summary>
    public const int Base = 12;
    /// <summary>Scale a legacy size (authored for the old 15 px base) to the current base.</summary>
    public static int Size(int size) => Math.Max(10, (int)Math.Round(size * 0.8));

    public static Label Text(string text, int size = 15, Color? color = null, bool wrap = false)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", Size(size));
        l.AddThemeColorOverride("font_color", color ?? Ink);
        if (wrap) l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return l;
    }
}
