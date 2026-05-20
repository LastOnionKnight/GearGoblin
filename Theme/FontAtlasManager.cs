// Theme/FontAtlasManager.cs
//
// v0.6.0 — IFontAtlas Phase 2.
// v0.6.7 — Track 2 typography added.
//
// Loads custom .ttf fonts bundled in Assets/Fonts/ and exposes each as
// an IFontHandle for the rest of the plugin to Push() onto the ImGui
// font stack.
//
// Font roster:
//
//   ── Track 1 (TlfTheme, v0.6.0+) — gold/navy chrome ──
//   Cinzel             — display serif, headers and titles
//   Cinzel SemiBold    — display serif emphasis, "TONBERRY TACTICS" lockup
//   EB Garamond        — body serif, manifesto / credo / About prose
//   EB Garamond Italic — italic emphasis inside Garamond runs
//   Press Start 2P     — pixel font, version pills + eyebrow micro-labels
//
//   ── Track 2 (TtChrome, v0.6.7+) — ember/frost-blue chrome ──
//   Cormorant Garamond Regular — body quip prose in Track 2 surfaces
//   Cormorant Garamond Italic  — italic emphasis (Stab. voice, findings)
//   JetBrains Mono Regular     — numerics, iLvl, deltas (digit-aligned)
//   JetBrains Mono Bold        — meta labels, severity counts
//   Eorzea.ttf                 — ornamental rune accent (from the FFXIV
//                                Eorzean script — used for decorative
//                                section markers, NOT body text)
//
// Both font tracks coexist during the v0.6.7 → v1.0 migration. TlfTheme
// surfaces use Track 1; TtChrome surfaces use Track 2. At v1.0 the Track
// 1 fonts may be retired if no surface still references them.
//
// All five Track 2 .ttf files are loaded via the existing TryBuild
// pattern — if a font file is missing from Assets/Fonts/, the handle is
// left null and TtChrome falls back gracefully (default font or EB
// Garamond, depending on the surface). This means Brian can drop in
// font files at his own pace without breaking the build.
//
// Font handle lifecycle:
//   - Created in the ctor via atlas.NewDelegateFontHandle(...) calls.
//   - Dalamud owns the underlying texture atlas; we own the handles and
//     dispose them in our own Dispose() before the atlas is torn down.
//   - Each handle's .Push() returns an IDisposable for `using` blocks
//     (see Theme/FontPushExtensions.cs for the .PushOrNull() helper).
//   - On load failure (missing file, malformed .ttf), the handle is
//     left null. Errors are logged to /xllog for diagnosis. The plugin
//     still loads and works; you just don't see the custom typography
//     on that one font slot.

using System;
using System.IO;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;

namespace GearGoblin.Theme;

public sealed class FontAtlasManager : IDisposable
{
    // ── Track 1: Display serif (Cinzel) ─────────────────────────────────

    /// <summary>Cinzel Regular @ 32px — for the "TONBERRY TACTICS" wordmark in About.</summary>
    public IFontHandle? CinzelDisplay { get; }

    /// <summary>Cinzel Regular @ 22px — for tab section headers and the player name line.</summary>
    public IFontHandle? CinzelHeader  { get; }

    /// <summary>Cinzel SemiBold @ 16px — for menu-box titles and eyebrow labels.</summary>
    public IFontHandle? CinzelEmphasis { get; }

    // ── Track 1: Body serif (EB Garamond) ───────────────────────────────

    /// <summary>EB Garamond Regular @ 15px — manifesto / credo body prose.</summary>
    public IFontHandle? GaramondBody { get; }

    /// <summary>EB Garamond Italic @ 15px — italic emphasis inside Garamond runs.</summary>
    public IFontHandle? GaramondItalic { get; }

    // ── Track 1: Pixel display (Press Start 2P) ─────────────────────────

    /// <summary>Press Start 2P @ 10px — version pills, parsed-status badges, micro-labels.</summary>
    public IFontHandle? Pixel { get; }

    /// <summary>Press Start 2P @ 32px — Character tab portrait jobAbbr fallback glyph (v0.6.6.2).</summary>
    public IFontHandle? PixelDisplay { get; }

    // ── Track 2: Body serif (Cormorant Garamond) v0.6.7 ─────────────────
    //
    // Distinct family from EB Garamond. Cormorant is a display-oriented
    // Garamond revival with slightly more elegant proportions — used in
    // Track 2 for quips, finding-body lines, and the "Stab." voice italic
    // emphasis per the design handoff.

    /// <summary>Cormorant Garamond Regular @ 15px — Track 2 body quip prose.</summary>
    public IFontHandle? CormorantBody { get; }

    /// <summary>Cormorant Garamond Italic @ 15px — Track 2 italic quips and Stab. voice.</summary>
    public IFontHandle? CormorantItalic { get; }

    // ── Track 2: Monospace (JetBrains Mono) v0.6.7 ──────────────────────

    /// <summary>JetBrains Mono Regular @ 11px — Track 2 numerics, iLvl, stat deltas.</summary>
    public IFontHandle? JetBrainsMonoBody { get; }

    /// <summary>JetBrains Mono Bold @ 10px — Track 2 meta labels, severity counts.</summary>
    public IFontHandle? JetBrainsMonoMeta { get; }

    // ── Track 2: Ornamental rune (Eorzea) v0.6.7 ────────────────────────
    //
    // Decorative use only. The Eorzea font reproduces the in-game Eorzean
    // script glyphs. Used sparingly for section markers and the v1.0
    // brand wordmark accent. NEVER used for body text or anything
    // load-bearing — most users can't read it.

    /// <summary>Eorzea @ 13px — ornamental rune accent (decorative only).</summary>
    public IFontHandle? EorzeaRune { get; }

    // ── Construction ────────────────────────────────────────────────────

    public FontAtlasManager(IDalamudPluginInterface pi)
    {
        var atlas = pi.UiBuilder.FontAtlas;
        var dir   = Path.Combine(
            pi.AssemblyLocation.DirectoryName ?? string.Empty,
            "Assets", "Fonts");

        // Build each handle defensively — one bad file shouldn't kill the rest.

        // Track 1 — TlfTheme fonts (unchanged from v0.6.0).
        CinzelDisplay  = TryBuild(atlas, dir, "Cinzel-Regular.ttf",       32f, "CinzelDisplay");
        CinzelHeader   = TryBuild(atlas, dir, "Cinzel-Regular.ttf",       22f, "CinzelHeader");
        CinzelEmphasis = TryBuild(atlas, dir, "Cinzel-SemiBold.ttf",      16f, "CinzelEmphasis");
        GaramondBody   = TryBuild(atlas, dir, "EBGaramond-Regular.ttf",   15f, "GaramondBody");
        GaramondItalic = TryBuild(atlas, dir, "EBGaramond-Italic.ttf",    15f, "GaramondItalic");
        Pixel          = TryBuild(atlas, dir, "PressStart2P-Regular.ttf", 10f, "Pixel");
        PixelDisplay   = TryBuild(atlas, dir, "PressStart2P-Regular.ttf", 32f, "PixelDisplay");

        // Track 2 — TtChrome fonts (v0.6.7).
        // Filenames match Google Fonts' standard distribution + the
        // Eorzea.ttf shipped in TLF design handoff v0.1 assets.
        CormorantBody      = TryBuild(atlas, dir, "CormorantGaramond-Regular.ttf", 15f, "CormorantBody");
        CormorantItalic    = TryBuild(atlas, dir, "CormorantGaramond-Italic.ttf",  15f, "CormorantItalic");
        JetBrainsMonoBody  = TryBuild(atlas, dir, "JetBrainsMono-Regular.ttf",     11f, "JetBrainsMonoBody");
        JetBrainsMonoMeta  = TryBuild(atlas, dir, "JetBrainsMono-Bold.ttf",        10f, "JetBrainsMonoMeta");
        EorzeaRune         = TryBuild(atlas, dir, "Eorzea.ttf",                    13f, "EorzeaRune");

        DalamudServices.Log.Info(
            $"[FontAtlasManager v0.6.7] Loaded {CountLoaded()}/12 custom fonts from {dir}");
    }

    /// <summary>
    /// Build a single font handle from a .ttf file at <paramref name="fileName"/>.
    /// Returns null and logs on failure so missing assets degrade to default-font
    /// fallback instead of failing plugin load.
    /// </summary>
    private static IFontHandle? TryBuild(
        IFontAtlas atlas, string dir, string fileName, float sizePx, string logName)
    {
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path))
        {
            DalamudServices.Log.Warning(
                $"[FontAtlasManager v0.6.7] {logName}: file not found at {path} — " +
                "falling back to default font. Verify Assets/Fonts/ ships in the dropin.");
            return null;
        }

        try
        {
            return atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
                tk.AddFontFromFile(path, new SafeFontConfig { SizePx = sizePx })));
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex,
                $"[FontAtlasManager v0.6.7] {logName}: AddFontFromFile threw on {fileName}. " +
                "Default font will be used in its place. " +
                "Bug-report with /xllog excerpt + .ttf file size.");
            return null;
        }
    }

    private int CountLoaded() =>
        (CinzelDisplay     is null ? 0 : 1) +
        (CinzelHeader      is null ? 0 : 1) +
        (CinzelEmphasis    is null ? 0 : 1) +
        (GaramondBody      is null ? 0 : 1) +
        (GaramondItalic    is null ? 0 : 1) +
        (Pixel             is null ? 0 : 1) +
        (PixelDisplay      is null ? 0 : 1) +
        (CormorantBody     is null ? 0 : 1) +
        (CormorantItalic   is null ? 0 : 1) +
        (JetBrainsMonoBody is null ? 0 : 1) +
        (JetBrainsMonoMeta is null ? 0 : 1) +
        (EorzeaRune        is null ? 0 : 1);

    // ── Disposal ────────────────────────────────────────────────────────

    public void Dispose()
    {
        // Dispose handles in reverse construction order. Dalamud's atlas
        // itself is owned by UiBuilder; we only release our refcounts.
        EorzeaRune?.Dispose();
        JetBrainsMonoMeta?.Dispose();
        JetBrainsMonoBody?.Dispose();
        CormorantItalic?.Dispose();
        CormorantBody?.Dispose();
        PixelDisplay?.Dispose();
        Pixel?.Dispose();
        GaramondItalic?.Dispose();
        GaramondBody?.Dispose();
        CinzelEmphasis?.Dispose();
        CinzelHeader?.Dispose();
        CinzelDisplay?.Dispose();
    }
}
