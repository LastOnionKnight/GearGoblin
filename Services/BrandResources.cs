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

    /// <summary>Inline Tonberry Tactics lantern mark icon. Used in place of
    /// generic geometric glyphs in the UI.</summary>
    public IDalamudTextureWrap? LanternMark { get; private set; }

    public System.Collections.Generic.Dictionary<string, IDalamudTextureWrap?> JobStones { get; } = new();

    // --- Phase 3 Additions ---
    public IDalamudTextureWrap? OnionCrest { get; private set; }
    public IDalamudTextureWrap? DrkJob { get; private set; }
    public IDalamudTextureWrap? MateriaCrit { get; private set; }
    public IDalamudTextureWrap? MateriaDet { get; private set; }
    public IDalamudTextureWrap? MateriaDh { get; private set; }
    public IDalamudTextureWrap? MateriaPie { get; private set; }
    public IDalamudTextureWrap? MateriaSks { get; private set; }
    public IDalamudTextureWrap? MateriaSps { get; private set; }
    public IDalamudTextureWrap? MateriaTen { get; private set; }

    /// <summary>True if at least one asset loaded successfully. Callers can
    /// use this as a quick "do brand stuff or fall back to text?" check.</summary>
    public bool AnyLoaded => CircleLogo != null || RagsPortrait != null || RagsMini != null || LanternMark != null;

    public BrandResources()
    {
        // v0.6.5.2 — Defer the actual loads to the framework thread because
        // TextureProvider.GetFromFile(path).GetWrapOrEmpty() reads from the
        // render-thread ImGui/texture context, which only exists during a
        // framework tick. The BrandResources constructor runs synchronously
        // from Plugin's constructor at plugin load, which can be off the
        // framework thread depending on Dalamud's load sequence. Prior to
        // this fix, three "Not on main thread!" log warnings appeared on
        // startup (one per asset), all three loads returned null, and the
        // plugin silently fell back to text-only branding for the entire
        // session.
        //
        // RunOnFrameworkThread queues the load work onto the next render
        // tick. Properties (CircleLogo, RagsPortrait, RagsMini) start as
        // null and populate within a frame or two of plugin load. All
        // existing callers already null-check before drawing so they
        // remain safe during the brief pre-load window.
        try
        {
            DalamudServices.Framework.RunOnFrameworkThread(() =>
            {
                try
                {
                    CircleLogo   = TryLoad("circle-logo.png");
                    RagsPortrait = TryLoad("rags-portrait.png");
                    RagsMini     = TryLoad("rags-mini.png");
                    LanternMark  = TryLoad("lantern-mark.png");

                    OnionCrest   = TryLoad("onion-crest.png");
                    DrkJob       = TryLoad("DRK.png");
                    MateriaCrit  = TryLoad("materia/crit.png");
                    MateriaDet   = TryLoad("materia/det.png");
                    MateriaDh    = TryLoad("materia/dh.png");
                    MateriaPie   = TryLoad("materia/pie.png");
                    MateriaSks   = TryLoad("materia/sks.png");
                    MateriaSps   = TryLoad("materia/sps.png");
                    MateriaTen   = TryLoad("materia/ten.png");

                    if (AnyLoaded)
                    {
                        DalamudServices.Log.Info(
                            "BrandResources v0.6.5.2: brand artwork loaded (deferred to framework thread) · " +
                            $"circle-logo={CircleLogo != null} · " +
                            $"rags-portrait={RagsPortrait != null} · " +
                            $"rags-mini={RagsMini != null} · " +
                            $"lantern-mark={LanternMark != null}");
                    }
                    else
                    {
                        DalamudServices.Log.Warning(
                            "BrandResources v0.6.5.2: no brand artwork found. Plugin will " +
                            "fall back to text-only branding. Expected files under Assets/ " +
                            "in the plugin install directory; rebuild dropin if missing.");
                    }
                }
                catch (Exception ex)
                {
                    DalamudServices.Log.Warning(ex,
                        "BrandResources v0.6.5.2: deferred load threw on framework thread; " +
                        "all assets remain null and plugin falls back to text-only branding.");
                }
            });
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex,
                "BrandResources v0.6.5.2: failed to schedule deferred load. " +
                "Falling back to text-only branding.");
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
        LanternMark?.Dispose();

        OnionCrest?.Dispose();
        DrkJob?.Dispose();
        MateriaCrit?.Dispose();
        MateriaDet?.Dispose();
        MateriaDh?.Dispose();
        MateriaPie?.Dispose();
        MateriaSks?.Dispose();
        MateriaSps?.Dispose();
        MateriaTen?.Dispose();

        CircleLogo = null;
        RagsPortrait = null;
        RagsMini = null;
        LanternMark = null;

        OnionCrest = null;
        DrkJob = null;
        MateriaCrit = null;
        MateriaDet = null;
        MateriaDh = null;
        MateriaPie = null;
        MateriaSks = null;
        MateriaSps = null;
        MateriaTen = null;
    }
}
