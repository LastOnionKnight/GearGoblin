// Theme/TtChrome.cs
//
// Track 3 Phase 3 chrome module. Aligns the plugin with the Tonberry Tactics
// web application design language (cobalt+gold dark panels).

using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace GearGoblin.Theme;

public static class TtChrome
{
    // ── Phase 3 Palette ──────────────────────────────────────────────────

    // Background & Panels
    public static readonly Vector4 Bg       = Rgb(0x0a, 0x13, 0x20); // #0a1320
    public static readonly Vector4 Bg2      = Rgb(0x0d, 0x18, 0x28); // #0d1828
    public static readonly Vector4 Panel    = Rgb(0x10, 0x1d, 0x30); // #101d30
    public static readonly Vector4 Panel2   = Rgb(0x14, 0x25, 0x39); // #142539
    public static readonly Vector4 Sink     = Rgb(0x0a, 0x14, 0x22); // #0a1422

    // Lines & Borders
    public static readonly Vector4 Line       = Rgb(0x22, 0x38, 0x4f); // #22384f
    public static readonly Vector4 LineSoft   = Rgb(0x1a, 0x2c, 0x40); // #1a2c40
    public static readonly Vector4 LineGold   = Rgba(0xc9, 0xa2, 0x27, 0.30f);
    public static readonly Vector4 LineCobalt = Rgba(0x2d, 0x6c, 0xdf, 0.35f);

    // Accents
    public static readonly Vector4 Gold       = Rgb(0xc9, 0xa2, 0x27); // #C9A227
    public static readonly Vector4 GoldBright = Rgb(0xe5, 0xc2, 0x4e); // #E5C24E
    public static readonly Vector4 GoldDim    = Rgb(0x8a, 0x73, 0x20); // #8A7320

    public static readonly Vector4 Cobalt       = Rgb(0x2d, 0x6c, 0xdf); // #2D6CDF
    public static readonly Vector4 CobaltBright = Rgb(0x4a, 0x8b, 0xff); // #4A8BFF
    public static readonly Vector4 CobaltDeep   = Rgb(0x1b, 0x3f, 0x8c); // #1B3F8C

    public static readonly Vector4 Tonberry   = Rgb(0x9b, 0xc0, 0x63); // #9bc063

    // Text & Foreground
    public static readonly Vector4 Fg       = Rgb(0xe7, 0xee, 0xf6); // #E7EEF6
    public static readonly Vector4 Fg2      = Rgb(0xaf, 0xc0, 0xd2); // #AFC0D2
    public static readonly Vector4 FgMuted  = Rgb(0x7c, 0x90, 0xa6); // #7C90A6
    public static readonly Vector4 FgFaint  = Rgb(0x56, 0x70, 0x8a); // #56708a

    // Semantic
    public static readonly Vector4 Ok    = Rgb(0x3a, 0x9d, 0x6e); // #3A9D6E
    public static readonly Vector4 Warn  = Rgb(0xe0, 0xa2, 0x3c); // #E0A23C
    public static readonly Vector4 Over  = Rgb(0xc0, 0x55, 0x6b); // #C0556B
    public static readonly Vector4 Err   = Rgb(0xc0, 0x55, 0x6b); // #C0556B

    // ── Glyphs ───────────────────────────────────────────────────────────

    public const string GlyphEyebrow = "»";   // section heading
    public const string GlyphForward = "▶";   // active / forward
    public const string GlyphCorner  = "◆";   // corner marker

    // ── Style stack ─────────────────────────────────────────────────────

    private const int StackDepth = 12;

    public static void Push()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg,         Bg);
        ImGui.PushStyleColor(ImGuiCol.ChildBg,          Sink);
        ImGui.PushStyleColor(ImGuiCol.FrameBg,          Bg2);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered,   Panel);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive,    Panel);
        ImGui.PushStyleColor(ImGuiCol.TitleBg,          Panel2);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive,    Panel2);
        ImGui.PushStyleColor(ImGuiCol.Tab,              Bg);
        ImGui.PushStyleColor(ImGuiCol.TabActive,        Panel);
        ImGui.PushStyleColor(ImGuiCol.TabHovered,       Panel);
        ImGui.PushStyleColor(ImGuiCol.Separator,        Line);
        ImGui.PushStyleColor(ImGuiCol.Text,             Fg);
    }

    public static void Pop() => ImGui.PopStyleColor(StackDepth);

    // ── Composite helpers ───────────────────────────────────────────────

    public static void Eyebrow(FontAtlasManager fonts, string label)
    {
        using (fonts.Pixel.PushOrNull())
        {
            ImGui.TextColored(Gold, $"{GlyphEyebrow} {label.ToUpperInvariant()}");
        }
    }

    public static void Quip(FontAtlasManager fonts, string text)
    {
        var handle = fonts.CormorantItalic ?? fonts.GaramondItalic;
        using (handle.PushOrNull())
        {
            ImGui.PushStyleColor(ImGuiCol.Text, FgMuted);
            ImGui.TextUnformatted(text);
            ImGui.PopStyleColor();
        }
    }

    public static void Number(FontAtlasManager fonts, string text, Vector4 color)
    {
        using (fonts.JetBrainsMonoBody.PushOrNull())
        {
            ImGui.TextColored(color, text);
        }
    }

    public static void Pill(string text, Vector4 color)
    {
        ImGui.TextColored(color, $"[ {text} ]");
    }

    public static void BeginPanel(string id, float height = 0f)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Panel);
        ImGui.PushStyleColor(ImGuiCol.Border, Line);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 11f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14, 14));
        ImGui.BeginChild(id, new Vector2(0, height), true, ImGuiWindowFlags.NoScrollbar);
    }

    public static void EndPanel()
    {
        ImGui.EndChild();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    public static Vector4 Rgb(byte r, byte g, byte b) =>
        new(r / 255f, g / 255f, b / 255f, 1f);

    public static Vector4 Rgba(byte r, byte g, byte b, float a) =>
        new(r / 255f, g / 255f, b / 255f, a);
}
