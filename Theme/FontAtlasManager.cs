// Theme/FontAtlasManager.cs
//
// v0.6.0 — IFontAtlas Phase 2.
//
// Loads the five custom .ttf fonts bundled in Assets/Fonts/ and exposes
// each as an IFontHandle for the rest of the plugin to Push() onto the
// ImGui font stack. Matches the web app's Google-Fonts typography:
//
//   Cinzel             — display serif, headers and titles
//   Cinzel SemiBold    — display serif emphasis, "TONBERRY TACTICS" lockup
//   EB Garamond        — body serif, manifesto / credo / About prose
//   EB Garamond Italic — italic emphasis inside Garamond runs
//   Press Start 2P     — pixel font, version pills + eyebrow micro-labels
//
// All five .ttf files were converted from fontsource's woff distributions
// via fontTools.ttLib (see CHANGELOG v0.6.0 for the bootstrap recipe).
// They live in <PluginDir>/Assets/Fonts/ at runtime; csproj is configured
// to copy them with CopyToOutputDirectory=PreserveNewest.
//
// Font handle lifecycle:
//   - Created in the ctor via atlas.NewDelegateFontHandle(...) calls.
//   - Dalamud owns the underlying texture atlas; we own the handles and
//     dispose them in our own Dispose() before the atlas is torn down.
//   - Each handle's .Push() returns an IDisposable for `using` blocks.
//   - On load failure (missing file, malformed .ttf), the handle property
//     is left null and UI helpers fall back to the default ImGui font.
//     The plugin still loads and works; you just don't see the custom
//     typography. Errors are logged to /xllog for diagnosis.
//
// IMPORTANT: these fonts only apply to the plugin's own ImGui surfaces
// (the /tt MainWindow). The native CharacterStatus injection
// (StatusPanelInjector) is constrained to FFXIV's bundled SE font system
// — game-engine AtkTextNodes can't accept a TTF from a plugin atlas.

using System;
using System.IO;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;

namespace GearGoblin.Theme;

public sealed class FontAtlasManager : IDisposable
{
    // ── Display serif (Cinzel) ──────────────────────────────────────────

    /// <summary>Cinzel Regular @ 32px — for the "TONBERRY TACTICS" wordmark in About.</summary>
    public IFontHandle? CinzelDisplay { get; }

    /// <summary>Cinzel Regular @ 22px — for tab section headers and the player name line.</summary>
    public IFontHandle? CinzelHeader  { get; }

    /// <summary>Cinzel SemiBold @ 16px — for menu-box titles and eyebrow labels.</summary>
    public IFontHandle? CinzelEmphasis { get; }

    // ── Body serif (EB Garamond) ────────────────────────────────────────

    /// <summary>EB Garamond Regular @ 15px — manifesto / credo body prose.</summary>
    public IFontHandle? GaramondBody { get; }

    /// <summary>EB Garamond Italic @ 15px — italic emphasis inside Garamond runs.</summary>
    public IFontHandle? GaramondItalic { get; }

    // ── Pixel display (Press Start 2P) ──────────────────────────────────

    /// <summary>Press Start 2P @ 10px — version pills, parsed-status badges, micro-labels.</summary>
    public IFontHandle? Pixel { get; }

    /// <summary>Press Start 2P @ 32px - centered jobAbbr fallback glyph.</summary>
    public IFontHandle? PixelDisplay { get; }

    // ── Construction ────────────────────────────────────────────────────

    public FontAtlasManager(IDalamudPluginInterface pi)
    {
        var atlas = pi.UiBuilder.FontAtlas;
        var dir   = Path.Combine(
            pi.AssemblyLocation.DirectoryName ?? string.Empty,
            "Assets", "Fonts");

        // Build each handle defensively — one bad file shouldn't kill the rest.
        CinzelDisplay  = TryBuild(atlas, dir, "Cinzel-Regular.ttf",       32f, "CinzelDisplay");
        CinzelHeader   = TryBuild(atlas, dir, "Cinzel-Regular.ttf",       22f, "CinzelHeader");
        CinzelEmphasis = TryBuild(atlas, dir, "Cinzel-SemiBold.ttf",      16f, "CinzelEmphasis");
        GaramondBody   = TryBuild(atlas, dir, "EBGaramond-Regular.ttf",   15f, "GaramondBody");
        GaramondItalic = TryBuild(atlas, dir, "EBGaramond-Italic.ttf",    15f, "GaramondItalic");
        Pixel          = TryBuild(atlas, dir, "PressStart2P-Regular.ttf", 10f, "Pixel");
        PixelDisplay   = TryBuild(atlas, dir, "PressStart2P-Regular.ttf", 32f, "PixelDisplay");

        DalamudServices.Log.Info(
            $"[FontAtlasManager v0.6.0] Loaded {CountLoaded()}/7 custom fonts from {dir}");
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
                $"[FontAtlasManager v0.6.0] {logName}: file not found at {path} — " +
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
                $"[FontAtlasManager v0.6.0] {logName}: AddFontFromFile threw on {fileName}. " +
                "Default font will be used in its place. " +
                "Bug-report with /xllog excerpt + .ttf file size.");
            return null;
        }
    }

    private int CountLoaded() =>
        (CinzelDisplay  is null ? 0 : 1) +
        (CinzelHeader   is null ? 0 : 1) +
        (CinzelEmphasis is null ? 0 : 1) +
        (GaramondBody   is null ? 0 : 1) +
        (GaramondItalic is null ? 0 : 1) +
        (Pixel          is null ? 0 : 1) +
        (PixelDisplay   is null ? 0 : 1);

    // ── Disposal ────────────────────────────────────────────────────────

    public void Dispose()
    {
        // Dispose handles in reverse construction order. Dalamud's atlas
        // itself is owned by UiBuilder; we only release our refcounts.
        PixelDisplay?.Dispose();
        Pixel?.Dispose();
        GaramondItalic?.Dispose();
        GaramondBody?.Dispose();
        CinzelEmphasis?.Dispose();
        CinzelHeader?.Dispose();
        CinzelDisplay?.Dispose();
    }
}
