using Godot;

namespace BadWaters.Game;

/// <summary>
/// Placeholder palette: "Ops Floor · War Room colours". Swap the constants here once a UI direction is picked.
/// </summary>
public static class UiTheme
{
    public static readonly Color Bg = new("03070c");
    public static readonly Color Panel = new(0.03f, 0.08f, 0.13f, 0.94f);
    public static readonly Color Panel2 = new(0.05f, 0.12f, 0.19f, 0.94f);
    public static readonly Color Line = new(0.50f, 0.85f, 1.0f, 0.20f);
    public static readonly Color Ink = new("d8f3ff");
    public static readonly Color Dim = new("72a0b6");
    public static readonly Color Accent = new("7fd8ff");
    public static readonly Color AccentInk = new("02121c");
    public static readonly Color Bad = new("ff4d5a");
    public static readonly Color Good = new("5cf2b0");

    public static StyleBoxFlat Box(Color bg, Color border, int radius = 6, int borderWidth = 1)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        s.SetBorderWidthAll(borderWidth);
        s.SetCornerRadiusAll(radius);
        s.ContentMarginLeft = s.ContentMarginRight = 12;
        s.ContentMarginTop = s.ContentMarginBottom = 8;
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
        t.SetFontSize("font_size", "Label", 15);
        t.SetFontSize("font_size", "Button", 15);
        return t;
    }

    public static Label Text(string text, int size = 15, Color? color = null, bool wrap = false)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color ?? Ink);
        if (wrap) l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return l;
    }
}
