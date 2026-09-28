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

    // ---------------- Theatre Ops: a second visual language (dense, instrument-like, monospace, amber on charcoal) ----
    public static readonly Color TacBg = new("0a0c0e");
    public static readonly Color TacPanel = new(0.055f, 0.066f, 0.075f, 0.97f);
    public static readonly Color TacPanel2 = new(0.085f, 0.098f, 0.108f, 0.97f);
    public static readonly Color TacLine = new(0.55f, 0.62f, 0.52f, 0.35f);
    public static readonly Color TacInk = new("c9d6c3");
    public static readonly Color TacDim = new("7c8a80");
    public static readonly Color TacAccent = new("e8b04a");
    public static readonly Color TacAccentInk = new("1a1206");
    /// <summary>Set while Theatre Ops builds its UI: Text() maps the strategic colours onto the tactical palette.</summary>
    public static bool Tactical { get; set; }
    public static Color Map(Color c) => !Tactical ? c : c == Ink ? TacInk : c == Dim ? TacDim : c == Accent ? TacAccent : c == AccentInk ? TacAccentInk : c;
    private static SystemFont? _mono;
    public static SystemFont Mono => _mono ??= new SystemFont { FontNames = new[] { "Consolas", "Lucida Console", "Cascadia Mono", "Courier New", "monospace" } };

    public static Theme BuildTactical()
    {
        var t = new Theme { DefaultFont = Mono, DefaultFontSize = 12 };
        StyleBoxFlat B(Color bg, Color border, int w = 1)
        {
            var s = new StyleBoxFlat { BgColor = bg, BorderColor = border };
            s.SetBorderWidthAll(w); s.SetCornerRadiusAll(1);
            s.ContentMarginLeft = s.ContentMarginRight = 6; s.ContentMarginTop = s.ContentMarginBottom = 3;
            return s;
        }
        foreach (var type in new[] { "Label", "Button", "LineEdit", "OptionButton", "CheckBox", "PopupMenu", "SpinBox", "ItemList", "TabBar", "RichTextLabel" })
        {
            t.SetColor("font_color", type, TacInk);
            t.SetFontSize("font_size", type, 12);
        }
        t.SetFontSize("normal_font_size", "RichTextLabel", 12);
        t.SetColor("default_color", "RichTextLabel", TacInk);
        t.SetColor("font_hover_color", "Button", TacAccent);
        t.SetColor("font_pressed_color", "Button", TacAccentInk);
        t.SetColor("font_disabled_color", "Button", new Color(TacDim, 0.5f));
        t.SetStylebox("normal", "Button", B(TacPanel2, TacLine));
        t.SetStylebox("hover", "Button", B(TacPanel2, TacAccent));
        t.SetStylebox("pressed", "Button", B(TacAccent, TacAccent));
        t.SetStylebox("disabled", "Button", B(new Color(TacPanel2, 0.5f), new Color(TacLine, 0.15f)));
        t.SetStylebox("focus", "Button", B(new Color(0, 0, 0, 0), TacAccent));
        t.SetStylebox("panel", "PanelContainer", B(TacPanel, TacLine));
        t.SetStylebox("normal", "LineEdit", B(TacBg, TacLine));
        foreach (var st in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
            t.SetStylebox(st, "CheckBox", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), ContentMarginLeft = 2 });
        t.SetColor("font_pressed_color", "CheckBox", TacInk);
        return t;
    }

    /// <summary>A small caps header strip for tactical windows.</summary>
    public static Label TacHeader(string text) => new Label { Text = text.ToUpperInvariant(), Modulate = TacAccent };

    public static Label Text(string text, int size = 15, Color? color = null, bool wrap = false)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", Tactical ? Math.Max(10, Size(size) - 1) : Size(size));
        l.AddThemeColorOverride("font_color", Map(color ?? Ink));
        if (wrap) l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return l;
    }
}
