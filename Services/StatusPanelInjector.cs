// Services/StatusPanelInjector.cs
//
// v0.4.6 — "Coexistence" release.
// ============================================================================
//
// Inject GearGoblin's derived stat data and Materia Advisor directly into
// FFXIV's native CharacterStatus addon. v0.4.6 reframes the v0.4.5 model:
// instead of treating CPR detection as a fallback path, we treat coexistence
// as the deployment. GG runs alongside CPR — CPR brings the substat
// derivations, GG brings the Materia Advisor, real GCD (when CPR doesn't
// supply a job-aware variant), breakpoint hints, and the /goblininfo
// diagnostic surface. The override toggle for users who want GG-only is
// still here, but the default mode is friendly cohabitation.
//
// Lineage:
//   v0.4.0  first injection — breakpoint hints + GCD + Materia Advisor
//   v0.4.1  /goblinexport + Dalamud SDK compat (AddonEventData signature)
//   v0.4.2  4 bug fixes: footer off-panel, missing Crit hint, row overlap,
//           empty-advisor blank rows; advisor consolidated 6→4 rows
//   v0.4.5  Full CPR-equivalent derivations, CPR coexistence detect-and-defer,
//           role-gated Tenacity/Piety rows, per-section toggles
//   v0.4.6  THIS RELEASE
//           - FIX: Materia Advisor now visible when CPR coexists. AddStatRow
//             grows parent component height but never grew the outer addon's
//             RootNode height, so the advisor rows (in the gear section, last
//             in the panel) were rendered past the addon's visible clip. Now
//             we track totalInjectedHeight across every AddStatRow call and
//             grow characterStatusPtr->RootNode->Height by that amount after
//             InjectAllRows completes.
//           - Instrumented advisor logging — log lines now confirm what
//             injected (header / rec1-3 / total height) and what updated
//             (recommendation count / empty-state / errored). No more
//             aspirational "will inject normally" messages.
//           - DiagnosticState exposure via public properties for the new
//             Diagnostics tab and /goblininfo slash command.
//           - ForceReinject() public method for the diagnostics-tab button.
//
// Injection patterns (AddStatRow, node walks, hardcoded section IDs) are
// adapted from CharacterPanelRefined (MIT). See LICENSES/CharacterPanelRefined-MIT.txt.
//
// Lifecycle:
//   PostSetup            → InjectAllRows (every time the addon opens)
//                          then GrowAddonHeight(totalInjectedHeight)
//   PostRequestedUpdate  → UpdateAllValues (every game tick while open)
//   PreFinalize          → cleanup pointers; addon teardown frees memory

using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Addon.Events;
using Dalamud.Game.Addon.Events.EventDataTypes;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GearGoblin.Materia;
using GearGoblin.Util;

namespace GearGoblin.Services;

public sealed unsafe class StatusPanelInjector : IStatusPanelInjector
{
    // ── Node IDs verified by CharacterPanelRefined in-game inspection ──
    private const uint AttributesNodeId          = 26;
    private const uint OffensivePropertiesNodeId = 36;
    private const uint DefensivePropertiesNodeId = 44;
    private const uint PhysicalPropertiesNodeId  = 51;
    private const uint MentalPropertiesNodeId    = 58;
    private const uint GearNodeId                = 80;
    private const uint RolePropertiesNodeId      = 86;

    private const string AddonName = "CharacterStatus";

    // v0.6.0 — Palette refresh on the native CharacterStatus injection.
    // The plugin's own ImGui window gets custom Cinzel/Garamond/Pixel fonts
    // via IFontAtlas Phase 2, but the in-game text nodes are stuck on
    // FFXIV's bundled SE font (AtkTextNode can't accept plugin atlases).
    // What we CAN tune is byte color — so derived rows pick up TLF's
    // FrostSoft body tone and the Materia Advisor accent shifts from
    // TLF Gold (matched the v0.4.7 chrome) to LanternHot, which reads
    // brighter against the native panel's blue background.
    //
    // ByteColor RGB values are mirrored from Theme/TlfTheme.cs:
    //   FrostSoft  = #C2C5D8  (was #A0A0A0 neutral gray)
    //   LanternHot = #FFCE5E  (was #C9B27E TLF Gold)

    private static readonly ByteColor InjectedRowColor =
        new() { A = 0xFF, R = 0xC2, G = 0xC5, B = 0xD8 };

    private static readonly ByteColor AdvisorAccentColor =
        new() { A = 0xFF, R = 0xFF, G = 0xCE, B = 0x5E };

    private readonly Plugin plugin;
    private AtkUnitBase* characterStatusPtr;
    private bool registered;
    private bool firstInjectLogged;

    // Compact derived-stat value cells (one per substat).
    private AtkTextNode* critCompactValue;
    private AtkTextNode* detCompactValue;
    private AtkTextNode* dhCompactValue;

    // Speed section: GCD time row + combined breakpoint+damage row.
    private AtkTextNode* speedGcdValue;
    private AtkTextNode* speedCompactValue;

    // Role-gated rows.
    private AtkTextNode* tenacityCompactValue;
    private AtkTextNode* pietyCompactValue;

    // Materia Advisor section pointers (v0.4.2 consolidated layout).
    private AtkTextNode* advisorHeader;
    private AtkTextNode* advisorRec1;
    private AtkTextNode* advisorRec2;
    private AtkTextNode* advisorRec3;

    private IAddonEventHandle? footerClickHandle;
    private bool cprDetectedActive;

    // ── v0.4.6 instrumentation state ────────────────────────────────────
    // Total height (in pixels) added across this addon's lifetime by
    // AddStatRow. Used to grow the addon's outer RootNode after injection
    // so trailing rows (advisor section) don't fall past the visible clip.
    // Reset to 0 on every PostSetup since the addon is rebuilt fresh.
    private ushort totalInjectedHeight;

    // Snapshot data exposed to the Diagnostics tab and /goblininfo command.
    private DateTime lastInjectTime;
    private DateTime lastUpdateTime;
    private string   lastInjectResult     = "(not yet injected)";
    private int      lastAdvisorRecCount;
    private bool     lastAdvisorEmptyState;
    private bool     lastAdvisorErrored;
    private bool     advisorSectionPresent;

    /// <summary>
    /// v0.4.6 diagnostic snapshot for the UI tab and /goblininfo command.
    /// Plain immutable record — read at draw time, no locking needed since
    /// the injector only mutates from the game thread.
    /// </summary>
    public readonly record struct DiagnosticSnapshot(
        bool     PanelAttached,
        bool     CprDetected,
        bool     DerivationsEnabled,
        bool     AdvisorSectionPresent,
        int      AdvisorRecCount,
        bool     AdvisorEmptyState,
        bool     AdvisorErrored,
        ushort   InjectedHeightPx,
        DateTime LastInjectTime,
        DateTime LastUpdateTime,
        string   LastInjectResult);

    public DiagnosticSnapshot GetDiagnostics() => new(
        PanelAttached:         characterStatusPtr != null,
        CprDetected:           cprDetectedActive,
        DerivationsEnabled:    WillInjectDerivations(),
        AdvisorSectionPresent: advisorSectionPresent,
        AdvisorRecCount:       lastAdvisorRecCount,
        AdvisorEmptyState:     lastAdvisorEmptyState,
        AdvisorErrored:        lastAdvisorErrored,
        InjectedHeightPx:      totalInjectedHeight,
        LastInjectTime:        lastInjectTime,
        LastUpdateTime:        lastUpdateTime,
        LastInjectResult:      lastInjectResult);

    /// <summary>
    /// v0.4.6: force the advisor + derived rows to recompute their text
    /// without reopening the Character window. Used by the Diagnostics tab
    /// "Force Reinject" button. We deliberately do NOT re-run AddStatRow —
    /// that would duplicate the cloned nodes — only the value-update pass.
    /// </summary>
    public void ForceReinject()
    {
        if (characterStatusPtr == null)
        {
            DalamudServices.Log.Info("ForceReinject: Character panel not attached; nothing to do.");
            return;
        }
        try
        {
            UpdateAllValues();
            DalamudServices.Log.Info("ForceReinject: UpdateAllValues completed.");
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "ForceReinject: UpdateAllValues threw.");
        }
    }

    // ── Construction / disposal ─────────────────────────────────────────

    public StatusPanelInjector(Plugin plugin)
    {
        this.plugin = plugin;

        if (!plugin.Configuration.EnableNativeStatPanel)
        {
            DalamudServices.Log.Info(
                "StatusPanelInjector: disabled in configuration; not registering listeners.");
            return;
        }

        DalamudServices.AddonLifecycle.RegisterListener(
            AddonEvent.PostSetup,           AddonName, OnPostSetup);
        DalamudServices.AddonLifecycle.RegisterListener(
            AddonEvent.PreFinalize,         AddonName, OnPreFinalize);
        DalamudServices.AddonLifecycle.RegisterListener(
            AddonEvent.PostRequestedUpdate, AddonName, OnRequestedUpdate);
        registered = true;

        DalamudServices.Log.Info(
            "StatusPanelInjector v0.4.6: registered AddonLifecycle listeners for CharacterStatus.");

        _ = DalamudServices.Framework.RunOnFrameworkThread(() =>
        {
            try
            {
                BootInjectIfPanelOpen();
            }
            catch (Exception ex)
            {
                DalamudServices.Log.Warning(ex, "StatusPanelInjector: boot-time inject guard threw.");
            }
        });
    }

    public void Dispose()
    {
        if (footerClickHandle != null)
        {
            try { DalamudServices.AddonEventManager.RemoveEvent(footerClickHandle); }
            catch (Exception ex) { DalamudServices.Log.Warning(ex, "RemoveEvent threw on dispose."); }
            footerClickHandle = null;
        }

        if (registered)
        {
            DalamudServices.AddonLifecycle.UnregisterListener(OnPostSetup);
            DalamudServices.AddonLifecycle.UnregisterListener(OnPreFinalize);
            DalamudServices.AddonLifecycle.UnregisterListener(OnRequestedUpdate);
        }

        ClearPointers();
    }

    // ── Lifecycle handlers ──────────────────────────────────────────────

    private void OnPostSetup(AddonEvent type, AddonArgs args)
        => RunInjection((AtkUnitBase*)args.Addon.Address);

    // v0.6.7.6: boot guard. If the Character panel is already open when this
    // injector is constructed (plugin hot-reloaded while the window was up),
    // OnPostSetup never fires for it, so nothing injects until close+reopen.
    private void BootInjectIfPanelOpen()
    {
        if (characterStatusPtr != null) return;
        var addon = DalamudServices.GameGui.GetAddonByName<AtkUnitBase>(AddonName, 1);
        if (addon == null) return;
        DalamudServices.Log.Info(
            "StatusPanelInjector: Character panel already open at injector load; running boot-time inject.");
        RunInjection(addon);
    }

    private void RunInjection(AtkUnitBase* addon)
    {
        try
        {
            characterStatusPtr = addon;

            // v0.4.6: reset per-open instrumentation state. The addon is
            // torn down (PreFinalize) and rebuilt between opens, so any
            // height we grew last time is gone with the old node tree.
            totalInjectedHeight   = 0;
            advisorSectionPresent = false;
            lastAdvisorErrored    = false;

            cprDetectedActive = CprDetection.IsCprActive();
            if (cprDetectedActive && !plugin.Configuration.ForceDerivationsOverCpr)
            {
                DalamudServices.Log.Info(
                    "StatusPanelInjector v0.4.6: CharacterPanelRefined detected as active; " +
                    "skipping derived-stat injection (set ForceDerivationsOverCpr=true to override). " +
                    "Materia Advisor, breakpoint hints, and real GCD will still inject.");
            }

            InjectAllRows();

            // v0.4.6 critical fix: grow the addon's outer RootNode by the
            // total height we added inside parent components. Without this
            // step, rows we appended to the gear section (the last section
            // in the panel) fall past the addon's visible clip and look
            // like they never injected. See header doc for full rationale.
            if (totalInjectedHeight > 0)
            {
                GrowAddonHeight(totalInjectedHeight);
            }

            UpdateAllValues();

            lastInjectTime   = DateTime.UtcNow;
            lastInjectResult = $"OK · CPR={cprDetectedActive} · derivations={WillInjectDerivations()} · " +
                               $"advisor={advisorSectionPresent} · grewBy={totalInjectedHeight}px";

            if (!firstInjectLogged)
            {
                firstInjectLogged = true;
                DalamudServices.Log.Info(
                    "StatusPanelInjector v0.4.6: first inject complete. " +
                    $"CPR active: {cprDetectedActive}. " +
                    $"Derivations enabled: {WillInjectDerivations()}. " +
                    $"Advisor section injected: {advisorSectionPresent}. " +
                    $"Outer addon grew by {totalInjectedHeight}px.");
            }
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "StatusPanelInjector: PostSetup injection failed.");
            lastInjectResult = $"FAILED · {ex.GetType().Name}: {ex.Message}";
            ClearPointers();
        }
    }

    private void OnPreFinalize(AddonEvent type, AddonArgs args)
    {
        if (footerClickHandle != null)
        {
            try { DalamudServices.AddonEventManager.RemoveEvent(footerClickHandle); }
            catch { /* addon may already be partially torn down */ }
            footerClickHandle = null;
        }
        ClearPointers();
    }

    private void OnRequestedUpdate(AddonEvent type, AddonArgs args)
    {
        if (characterStatusPtr == null) return;
        try
        {
            UpdateAllValues();
            lastUpdateTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "StatusPanelInjector: RequestedUpdate failed.");
        }
    }

    private bool WillInjectDerivations()
    {
        if (!plugin.Configuration.EnableDerivedStatInjection) return false;
        if (cprDetectedActive && !plugin.Configuration.ForceDerivationsOverCpr) return false;
        return true;
    }

    // ── Injection ───────────────────────────────────────────────────────

    private void InjectAllRows()
    {
        if (characterStatusPtr == null) return;

        if (WillInjectDerivations())
        {
            InjectOffensiveDerivations();
            InjectSpeedSection();
            InjectRoleDerivations();
        }
        InjectAdvisorSection();
    }

    /// <summary>
    /// Inject a compact derived-stat row under each of Crit / Det / DH.
    /// One row per substat, label cell empty, value cell carries the full
    /// "chance · damage · DI · +N→tier" compact string.
    /// </summary>
    private void InjectOffensiveDerivations()
    {
        var offensive = characterStatusPtr->UldManager.SearchNodeById(OffensivePropertiesNodeId);
        if (offensive == null)
        {
            DalamudServices.Log.Warning(
                "StatusPanelInjector v0.4.6: OffensiveProperties node not found; derivations skipped.");
            return;
        }

        AtkComponentNode* critComponent = null;
        AtkComponentNode* detComponent  = null;
        AtkComponentNode* dhComponent   = null;

        var child = offensive->ChildNode;
        int safety = 0;
        while (child != null && safety++ < 32)
        {
            if (child->Type == NodeType.Component)
            {
                var component = (AtkComponentNode*)child;
                var label     = GetComponentLabelText(component);
                if (!string.IsNullOrEmpty(label))
                {
                    if      (label.StartsWith("Critical Hit"))   critComponent = component;
                    else if (label.StartsWith("Determination"))  detComponent  = component;
                    else if (label.StartsWith("Direct Hit"))     dhComponent   = component;
                }
            }
            child = child->NextSiblingNode;
        }

        if (critComponent == null || detComponent == null || dhComponent == null)
        {
            DalamudServices.Log.Warning(
                "StatusPanelInjector v0.4.6: could not identify all three offensive substat rows " +
                $"by label (crit={(critComponent != null)} det={(detComponent != null)} dh={(dhComponent != null)}). " +
                "Some derivation rows will be missing.");
        }

        if (critComponent != null && plugin.Configuration.ShowCritDerivations)
            critCompactValue = AddStatRow(critComponent, "");
        if (detComponent != null && plugin.Configuration.ShowDetDerivations)
            detCompactValue = AddStatRow(detComponent, "");
        if (dhComponent != null && plugin.Configuration.ShowDhDerivations)
            dhCompactValue = AddStatRow(dhComponent, "");
    }

    private void InjectSpeedSection()
    {
        if (!plugin.Configuration.ShowSpeedDerivations) return;

        var snap = StatReader.ReadCurrent();
        if (snap == null) return;
        var profile = JobProfiles.All.GetValueOrDefault(snap.Value.JobId);
        if (profile == null) return;

        var useSpellSpeed = profile.Role == Role.MagicalRangedDps || profile.Role == Role.Healer;
        var sectionId    = useSpellSpeed ? MentalPropertiesNodeId : PhysicalPropertiesNodeId;

        var section = characterStatusPtr->UldManager.SearchNodeById(sectionId);
        if (section == null || section->ChildNode == null)
        {
            DalamudServices.Log.Warning(
                $"StatusPanelInjector v0.4.6: speed section ({sectionId}) not found; speed rows skipped.");
            return;
        }

        AtkComponentNode* speedComponent = null;
        var child = section->ChildNode;
        int safety = 0;
        var wantedLabel = useSpellSpeed ? "Spell Speed" : "Skill Speed";
        while (child != null && safety++ < 32)
        {
            if (child->Type == NodeType.Component)
            {
                var component = (AtkComponentNode*)child;
                var label     = GetComponentLabelText(component);
                if (!string.IsNullOrEmpty(label) && label.StartsWith(wantedLabel))
                {
                    speedComponent = component;
                    break;
                }
            }
            child = child->NextSiblingNode;
        }

        if (speedComponent == null)
        {
            DalamudServices.Log.Warning(
                $"StatusPanelInjector v0.4.6: '{wantedLabel}' component not found; speed rows skipped.");
            return;
        }

        speedGcdValue     = AddStatRow(speedComponent, "GCD (real):");
        speedCompactValue = AddStatRow(speedComponent, "");
    }

    private void InjectRoleDerivations()
    {
        var snap = StatReader.ReadCurrent();
        if (snap == null) return;
        var profile = JobProfiles.All.GetValueOrDefault(snap.Value.JobId);
        if (profile == null) return;

        var injectTenacity = profile.Role == Role.Tank   && plugin.Configuration.ShowTenacityRow;
        var injectPiety    = profile.Role == Role.Healer && plugin.Configuration.ShowPietyRow;
        if (!injectTenacity && !injectPiety) return;

        var roleSection = characterStatusPtr->UldManager.SearchNodeById(RolePropertiesNodeId);
        if (roleSection == null || roleSection->ChildNode == null)
        {
            DalamudServices.Log.Warning(
                "StatusPanelInjector v0.4.6: Role Properties section not found; role rows skipped.");
            return;
        }

        AtkComponentNode* targetComponent = null;
        var wantedLabel = injectTenacity ? "Tenacity" : "Piety";

        var child = roleSection->ChildNode;
        int safety = 0;
        while (child != null && safety++ < 32)
        {
            if (child->Type == NodeType.Component)
            {
                var component = (AtkComponentNode*)child;
                var label     = GetComponentLabelText(component);
                if (!string.IsNullOrEmpty(label) && label.StartsWith(wantedLabel))
                {
                    targetComponent = component;
                    break;
                }
            }
            child = child->NextSiblingNode;
        }

        if (targetComponent == null)
        {
            DalamudServices.Log.Warning(
                $"StatusPanelInjector v0.4.6: '{wantedLabel}' component not found in Role Properties.");
            return;
        }

        if (injectTenacity)
            tenacityCompactValue = AddStatRow(targetComponent, "");
        else
            pietyCompactValue    = AddStatRow(targetComponent, "");
    }

    private void InjectAdvisorSection()
    {
        var gear = characterStatusPtr->UldManager.SearchNodeById(GearNodeId);
        if (gear == null || gear->ChildNode == null)
        {
            DalamudServices.Log.Warning(
                "StatusPanelInjector v0.4.6: Gear node not found; Materia Advisor skipped.");
            advisorSectionPresent = false;
            return;
        }

        var avgIlvlComponent = (AtkComponentNode*)gear->ChildNode;
        var heightBeforeAdvisor = totalInjectedHeight;

        // v0.6.5.3 — Advisor rows inject into the Gear / Average Item Level
        // component. Each row passes expandCollisionNode: false so the original
        // ILVL text node's bounds aren't stretched onto our injected rows.
        // v0.6.5.2 attempted to fix this by pre-padding the parent + collision
        // node by 20px before the first AddStatRow — that approach grew the
        // collision node which is exactly the thing the ghost-text bug needed
        // us to STOP doing. The pre-pad has been removed; the parameter on
        // each AddStatRow call is the correct intervention, matching the
        // upstream CharacterPanelRefined pattern (see AddStatRow comment).
        // v0.6.6 — restored the em-dashed label. The v0.6.6 H6-A test
        // (shorten label to "Materia Advisor") reduced the ghost text
        // visibly but didn't eliminate it, which proved the overflow
        // was coming from the NUMBER cell, not the label. With the
        // actual root cause now fixed in UpdateAdvisor (see SetText
        // call further down — pill text shortened from the long
        // "{crit}c · {warn}w · {empty}e   ▶ /tt" format to just
        // "▶ /tt" to match CharacterPanelRefined's short-number-cell
        // discipline), the label aesthetic returns to the original
        // em-dashed form.
        advisorHeader = AddStatRow(avgIlvlComponent, "── Materia Advisor ──", expandCollisionNode: false);
        advisorRec1   = AddStatRow(avgIlvlComponent, "",                        expandCollisionNode: false);
        advisorRec2   = AddStatRow(avgIlvlComponent, "",                        expandCollisionNode: false);
        advisorRec3   = AddStatRow(avgIlvlComponent, "",                        expandCollisionNode: false);

        var advisorHeightAdded = totalInjectedHeight - heightBeforeAdvisor;
        advisorSectionPresent =
            advisorHeader != null && advisorRec1 != null
            && advisorRec2 != null && advisorRec3 != null;

        // v0.4.6: replace the v0.4.5 aspirational log with a real status line
        // that records exactly what happened. This is the line that should
        // have existed in v0.4.5 — its absence is why we missed the bug.
        DalamudServices.Log.Info(
            $"StatusPanelInjector v0.4.6: Materia Advisor inject attempt. " +
            $"Rows OK: header={(advisorHeader != null)} rec1={(advisorRec1 != null)} " +
            $"rec2={(advisorRec2 != null)} rec3={(advisorRec3 != null)}. " +
            $"Height added: {advisorHeightAdded}px. " +
            $"Section present: {advisorSectionPresent}.");

        if (advisorHeader != null) advisorHeader->TextColor = AdvisorAccentColor;

        if (advisorHeader != null)
        {
            var headerNode = (AtkResNode*)advisorHeader;
            headerNode->NodeFlags |=
                NodeFlags.EmitsEvents | NodeFlags.RespondToMouse | NodeFlags.HasCollision;

            footerClickHandle = DalamudServices.AddonEventManager.AddEvent(
                (nint)characterStatusPtr,
                (nint)headerNode,
                AddonEventType.MouseClick,
                OnAdvisorHeaderClick);

            if (footerClickHandle == null)
                DalamudServices.Log.Warning(
                    "StatusPanelInjector v0.4.6: AddEvent returned null; advisor header will be non-interactive.");
        }
    }

    /// <summary>
    /// Read the label TextNode contents from a stat-row component. Returns
    /// null if the component's internal layout doesn't match the expected
    /// (collisionNode, numberNode, labelNode) sibling chain.
    /// </summary>
    private static string? GetComponentLabelText(AtkComponentNode* component)
    {
        if (component == null || component->Component == null) return null;
        var root = component->Component->UldManager.RootNode;
        if (root == null) return null;
        var number = (AtkTextNode*)root->PrevSiblingNode;
        if (number == null) return null;
        var label = (AtkTextNode*)number->AtkResNode.PrevSiblingNode;
        if (label == null) return null;

        try { return label->NodeText.ToString(); }
        catch { return null; }
    }

    // ── Update ──────────────────────────────────────────────────────────

    private void UpdateAllValues()
    {
        var snap = StatReader.ReadCurrent();
        if (snap == null) return;
        var s = snap.Value;

        var mod = LevelTable.Get(s.Level);
        var profile = JobProfiles.All.GetValueOrDefault(s.JobId);
        if (profile == null) return;

        UpdateOffensiveDerivations(s, mod);
        UpdateSpeedSection(s, mod, profile);
        UpdateRoleDerivations(s, mod, profile);
        UpdateAdvisor(s, mod, profile);
    }

    private void UpdateOffensiveDerivations(StatSnapshot s, in LevelMod mod)
    {
        if (critCompactValue != null)
        {
            var rate    = Formulas.CritRate(s.Crit, mod);
            var derived = DerivedStatFormatter.CritCompact(s.Crit, mod);
            var delta   = rate.NextTier - s.Crit;
            var hint    = delta > 0 ? $"+{delta}→tier" : "at cap";
            critCompactValue->SetText($"{derived} · {hint}");
        }
        if (detCompactValue != null)
        {
            var bd      = Formulas.Determination(s.Det, mod);
            var derived = DerivedStatFormatter.DetCompact(s.Det, mod);
            var delta   = bd.NextTier - s.Det;
            var hint    = delta > 0 ? $"+{delta}→tier" : "at cap";
            detCompactValue->SetText($"{derived} · {hint}");
        }
        if (dhCompactValue != null)
        {
            var bd      = Formulas.DirectHit(s.DH, mod);
            var derived = DerivedStatFormatter.DhCompact(s.DH, mod);
            var delta   = bd.NextTier - s.DH;
            var hint    = delta > 0 ? $"+{delta}→tier" : "at cap";
            dhCompactValue->SetText($"{derived} · {hint}");
        }
    }

    private void UpdateSpeedSection(StatSnapshot s, in LevelMod mod, JobProfile profile)
    {
        if (speedGcdValue == null && speedCompactValue == null) return;

        var useSpellSpeed = profile.Role == Role.MagicalRangedDps || profile.Role == Role.Healer;
        var speed = useSpellSpeed ? s.SpS : s.SkS;

        if (speedGcdValue != null)
        {
            var gcd = Formulas.GcdFromSpeed(speed, mod);
            speedGcdValue->SetText($"{gcd:0.00}s");
        }

        if (speedCompactValue != null)
        {
            var bd    = Formulas.SpeedDamage(speed, mod);
            var dmg   = DerivedStatFormatter.SpeedDamage(speed, mod);
            var delta = bd.NextTier - speed;
            var hint  = delta > 0 ? $"+{delta}→tier" : "at cap";
            speedCompactValue->SetText($"{dmg} · {hint}");
        }
    }

    private void UpdateRoleDerivations(StatSnapshot s, in LevelMod mod, JobProfile profile)
    {
        if (tenacityCompactValue != null && profile.Role == Role.Tank)
        {
            tenacityCompactValue->SetText(DerivedStatFormatter.TenacityCompact(s.Ten, mod));
        }
        if (pietyCompactValue != null && profile.Role == Role.Healer)
        {
            pietyCompactValue->SetText(DerivedStatFormatter.PietyMpPerTick(s.Pie, mod));
        }
    }

    private void UpdateAdvisor(StatSnapshot s, in LevelMod mod, JobProfile profile)
    {
        if (advisorRec1 == null) return;

        try
        {
            var equipped = plugin.Inventory.ReadEquipped();
            var pieces = equipped.Select(MeldSlotsBuilder.FromEquipped).ToList();
            if (pieces.Count == 0)
            {
                ClearAdvisorRows("(no gear)");
                lastAdvisorRecCount   = 0;
                lastAdvisorEmptyState = true;
                lastAdvisorErrored    = false;
                return;
            }

            var result = MeldOptimizer.Optimize(pieces, s, mod, profile, WeightMode.PureMath);

            var candidates = new List<string>();
            var topAudits = result.Audits
                .Where(a => a.Severity is AuditSeverity.Critical or AuditSeverity.Warning)
                .Where(a => a.SuggestedReplacement is not null)
                .OrderByDescending(a => a.GainIfReplaced)
                .Take(3);
            foreach (var audit in topAudits)
            {
                var slot = audit.Piece;
                var idx  = audit.SlotIndex + 1;
                var repl = audit.SuggestedReplacement!.Value.Display();
                candidates.Add($"{slot} #{idx} → {repl}");
            }
            if (candidates.Count < 3)
            {
                var topPlans = result.PlanRecommendations
                    .OrderByDescending(r => r.ScoreGain)
                    .Take(3 - candidates.Count);
                foreach (var rec in topPlans)
                {
                    var slot = rec.Piece;
                    var idx  = rec.SlotIndex + 1;
                    var mat  = rec.Materia.Display();
                    candidates.Add($"{slot} #{idx} ← {mat}");
                }
            }

            var crit  = result.Audits.Count(a => a.Severity == AuditSeverity.Critical);
            var warn  = result.Audits.Count(a => a.Severity == AuditSeverity.Warning);
            var empty = result.PlanRecommendations.Count;

            // v0.4.6 instrumentation: track recommendation count + empty-state
            // for the Diagnostics tab and /goblininfo command. Verbose log
            // line confirms what got rendered to the panel.
            lastAdvisorRecCount   = candidates.Count;
            lastAdvisorEmptyState = candidates.Count == 0;
            lastAdvisorErrored    = false;
            DalamudServices.Log.Verbose(
                $"StatusPanelInjector v0.4.6: Materia Advisor updated. " +
                $"Pieces: {pieces.Count}. Audits: crit={crit} warn={warn}. " +
                $"PlanRecs: {result.PlanRecommendations.Count}. " +
                $"Rendered: {candidates.Count} candidate(s)" +
                (candidates.Count == 0 ? " — empty-state row shown." : "."));

            if (candidates.Count == 0)
            {
                SetAdvisorRow(advisorRec1, "", "All guaranteed slots filled · no upgrades suggested");
                SetAdvisorRow(advisorRec2, "", "");
                SetAdvisorRow(advisorRec3, "", "");
            }
            else
            {
                SetAdvisorRow(advisorRec1, "", candidates.ElementAtOrDefault(0) ?? "");
                SetAdvisorRow(advisorRec2, "", candidates.ElementAtOrDefault(1) ?? "");
                SetAdvisorRow(advisorRec3, "", candidates.ElementAtOrDefault(2) ?? "");
            }

            if (advisorHeader != null)
            {
                // v0.6.6 — match CharacterPanelRefined's short-number-cell
                // discipline. The cloned number cell here inherits "780"-sized
                // geometry from the original ILVL value cell (~30px wide,
                // right-aligned text). The previous text format
                // `{crit}c · {warn}w · {empty}e   ▶ /tt` is ~17 characters
                // wide; right-aligned, it overflows leftward into the label
                // cell's render zone, producing BUG-001's visible ghost
                // pattern. CPR's analogous ilvlSync injection only ever puts
                // short numeric values in the cloned cell (e.g. "660",
                // "12.4%") — values that fit the inherited geometry. Mirror
                // that approach: just the command hint, 5 chars, fits.
                // Audit counts (crit/warn/empty) are not lost — they remain
                // visible in the rec rows injected immediately below this
                // header (the empty-state row renders "All guaranteed slots
                // filled · no upgrades suggested"; the with-audits rows
                // render the individual slot recommendations).
                advisorHeader->SetText("");
            }
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "StatusPanelInjector v0.4.6: UpdateAdvisor threw.");
            ClearAdvisorRows("(advisor error)");
            lastAdvisorErrored = true;
        }
    }

    private void SetAdvisorRow(AtkTextNode* numberNode, string numberText, string? labelText = null)
    {
        if (numberNode == null) return;
        numberNode->SetText(numberText);

        if (labelText != null)
        {
            var prevSibling = numberNode->AtkResNode.PrevSiblingNode;
            if (prevSibling != null && prevSibling->Type == NodeType.Text)
            {
                ((AtkTextNode*)prevSibling)->SetText(labelText);
            }
        }
    }

    private void ClearAdvisorRows(string placeholder)
    {
        SetAdvisorRow(advisorRec1, "", placeholder);
        SetAdvisorRow(advisorRec2, "", "");
        SetAdvisorRow(advisorRec3, "", "");
        if (advisorHeader != null) advisorHeader->SetText("");
    }

    // ── Header click → invoke /goblin ───────────────────────────────────

    private void OnAdvisorHeaderClick(AddonEventType type, AddonEventData data)
    {
        try
        {
            DalamudServices.CommandManager.ProcessCommand("/tt");
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "StatusPanelInjector v0.4.6: /tt invoke failed.");
        }
    }

    // ── AddStatRow primitive (adapted from CPR, MIT) ────────────────────

    /// <summary>
    /// Clone the existing label+number node pair beneath <paramref name="parentNode"/>
    /// to add a new row of stat text. Returns a pointer to the new value cell
    /// (right side); caller can SetText on it during update ticks. Returns
    /// null if the parent doesn't have the expected sub-node layout.
    ///
    /// <para>
    /// v0.4.2 bug 3 fix preserved: new row Y = parentNode.Height - 20
    /// (NOT -24). With the +20 height bump done first, this places the new
    /// row's top exactly at the old content bottom, no overlap.
    /// </para>
    ///
    /// <para>
    /// v0.4.6: was static in v0.4.5. Now instance-bound so it can track
    /// <see cref="totalInjectedHeight"/> across calls. The outer addon's
    /// RootNode is grown by that total after <see cref="InjectAllRows"/>
    /// completes — without that step the trailing rows we inject into the
    /// gear section fall past the addon's visible clip and look invisible.
    /// </para>
    /// </summary>
    private AtkTextNode* AddStatRow(AtkComponentNode* parentNode, string label,
                                     bool expandCollisionNode = true)
    {
        if (parentNode == null) return null;
        if (parentNode->Component == null) return null;

        var collisionNode = parentNode->Component->UldManager.RootNode;
        if (collisionNode == null) return null;
        var numberNode = (AtkTextNode*)collisionNode->PrevSiblingNode;
        if (numberNode == null) return null;
        var labelNode = (AtkTextNode*)numberNode->AtkResNode.PrevSiblingNode;
        if (labelNode == null) return null;

        parentNode->AtkResNode.Height += 20;
        // v0.6.5.3 — collisionNode growth is gated. Adapted from CharacterPanelRefined's
        // AddStatRow signature (expandCollisionNode parameter). The Gear / Average Item
        // Level component and crafter-stats components have collision nodes that the
        // game uses to bound the existing row's text rendering — growing them stretches
        // the original ILVL text node onto subsequent injected rows, producing the
        // ghost-text overlay seen on VPR/PLD/CRP panels through v0.6.5.2. Substat
        // sections (Crit/Det/DH/Speed/Tenacity/Piety) want the default true behavior so
        // their hit regions extend with the new rows; only advisor / ilvl-area rows
        // pass false. See CharacterPanelRefined CharacterStatusAugments.cs:246 for the
        // original pattern (MIT, Kouzukii — LICENSES/CharacterPanelRefined-MIT.txt).
        if (expandCollisionNode)
            collisionNode->Height += 20;
        // v0.4.6: accumulate so we can grow the outer addon after injection.
        // Always counted — the outer addon RootNode needs to grow for visible content
        // regardless of whether the immediate parent's collision node grew.
        totalInjectedHeight = (ushort)(totalInjectedHeight + 20);

        var newNumberNode = NodeUtil.CloneNode(numberNode);
        var prevSiblingBeforeLabel = labelNode->AtkResNode.PrevSiblingNode;
        labelNode->AtkResNode.PrevSiblingNode  = (AtkResNode*)newNumberNode;
        newNumberNode->AtkResNode.NextSiblingNode = (AtkResNode*)labelNode;
        newNumberNode->AtkResNode.Y    = parentNode->AtkResNode.Height - 20;
        newNumberNode->TextColor       = InjectedRowColor;
        NodeUtil.AllocateFreshTextBuffer(newNumberNode);

        var newLabelNode = NodeUtil.CloneNode(labelNode);
        newNumberNode->AtkResNode.PrevSiblingNode = (AtkResNode*)newLabelNode;
        newLabelNode->AtkResNode.PrevSiblingNode  = prevSiblingBeforeLabel;
        newLabelNode->AtkResNode.NextSiblingNode  = (AtkResNode*)newNumberNode;
        newLabelNode->AtkResNode.Y    = parentNode->AtkResNode.Height - 20;
        newLabelNode->TextColor       = InjectedRowColor;
        NodeUtil.AllocateFreshTextBuffer(newLabelNode);
        newLabelNode->SetText(label);

        // v0.6.6 [CANDIDATE FIX, PENDING VERIFICATION] — BUG-001, hypothesis H1
        //
        // Previous versions had a bidirectional sibling-link patch here:
        //
        //   if (prevSiblingBeforeLabel != null)
        //       prevSiblingBeforeLabel->NextSiblingNode = (AtkResNode*)newLabelNode;
        //
        // It was added defensively to keep both the PrevSibling and NextSibling
        // chains internally consistent after node insertion. The reasoning at
        // the time: "if the renderer walks either direction, we want it to find
        // the new nodes." Plausible-sounding; almost certainly the bug.
        //
        // CharacterPanelRefined (the upstream we adapted AddStatRow from) does
        // NOT do this patch. CPR works visually; ours produces ghost-text
        // artifacts on the Materia Advisor header. See CPR_DEEP_DIVE.md for
        // the full analysis, but the short version:
        //
        // UldManager.UpdateDrawNodeList() rebuilds the component's NodeList[]
        // by walking PrevSiblingNode only (one direction). With our extra
        // NextSibling patch, the new nodes are reachable through TWO traversal
        // paths instead of one. If the rebuild enumerates each reachable node
        // once per path, our cloned labels end up in NodeList[] twice — and
        // render twice per frame at slightly different draw priorities. That
        // matches the visible ghost-text signature exactly.
        //
        // Removing the patch aligns our AddStatRow with CPR's pattern verbatim.
        // If the ghost text resolves with this change in place, H1 is confirmed
        // and we ship v0.6.6 with this revert. If it persists, this code
        // comment gets a "didn't work" addendum and we move to H2 (Gear section
        // repositioning).

        parentNode->Component->UldManager.UpdateDrawNodeList();

        return newNumberNode;
    }

    // ── v0.4.6 addon-height grow ────────────────────────────────────────

    /// <summary>
    /// Grow the outer addon's RootNode (and its visible window-collision /
    /// background nodes when found) by <paramref name="pixels"/>. Called
    /// once at the end of <see cref="OnPostSetup"/> after all AddStatRow
    /// calls have completed.
    ///
    /// <para>
    /// In FFXIV's UI system, AtkUnitBase has a root container whose Height
    /// defines the addon's visible / hit-test clip region. Section components
    /// inside (Offensive Properties, Gear, etc.) auto-flow when their height
    /// changes — but the outer container does NOT auto-grow with them. With
    /// CPR coexisting, CPR adds ~12 rows above us in Offensive Properties +
    /// Speed; we then add 4 rows to the gear section (the LAST section). The
    /// combined ~320px extension exceeds the original window height, so the
    /// advisor's 4 rows fall past the bottom clip and look invisible. This
    /// method is the fix: grow RootNode->Height to match.
    /// </para>
    ///
    /// <para>
    /// We don't restore on PreFinalize because the entire addon (and its
    /// node tree) is torn down between opens — every PostSetup gets a
    /// fresh AtkUnitBase. The growth is per-instance, not persistent state.
    /// </para>
    /// </summary>
    private void GrowAddonHeight(ushort pixels)
    {
        if (characterStatusPtr == null) return;
        if (characterStatusPtr->RootNode == null) return;

        var oldHeight = characterStatusPtr->RootNode->Height;
        characterStatusPtr->RootNode->Height = (ushort)(oldHeight + pixels);

        // Some addons have a windowed background frame distinct from the
        // root. The collision/frame nodes are typically the first few
        // children of RootNode; bump any AtkNineGridNode/AtkResNode that
        // sits at full root width since those are window-frame layers.
        var rootHeight = characterStatusPtr->RootNode->Height;
        var rootWidth  = characterStatusPtr->RootNode->Width;
        var child = characterStatusPtr->RootNode->ChildNode;
        int safety = 0;
        while (child != null && safety++ < 32)
        {
            // Heuristic: any node whose width approximately matches the
            // root's width is a full-window layer (background, frame,
            // collision) and should grow with the root. Section components
            // are narrower and handle their own internal layout.
            if (child->Width >= rootWidth - 8 && child->Width <= rootWidth + 8)
            {
                child->Height = (ushort)(child->Height + pixels);
            }
            child = child->NextSiblingNode;
        }

        DalamudServices.Log.Info(
            $"StatusPanelInjector v0.4.6: outer addon grown {oldHeight}px → {rootHeight}px " +
            $"(+{pixels}px) to fit injected rows.");
    }

    private void ClearPointers()
    {
        characterStatusPtr = null;
        critCompactValue = null;
        detCompactValue  = null;
        dhCompactValue   = null;
        speedGcdValue    = null;
        speedCompactValue = null;
        tenacityCompactValue = null;
        pietyCompactValue    = null;
        advisorHeader = null;
        advisorRec1   = null;
        advisorRec2   = null;
        advisorRec3   = null;
        advisorSectionPresent = false;
    }
}

