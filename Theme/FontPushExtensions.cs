// Theme/FontPushExtensions.cs
//
// v0.6.0 — small helper that makes IFontHandle.Push() null-safe so call
// sites can wrap a section in a custom font without checking whether the
// font actually loaded.
//
// Usage:
//
//     using (plugin.Fonts.CinzelDisplay.PushOrNull())
//     {
//         ImGui.TextColored(TlfTheme.GoldBright, "TONBERRY TACTICS");
//     }
//
// When the handle is null (load failed; FontAtlasManager logged a warning),
// PushOrNull() returns a no-op IDisposable and the text renders in the
// default ImGui font. The block stays structurally identical either way.

using System;
using Dalamud.Interface.ManagedFontAtlas;

namespace GearGoblin.Theme;

public static class FontPushExtensions
{
    public static IDisposable PushOrNull(this IFontHandle? handle) =>
        handle is null ? NullDisposable.Instance : handle.Push();

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() { /* no-op */ }
    }
}
