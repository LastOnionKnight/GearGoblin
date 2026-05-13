// Theme/TlfTheme.cs
//
// v0.4.7 Phase-1 "TLF Lite" theme for the /goblin ImGui window.
//
// Palette and visual language ported from Tonberry Tactics' Claude
// Design output (TLF Gear Division aesthetic). Single source of
// truth — every color used in the plugin UI should reach here.
//
// Constraints honored:
//   - Dalamud's bundled fonts only (no custom font loading in Phase 1).
//   - No ImDrawList custom rendering (no gradients, no sprites).
//   - Interactive controls keep mostly-default ImGui chrome so users
//     don't have to relearn buttons and tab bars.
//
// Phase-1 covers: palette, section glyphs, eyebrow labels, pill
// badges, credo blocks, themed style stack pushed at window entry.
// Phase 2 (v0.5.x) will add custom fonts via IFontAtlas.

using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace GearGoblin.Theme;

public static class TlfTheme
{
    // ── Palette (RGB hex from Tonberry Tactics styles.css) ──────────────

    // Ink / night
    public static readonly Vector4 InkVoid       = Rgb(0x03, 0x05, 0x0f);
    public static readonly Vector4 InkDeep       = Rgb(0x06, 0x0a, 0x28);
    public static readonly Vector4 Ink           = Rgb(0x0c, 0x11, 0x30);
    public static readonly Vector4 Ink2          = Rgb(0x14, 0x18, 0x4c);
    public static readonly Vector4 InkPanel      = Rgb(0x0e, 0x12, 0x32);
    public static readonly Vector4 InkPanelAlt   = Rgb(0x16, 0x1a, 0x44);
    public static readonly Vector4 Ink3          = Rgb(0x1a, 0x1f, 0x55);

    // TLF gold (Onion Knight)
    public static readonly Vector4 Gold          = Rgb(0xc9, 0xb2, 0x7e);
    public static readonly Vector4 GoldBright    = Rgb(0xff, 0xd9, 0x6b);
    public static readonly Vector4 GoldDim       = Rgb(0x8a, 0x7a, 0x55);
    public static readonly Vector4 GoldSoft      = Rgb(0xe5, 0xcf, 0x94);

    // Lantern flame
    public static readonly Vector4 Lantern       = Rgb(0xf5, 0xb9, 0x5d);
    public static readonly Vector4 LanternHot    = Rgb(0xff, 0xce, 0x5e);
    public static readonly Vector4 LanternGlow   = Rgba(0xf5, 0xb9, 0x5d, 0.35f);

    // Tonberry green
    public static readonly Vector4 Tonberry      = Rgb(0x6b, 0x8a, 0x3e);
    public static readonly Vector4 TonberryBright = Rgb(0x9b, 0xc0, 0x63);
    public static readonly Vector4 TonberryDeep  = Rgb(0x3d, 0x54, 0x21);

    // Steel / silver — "knife"
    public static readonly Vector4 Knife         = Rgb(0xd8, 0xdd, 0xe8);
    public static readonly Vector4 KnifeDim      = Rgb(0x7a, 0x8a, 0xae);

    // Frost / paper (body text)
    public static readonly Vector4 Frost         = Rgb(0xe8, 0xe6, 0xd5);
    public static readonly Vector4 FrostSoft     = Rgb(0xc2, 0xc5, 0xd8);
    public static readonly Vector4 FrostDim      = Rgb(0x80, 0x85, 0xa3);
    public static readonly Vector4 FrostFaint    = Rgb(0x4a, 0x51, 0x76);

    // States
    public static readonly Vector4 Blood         = Rgb(0xd2, 0x45, 0x45);
    public static readonly Vector4 BloodBright   = Rgb(0xff, 0x6a, 0x5e);
    public static readonly Vector4 Ship          = Rgb(0x6d, 0xb4, 0x4e);
    public static readonly Vector4 ShipBright    = Rgb(0x8d, 0xc8, 0x80);
    public static readonly Vector4 Ice           = Rgb(0x7e, 0xc0, 0xc4);
    public static readonly Vector4 Warning       = Rgb(0xd4, 0x92, 0x50);

    // Borders
    public static readonly Vector4 BorderPixel     = Rgb(0x2a, 0x2f, 0x6e);
    public static readonly Vector4 BorderPixelLite = Rgb(0x3a, 0x3f, 0x8a);
    public static readonly Vector4 BorderWarm      = Rgb(0x5a, 0x4a, 0x30);

    // ── Glyphs ──────────────────────────────────────────────────────────
    //
    // The TLF design uses three section glyphs in the prototype.
    // Keeping them as named constants so consistency is enforced.

    public const string GlyphSection    = "◇";   // section eyebrow (TLF Manifesto, etc)
    public const string GlyphAdvisor    = "▶";   // active / forward / call-to-action
    public const string GlyphCredo      = "◆";   // credo / standing-ready footer

    // ── Style stack ─────────────────────────────────────────────────────
    //
    // Push at the top of MainWindow.Draw(), pop at the end (or wrap in
    // a using-disposable if you want RAII). Repaints window chrome,
    // headers, frame backgrounds, tab bar colors, separator lines, and
    // body text in TLF palette. Interactive controls (buttons, sliders,
    // checkboxes) are deliberately left default so muscle memory stays
    // intact.

    private const int StackDepth = 12;

    public static void Push()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg,         Ink);
        ImGui.PushStyleColor(ImGuiCol.ChildBg,          InkPanel);
        ImGui.PushStyleColor(ImGuiCol.FrameBg,          InkPanelAlt);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered,   Ink3);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive,    Ink3);
        ImGui.PushStyleColor(ImGuiCol.TitleBg,          Ink);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive,    InkPanel);
        ImGui.PushStyleColor(ImGuiCol.Tab,              InkPanel);
        ImGui.PushStyleColor(ImGuiCol.TabActive,        InkPanelAlt);
        ImGui.PushStyleColor(ImGuiCol.TabHovered,       Ink2);
        ImGui.PushStyleColor(ImGuiCol.Separator,        BorderPixel);
        ImGui.PushStyleColor(ImGuiCol.Text,             Frost);
    }

    public static void Pop() => ImGui.PopStyleColor(StackDepth);

    // ── Composite helpers ───────────────────────────────────────────────

    /// <summary>
    /// Eyebrow-style section label. Renders as
    /// "◇ Title" in lantern-gold, used to mark visual sections within
    /// a tab. Matches the TLF prototype's `.h-eyebrow` style.
    /// </summary>
    public static void Eyebrow(string title)
    {
        ImGui.TextColored(Lantern, $"{GlyphSection} {title}");
    }

    /// <summary>
    /// Advisor-glyph header — "▶ TITLE" in TLF gold-bright. Use for
    /// step headers in workflow guides and for menu-box-style titles.
    /// </summary>
    public static void Advisor(string title)
    {
        ImGui.TextColored(GoldBright, $"{GlyphAdvisor} {title}");
    }

    /// <summary>
    /// Credo block — italic, frost-soft body color, slightly inset.
    /// Used for manifesto-flavored copy. ImGui doesn't render italic
    /// without a custom font; for now we use a subdued color + a
    /// double dash framing to read as a quotation.
    /// </summary>
    public static void Credo(string lines)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, FrostSoft);
        ImGui.TextUnformatted("    " + lines);
        ImGui.PopStyleColor();
    }

    /// <summary>
    /// Compact pill-style badge with colored text and a thin colored
    /// border-substitute (square brackets). Mimics the TLF
    /// `.pill .gold` / `.pill .ice` look in a font-only medium.
    /// </summary>
    public static void Pill(string text, Vector4 color)
    {
        ImGui.TextColored(color, $"[ {text} ]");
    }

    /// <summary>
    /// Standing-ready footer line — matches the TLF prototype's
    /// `Footer` block ("The Onion Knight stands ready"). Renders
    /// centered-feeling with the ◆ glyph on either side. Used in
    /// the About tab and at the bottom of the Quick Start tab.
    /// v0.6.0: corrected leftover "GEARGOBLIN" → "TONBERRY TACTICS"
    /// brand string (regression from v0.4.7.1).
    /// </summary>
    public static void StandingReadyFooter(string version)
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, GoldDim);
        ImGui.TextUnformatted(
            $"  {GlyphCredo}  The Onion Knight stands ready  {GlyphCredo}");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.Text, FrostFaint);
        ImGui.TextUnformatted(
            $"  TONBERRY TACTICS · v{version} · NO GEAR · NO HOPE · NO PANTS · JUST ONIONS");
        ImGui.PopStyleColor();
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static Vector4 Rgb(byte r, byte g, byte b) =>
        new(r / 255f, g / 255f, b / 255f, 1f);

    private static Vector4 Rgba(byte r, byte g, byte b, float a) =>
        new(r / 255f, g / 255f, b / 255f, a);
}
