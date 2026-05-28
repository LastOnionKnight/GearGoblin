// Theme/TtChrome.cs
//
// v0.6.7 — Track 2 chrome module. First surface in the ember/frost-blue
// visual language that replaces TlfTheme (gold/navy) over the v0.6.7 →
// v1.0 migration arc.
//
// Cohabitation rules during the migration:
//   - TlfTheme.cs stays untouched and continues to chrome the Character
//     tab, Materia tab, Quick Start, About, Settings, Diagnostics, and
//     Feedback tabs through v0.6.7.x and v0.6.8.x.
//   - TtChrome.cs chromes new surfaces as they get repainted: Plan tab
//     (v0.6.7), Audit fork (v0.7.x), Character tab (v0.7.0), the rest
//     of the surfaces one at a time through v0.7.x.
//   - The two never push window-level chrome at the same time. TtChrome
//     is *local* — its Push/Pop are scoped to a tab's content area, and
//     it relies on TlfTheme having already pushed window-level chrome
//     (WindowBg/ChildBg) further up the stack.
//   - At v1.0 TlfTheme.cs is deleted and TtChrome takes over the entire
//     window. About panel rebuilt at that point.
//
// Visual language summary (lifted verbatim from
// design_handoff_tlf_hud_v01/RUNTIME_PORT.md §1.3):
//   - Background:  rgba(20, 22, 28, 0.90) — ink, slightly warmer than
//                  TlfTheme.InkPanel (#0e1232). Drops the navy cast.
//   - Accent:      #D67B3C ember orange. Replaces TlfTheme.Lantern gold.
//                  Used for eyebrow labels, severity-critical badges,
//                  active state indicators.
//   - Outline:     rgba(178, 178, 178, 1.0) hard frost-grey. The
//                  primary frame line.
//   - Outline-soft: rgba(178, 178, 178, 0.22) — the doubled inner
//                  hairline, faint enough to read as a "double border"
//                  signature without competing visually with content.
//
// The doubled-inner-frame is the chrome signature. Per the design-
// principles deck: "outer 2px border at 0 inset; inner 1px hairline
// at 6 inset". Implemented via ImDrawList.AddRect on the window
// drawlist after the card content's BeginGroup/EndGroup. Card content
// has 14px horizontal / 10px vertical padding from the frame so the
// frame lines never overlap text (only the AddRect outline draws on
// top of the content layer, and at 14px padding it sits well clear
// of any glyph).

using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace GearGoblin.Theme;

public static class TtChrome
{
    // ── Track 2 palette (RGB hex from RUNTIME_PORT.md §1.3) ─────────────

    // Ink — backdrop layer for cards and popout surfaces
    public static readonly Vector4 InkDark    = Rgba(0x14, 0x16, 0x1c, 0.90f);
    public static readonly Vector4 InkDeeper  = Rgba(0x0e, 0x10, 0x16, 0.95f);
    public static readonly Vector4 InkSurface = Rgb(0x1a, 0x1c, 0x24);

    // Ember — warm orange accent (replaces TlfTheme.Lantern gold for Track 2)
    public static readonly Vector4 Ember       = Rgb(0xd6, 0x7b, 0x3c);
    public static readonly Vector4 EmberBright = Rgb(0xf2, 0xa0, 0x57);
    public static readonly Vector4 EmberDeep   = Rgb(0xa8, 0x58, 0x20);
    public static readonly Vector4 EmberGlow   = Rgba(0xd6, 0x7b, 0x3c, 0.35f);

    // Frost — body text and frame outlines
    public static readonly Vector4 FrostText        = Rgb(0xf2, 0xf2, 0xf5);
    public static readonly Vector4 FrostMuted       = Rgba(0xf2, 0xf2, 0xf5, 0.75f);
    public static readonly Vector4 FrostFaint       = Rgba(0xf2, 0xf2, 0xf5, 0.45f);
    public static readonly Vector4 FrostOutline     = Rgba(0xb2, 0xb2, 0xb2, 1.00f);
    public static readonly Vector4 FrostOutlineMid  = Rgba(0xb2, 0xb2, 0xb2, 0.45f);
    public static readonly Vector4 FrostOutlineSoft = Rgba(0xb2, 0xb2, 0xb2, 0.22f);

    // Severity ramp (Tactics Popout findings)
    public static readonly Vector4 SeverityCritical = Rgb(0xd6, 0x7b, 0x3c);  // ember
    public static readonly Vector4 SeverityWarning  = Rgb(0xe2, 0xc3, 0x6a);  // yellow
    public static readonly Vector4 SeverityNote     = Rgb(0x7f, 0xc9, 0xee);  // frost-blue

    // HP / match-success green
    public static readonly Vector4 HpGreen       = Rgb(0x6f, 0xbf, 0x6a);
    public static readonly Vector4 HpGreenBright = Rgb(0x8f, 0xd4, 0x88);

    // Farm / mismatch (warmer than ember to read as "action needed")
    public static readonly Vector4 Farm = Rgb(0xff, 0x9d, 0x4a);

    // ── Glyphs (carried from TlfTheme; same Unicode policy) ─────────────
    //
    // We keep » for section eyebrow and ▶ for advisor/forward to stay
    // visually consistent across the two themes during the migration.
    // v0.6.6.5 lesson: outlined-shape Unicode (◇, U+25C7) doesn't render
    // in Dalamud's default font — use Latin-1 (» U+00BB) or filled-shape
    // Geometric (▶ U+25B6, ◆ U+25C6) only.

    public const string GlyphEyebrow = "»";   // section heading
    public const string GlyphForward = "▶";   // active / forward
    public const string GlyphCorner  = "◆";   // ember-diamond corner marker (cards)

    // ── Card chrome geometry ────────────────────────────────────────────

    /// <summary>Outer-frame thickness in pixels.</summary>
    public const float CardOuterThickness = 2.0f;

    /// <summary>Inner-hairline thickness in pixels.</summary>
    public const float CardInnerThickness = 1.0f;

    /// <summary>Inner-hairline inset from the outer frame in pixels.</summary>
    public const float CardInnerInset = 6.0f;

    /// <summary>Horizontal padding inside the card (between outer frame and content).</summary>
    public const float CardPaddingX = 14.0f;

    /// <summary>Vertical padding inside the card (between outer frame and content).</summary>
    public const float CardPaddingY = 10.0f;

    // ── Card state (single-card-at-a-time; cards cannot nest) ───────────

    private static Vector2 s_cardStart;
    private static float   s_cardWidth;
    private static bool    s_cardActive;

    // ── Style stack ─────────────────────────────────────────────────────
    //
    // v1.0: TtChrome takes over the entire window.

    private const int StackDepth = 12;

    /// <summary>
    /// Push a TtChrome style stack onto ImGui. Scoped: callers MUST call
    /// Pop() in the same frame, ideally via try/finally for crash safety.
    /// </summary>
    public static void Push()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg,         InkDark);
        ImGui.PushStyleColor(ImGuiCol.ChildBg,          InkSurface);
        ImGui.PushStyleColor(ImGuiCol.FrameBg,          InkDark);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered,   InkDeeper);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive,    InkDeeper);
        ImGui.PushStyleColor(ImGuiCol.TitleBg,          InkDark);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive,    InkSurface);
        ImGui.PushStyleColor(ImGuiCol.Tab,              InkSurface);
        ImGui.PushStyleColor(ImGuiCol.TabActive,        InkDeeper);
        ImGui.PushStyleColor(ImGuiCol.TabHovered,       InkDeeper);
        ImGui.PushStyleColor(ImGuiCol.Separator,        FrostOutlineSoft);
        ImGui.PushStyleColor(ImGuiCol.Text,             FrostText);
    }

    public static void Pop() => ImGui.PopStyleColor(StackDepth);

    // ── Composite helpers ───────────────────────────────────────────────

    /// <summary>
    /// Eyebrow-style section label rendered in pixel font (small caps) with
    /// ember accent. The Track 2 parallel to TlfTheme.Eyebrow().
    /// Pass the plugin's FontAtlasManager so we can push the pixel font;
    /// falls back to default-font ALL CAPS if the pixel font is null.
    /// </summary>
    public static void Eyebrow(FontAtlasManager fonts, string label)
    {
        using (fonts.Pixel.PushOrNull())
        {
            ImGui.TextColored(Ember, $"{GlyphEyebrow} {label.ToUpperInvariant()}");
        }
    }

    /// <summary>
    /// Italic body quip. Prefers Cormorant Garamond Italic (v0.6.7 new font);
    /// falls back to EB Garamond Italic, then default font. Used for short
    /// flavor lines that sit under an eyebrow — "Paste an Etro or XIVGear
    /// URL. Grub-Grub will compare."
    /// </summary>
    public static void Quip(FontAtlasManager fonts, string text)
    {
        var handle = fonts.CormorantItalic ?? fonts.GaramondItalic;
        using (handle.PushOrNull())
        {
            ImGui.PushStyleColor(ImGuiCol.Text, FrostMuted);
            ImGui.TextUnformatted(text);
            ImGui.PopStyleColor();
        }
    }

    /// <summary>
    /// Numeric value in JetBrains Mono (v0.6.7 new font). Used for iLvl,
    /// stat deltas, and other numerics where digit-alignment matters.
    /// Falls back to default font.
    /// </summary>
    public static void Number(FontAtlasManager fonts, string text, Vector4 color)
    {
        using (fonts.JetBrainsMonoBody.PushOrNull())
        {
            ImGui.TextColored(color, text);
        }
    }

    /// <summary>
    /// Compact pill-style badge with bracket framing. Same shape as
    /// TlfTheme.Pill but uses Track 2 palette by default.
    /// </summary>
    public static void Pill(string text, Vector4 color)
    {
        ImGui.TextColored(color, $"[ {text} ]");
    }

    // ── Card chrome (the signature doubled-frame) ───────────────────────
    //
    // Usage:
    //
    //   TtChrome.BeginCard();
    //   TtChrome.Eyebrow(fonts, "Plan · BiS Paste");
    //   TtChrome.Quip(fonts, "Paste an Etro or XIVGear URL. Grub-Grub will compare.");
    //   // ...content goes here...
    //   TtChrome.EndCard();
    //
    // Implementation notes:
    //   BeginCard/EndCard stash the starting screen position and full
    //   content width, then BeginGroup() so subsequent items are bounded
    //   together. EndCard() reads the group's bounding rect, extends it
    //   to the stashed full width, and draws the doubled frame on the
    //   window drawlist via two AddRect calls (outer 2px frost-outline +
    //   inner 1px hairline at 6px inset).
    //
    //   The frame draws on top of the content layer, but with 14px
    //   horizontal padding inside the card, the frame lines sit well
    //   clear of any glyph. No drawlist channel-split needed.
    //
    // Constraint: cards CANNOT nest (single-card-at-a-time static state).

    /// <summary>
    /// Begin a card-chromed region. Must be paired with EndCard() in the
    /// same frame. Throws InvalidOperationException if called while
    /// another card is already active (cards cannot nest).
    /// </summary>
    public static void BeginCard()
    {
        if (s_cardActive)
            throw new InvalidOperationException(
                "TtChrome.BeginCard called while another card was active. " +
                "Cards cannot nest — close the outer card with EndCard() first.");

        s_cardActive = true;
        s_cardStart  = ImGui.GetCursorScreenPos();
        s_cardWidth  = ImGui.GetContentRegionAvail().X;

        ImGui.BeginGroup();
        ImGui.Indent(CardPaddingX);
        ImGui.Dummy(new Vector2(0, CardPaddingY));
    }

    /// <summary>
    /// Close a card region opened with BeginCard(). Reads the group's
    /// bounding rect, extends it to the full content width, and draws
    /// the doubled-frame chrome via AddRect on the window drawlist.
    /// </summary>
    public static void EndCard()
    {
        if (!s_cardActive)
            throw new InvalidOperationException(
                "TtChrome.EndCard called without a matching BeginCard.");

        ImGui.Dummy(new Vector2(0, CardPaddingY));
        ImGui.Unindent(CardPaddingX);
        ImGui.EndGroup();

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();

        // Extend the card to the full content width so the right frame
        // edge is flush rather than wrapping the longest line of content.
        max.X = s_cardStart.X + s_cardWidth;

        var drawList = ImGui.GetWindowDrawList();
        var outerU32 = ImGui.GetColorU32(FrostOutline);
        var innerU32 = ImGui.GetColorU32(FrostOutlineSoft);

        // Outer 2px frame.
        drawList.AddRect(min, max, outerU32, 0f, 0, CardOuterThickness);

        // Inner 1px hairline, inset by 6px.
        drawList.AddRect(
            new Vector2(min.X + CardInnerInset, min.Y + CardInnerInset),
            new Vector2(max.X - CardInnerInset, max.Y - CardInnerInset),
            innerU32, 0f, 0, CardInnerThickness);

        s_cardActive = false;
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static Vector4 Rgb(byte r, byte g, byte b) =>
        new(r / 255f, g / 255f, b / 255f, 1f);

    private static Vector4 Rgba(byte r, byte g, byte b, float a) =>
        new(r / 255f, g / 255f, b / 255f, a);
}
