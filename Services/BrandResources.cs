// Services/BrandResources.cs
//
// v0.4.7.1 — "Brand Convergence" release.
// ============================================================================
//
// Loads the brand artwork (circle-logo + rags-portrait + rags-mini) from the
// plugin's Assets/ directory into IDalamudTextureWrap handles that ImGui can
// consume directly via ImGui.Image(wrap.Handle, ...).
//
// The loader is defensive by design: any failure (missing file, texture
// provider hiccup, wrong format) returns a null wrap rather than throwing.
// Callers null-check and fall back to text. This keeps a broken asset from
// breaking the whole window.
//
// Lifecycle:
//   constructed once at plugin load, disposed by Plugin.Dispose().
// ============================================================================

using System;
using System.IO;
using Dalamud.Interface.Textures.TextureWraps;

namespace GearGoblin.Services;

public sealed class BrandResources : IDisposable
{
    /// <summary>Circular wordmark logo. Used in the About tab header.</summary>
    public IDalamudTextureWrap? CircleLogo { get; private set; }

    /// <summary>Full Refia hero portrait. Reserved for the About tab body
    /// and future v0.6.x hero slots.</summary>
    public IDalamudTextureWrap? RagsPortrait { get; private set; }

    /// <summary>Compact Refia avatar. Reserved for tab headers, footers,
    /// and the v0.6.x rail UI.</summary>
    public IDalamudTextureWrap? RagsMini { get; private set; }

    /// <summary>True if at least one asset loaded successfully. Callers can
    /// use this as a quick "do brand stuff or fall back to text?" check.</summary>
    public bool AnyLoaded => CircleLogo != null || RagsPortrait != null || RagsMini != null;

    public BrandResources()
    {
        CircleLogo   = TryLoad("circle-logo.png");
        RagsPortrait = TryLoad("rags-portrait.png");
        RagsMini     = TryLoad("rags-mini.png");

        if (AnyLoaded)
        {
            DalamudServices.Log.Info(
                "BrandResources v0.4.7.1: brand artwork loaded · " +
                $"circle-logo={CircleLogo != null} · " +
                $"rags-portrait={RagsPortrait != null} · " +
                $"rags-mini={RagsMini != null}");
        }
        else
        {
            DalamudServices.Log.Warning(
                "BrandResources v0.4.7.1: no brand artwork found. Plugin will " +
                "fall back to text-only branding. Expected files under Assets/ " +
                "in the plugin install directory; rebuild dropin if missing.");
        }
    }

    private static IDalamudTextureWrap? TryLoad(string filename)
    {
        try
        {
            var dir = DalamudServices.PluginInterface.AssemblyLocation.Directory?.FullName;
            if (dir == null)
            {
                DalamudServices.Log.Warning(
                    $"BrandResources: AssemblyLocation.Directory was null, can't resolve {filename}.");
                return null;
            }

            var path = Path.Combine(dir, "Assets", filename);
            if (!File.Exists(path))
            {
                DalamudServices.Log.Warning(
                    $"BrandResources: asset missing at {path}. " +
                    "Did the build copy the Assets/ folder to output?");
                return null;
            }

            // GetFromFile returns an ISharedImmediateTexture; GetWrapOrEmpty
            // gives a non-null wrap immediately (returns an empty placeholder
            // texture while the file finishes loading async).
            var wrap = DalamudServices.TextureProvider.GetFromFile(path).GetWrapOrEmpty();
            return wrap;
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex,
                $"BrandResources: exception loading {filename}; falling back to null.");
            return null;
        }
    }

    public void Dispose()
    {
        CircleLogo?.Dispose();
        RagsPortrait?.Dispose();
        RagsMini?.Dispose();
        CircleLogo = null;
        RagsPortrait = null;
        RagsMini = null;
    }
}
