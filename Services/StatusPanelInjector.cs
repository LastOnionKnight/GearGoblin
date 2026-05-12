// Services/StatusPanelInjector.cs
//
// v0.4.0 headline feature: inject GearGoblin's derived data and Materia
// Advisor recommendations directly into FFXIV's native CharacterStatus addon.
//
// Injection patterns (AddStatRow, node walks, hardcoded section IDs) are
// adapted from CharacterPanelRefined (MIT). See LICENSES/CharacterPanelRefined-MIT.txt.
//
// Lifecycle:
//   PostSetup            → InjectAllRows (called every time the addon opens)
//   PostRequestedUpdate  → UpdateAllValues (called on every game tick while open)
//   PreFinalize          → cleanup pointers; addon teardown frees allocated memory
//
// We register against the AddonLifecycle service, NOT IGameGui's directly, so
// Dalamud manages our subscriptions correctly across plugin reloads.

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

public sealed unsafe class StatusPanelInjector : IDisposable
{
    // ── Node IDs verified by CharacterPanelRefined in-game inspection ──
    // These are stable across the current expansion patch cycle but should
    // be re-verified after major patches (X.0, X.1).
    private const uint AttributesNodeId          = 26;
    private const uint OffensivePropertiesNodeId = 36;
    private const uint DefensivePropertiesNodeId = 44;
    private const uint PhysicalPropertiesNodeId  = 51;  // Skill Speed lives here
    private const uint MentalPropertiesNodeId    = 58;  // Spell Speed lives here
    private const uint GearNodeId                = 80;  // Avg item level
    private const uint RolePropertiesNodeId      = 86;  // Piety / Tenacity

    private const string AddonName = "CharacterStatus";

    // Gray text matches CPR convention so injected rows read as
    // "supplementary" rather than competing with SE's own data.
    private static readonly ByteColor InjectedRowColor =
        new() { A = 0xFF, R = 0xA0, G = 0xA0, B = 0xA0 };

    // Slightly warmer gold for the advisor section to set it apart visually.
    private static readonly ByteColor AdvisorAccentColor =
        new() { A = 0xFF, R = 0xC9, G = 0xB2, B = 0x7E };

    // ── Dependencies ────────────────────────────────────────────────────
    private readonly Plugin plugin;

    // ── Live state ──────────────────────────────────────────────────────
    private AtkUnitBase* characterStatusPtr;
    private bool registered;

    // Breakpoint hint value-cell pointers (one per substat we annotate).
    private AtkTextNode* critBpValue;
    private AtkTextNode* detBpValue;
    private AtkTextNode* dhBpValue;
    private AtkTextNode* sksGcdValue;
    private AtkTextNode* sksBpValue;

    // Materia Advisor section pointers. v0.4.2 consolidated layout:
    // header carries both status counts and the clickable /goblin glyph in
    // its value cell, so the separate status/footer fields are retired.
    private AtkTextNode* advisorHeader;
    private AtkTextNode* advisorRec1;
    private AtkTextNode* advisorRec2;
    private AtkTextNode* advisorRec3;

    // Footer click handle — must be removed on dispose.
    private IAddonEventHandle? footerClickHandle;

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
            "StatusPanelInjector: registered AddonLifecycle listeners for CharacterStatus.");
    }

    public void Dispose()
    {
        // Remove click handler explicitly. Dalamud auto-cleans on plugin
        // unload, but we own the handle while the plugin lives.
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
    {
        try
        {
            characterStatusPtr = (AtkUnitBase*)args.Addon.Address;
            InjectAllRows();
            // First-paint values: don't wait for the next RequestedUpdate.
            UpdateAllValues();
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "StatusPanelInjector: PostSetup injection failed.");
            ClearPointers();
        }
    }

    private void OnPreFinalize(AddonEvent type, AddonArgs args)
    {
        // The addon's node memory is freed by the game on teardown; we just
        // null our pointers and release the click handle.
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
        try { UpdateAllValues(); }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "StatusPanelInjector: RequestedUpdate failed.");
        }
    }

    // ── Injection: walk the addon's node tree and clone in our rows ─────

    private void InjectAllRows()
    {
        InjectBreakpointHints();
        InjectSpeedDerivation();
        InjectAdvisorSection();
    }

    /// <summary>
    /// Add a small italic-gray row under Crit, Det, and DH showing how many
    /// more points are needed to hit the next 0.1% tier.
    /// </summary>
    /// <remarks>
    /// v0.4.2 fix (bug 2): previously walked siblings positionally
    /// (ChildNode → PrevSibling → PrevSibling) and assumed DH-then-Det-then-Crit
    /// order. That order isn't guaranteed across patches or with other plugins
    /// like CPR injecting their own rows. Now we iterate all children and
    /// identify each stat component by reading its label text, which is
    /// stable per game language. English client only for v0.4.2; localized
    /// matching via Addon sheet is a follow-up.
    /// </remarks>
    private void InjectBreakpointHints()
    {
        var offensive = characterStatusPtr->UldManager.SearchNodeById(OffensivePropertiesNodeId);
        if (offensive == null)
        {
            DalamudServices.Log.Warning(
                "StatusPanelInjector: OffensiveProperties node not found; breakpoint hints skipped.");
            return;
        }

        // Walk all child components and identify each by label text.
        AtkComponentNode* critComponent = null;
        AtkComponentNode* detComponent  = null;
        AtkComponentNode* dhComponent   = null;

        var child = offensive->ChildNode;
        int safety = 0;
        while (child != null && safety++ < 32)
        {
            // We only care about component nodes — section headers etc. have
            // different layouts and won't carry a stat label.
            if (child->Type == NodeType.Component)
            {
                var component = (AtkComponentNode*)child;
                var label     = GetComponentLabelText(component);
                if (!string.IsNullOrEmpty(label))
                {
                    // Match by prefix so "Critical Hit" matches whether the
                    // full label is "Critical Hit" or "Critical Hit Rate".
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
                "StatusPanelInjector: could not identify all three offensive substat rows " +
                $"by label (crit={(critComponent != null)} det={(detComponent != null)} dh={(dhComponent != null)}). " +
                "Breakpoint hints partially or fully skipped.");
        }

        // Inject in visual order (Crit first, DH last) — order doesn't actually
        // matter mechanically since each parent is independent, but matches
        // the player's reading order.
        if (critComponent != null) critBpValue = AddStatRow(critComponent, "next tier:");
        if (detComponent  != null) detBpValue  = AddStatRow(detComponent,  "next tier:");
        if (dhComponent   != null) dhBpValue   = AddStatRow(dhComponent,   "next tier:");
    }

    /// <summary>
    /// Read the label TextNode contents from a stat-row component. Returns
    /// null if the component's internal layout doesn't match the expected
    /// (collisionNode, numberNode, labelNode) sibling chain. v0.4.2.
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

        try
        {
            return label->NodeText.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Add a "real GCD" row under Skill Speed (physical jobs) or Spell Speed
    /// (caster jobs). Vanilla shows base 2.50s; we show the speed-adjusted GCD.
    /// </summary>
    private void InjectSpeedDerivation()
    {
        // Try physical first; if missing, try mental. Some jobs only render
        // one of the two property sections at a time.
        var phys = characterStatusPtr->UldManager.SearchNodeById(PhysicalPropertiesNodeId);
        if (phys != null && phys->ChildNode != null)
        {
            var skillSpeed = phys->ChildNode;
            sksGcdValue = AddStatRow((AtkComponentNode*)skillSpeed, "GCD (real):");
            sksBpValue  = AddStatRow((AtkComponentNode*)skillSpeed, "next GCD tier:");
            return;
        }

        var mental = characterStatusPtr->UldManager.SearchNodeById(MentalPropertiesNodeId);
        if (mental != null && mental->ChildNode != null)
        {
            var spellSpeed = mental->ChildNode;
            sksGcdValue = AddStatRow((AtkComponentNode*)spellSpeed, "GCD (real):");
            sksBpValue  = AddStatRow((AtkComponentNode*)spellSpeed, "next GCD tier:");
        }
    }

    /// <summary>
    /// Materia Advisor section: a pseudo-section made of stacked AddStatRow
    /// calls under the Gear container. v0.4.2 (bug 1): consolidated from 6
    /// rows to 4 — the header row now carries the status-count text in its
    /// value cell along with the clickable <c>▶ /goblin</c> glyph. This
    /// saves 40px of vertical real estate that was pushing the footer off
    /// the bottom of the Character window. A real section node with its own
    /// header divider remains queued for a later release.
    /// </summary>
    private void InjectAdvisorSection()
    {
        var gear = characterStatusPtr->UldManager.SearchNodeById(GearNodeId);
        if (gear == null || gear->ChildNode == null)
        {
            DalamudServices.Log.Warning(
                "StatusPanelInjector: Gear node not found; Materia Advisor skipped.");
            return;
        }

        var avgIlvlComponent = (AtkComponentNode*)gear->ChildNode;

        // 4 rows total: header (clickable, carries status) + 3 rec rows.
        advisorHeader = AddStatRow(avgIlvlComponent, "── Materia Advisor ──");
        advisorRec1   = AddStatRow(avgIlvlComponent, "");
        advisorRec2   = AddStatRow(avgIlvlComponent, "");
        advisorRec3   = AddStatRow(avgIlvlComponent, "");

        // Recolor the header so it reads as a unit. Rec rows stay in the
        // default injected-row gray so they don't compete for attention.
        if (advisorHeader != null) advisorHeader->TextColor = AdvisorAccentColor;

        // Make the header value cell clickable. We register on the value
        // cell (right side) since that's where "▶ /goblin · status" renders.
        if (advisorHeader != null)
        {
            var headerNode = (AtkResNode*)advisorHeader;
            headerNode->NodeFlags |=
                NodeFlags.EmitsEvents | NodeFlags.RespondToMouse | NodeFlags.HasCollision;

            footerClickHandle = DalamudServices.AddonEventManager.AddEvent(
                (nint)characterStatusPtr,
                (nint)headerNode,
                AddonEventType.MouseClick,
                OnAdvisorFooterClick);

            if (footerClickHandle == null)
                DalamudServices.Log.Warning(
                    "StatusPanelInjector: AddEvent returned null; advisor header will be non-interactive.");
        }
    }

    private void OnAdvisorFooterClick(AddonEventType type, AddonEventData data)
    {
        try
        {
            // Toggle the standalone /goblin window. Plugin owns the toggle method.
            plugin.ToggleMain();
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "StatusPanelInjector: footer click handler threw.");
        }
    }

    // ── Update: refresh values every tick the addon is open ─────────────

    private void UpdateAllValues()
    {
        var snap = StatReader.ReadCurrent();
        if (snap is null) return;
        var s = snap.Value;
        var mod = LevelTable.Get(s.Level);
        var profile = JobProfiles.GetOrDefault(s.JobId);

        UpdateBreakpoints(s, mod);
        UpdateSpeedDerivation(s, mod, profile);
        UpdateAdvisor(s, mod, profile);
    }

    private void UpdateBreakpoints(StatSnapshot s, LevelMod mod)
    {
        if (critBpValue != null)
        {
            var bd = Formulas.CritRate(s.Crit, mod);
            var delta = bd.NextTier - s.Crit;
            critBpValue->SetText(delta > 0 ? $"+{delta} → {bd.NextTier}" : "at cap");
        }
        if (detBpValue != null)
        {
            var bd = Formulas.Determination(s.Det, mod);
            var delta = bd.NextTier - s.Det;
            detBpValue->SetText(delta > 0 ? $"+{delta} → {bd.NextTier}" : "at cap");
        }
        if (dhBpValue != null)
        {
            var bd = Formulas.DirectHit(s.DH, mod);
            var delta = bd.NextTier - s.DH;
            dhBpValue->SetText(delta > 0 ? $"+{delta} → {bd.NextTier}" : "at cap");
        }
    }

    private void UpdateSpeedDerivation(StatSnapshot s, LevelMod mod, JobProfile profile)
    {
        if (sksGcdValue == null) return;

        // Use whichever speed stat the job actually uses.
        bool usesSps = Array.IndexOf(profile.RelevantStats, Substat.SpellSpeed) >= 0;
        int speed = usesSps ? s.SpS : s.SkS;

        var gcd = Formulas.GcdFromSpeed(speed, mod);
        sksGcdValue->SetText($"{gcd:0.00}s");

        if (sksBpValue != null)
        {
            var bd = Formulas.SpeedDamage(speed, mod);
            var delta = bd.NextTier - speed;
            sksBpValue->SetText(delta > 0 ? $"+{delta} → next tier" : "at cap");
        }
    }

    private void UpdateAdvisor(StatSnapshot s, LevelMod mod, JobProfile profile)
    {
        if (advisorRec1 == null) return;  // section not injected

        try
        {
            var equipped = plugin.Inventory.ReadEquipped();
            var pieces = equipped.Select(MeldSlotsBuilder.FromEquipped).ToList();
            if (pieces.Count == 0)
            {
                ClearAdvisorRows("(no gear)");
                return;
            }

            // Default to Pure Math weights for the in-addon advisor.
            // Players who want Balance weights can toggle in the standalone window.
            var result = MeldOptimizer.Optimize(pieces, s, mod, profile, WeightMode.PureMath);

            // Rank candidates: Critical/Warning audits with replacements
            // suggested, sorted by GainIfReplaced descending so the most
            // impactful upgrades surface first in the limited 3-row space.
            // Then fill any remaining slots from PlanRecommendations
            // (empty meld slots), again sorted by ScoreGain descending.
            var candidates = new List<string>();
            var topAudits = result.Audits
                .Where(a => a.Severity is AuditSeverity.Critical or AuditSeverity.Warning)
                .Where(a => a.SuggestedReplacement is not null)
                .OrderByDescending(a => a.GainIfReplaced)
                .Take(3);
            foreach (var audit in topAudits)
            {
                // MeldAudit shape:
                //   Piece (EquipSlot), SlotIndex (int),
                //   SuggestedReplacement (MateriaSpec?), GainIfReplaced (double)
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
                    // MeldRecommendation shape:
                    //   Piece (EquipSlot), SlotIndex (int),
                    //   Materia (MateriaSpec), ScoreGain (double)
                    var slot = rec.Piece;
                    var idx  = rec.SlotIndex + 1;
                    var mat  = rec.Materia.Display();
                    candidates.Add($"{slot} #{idx} ← {mat}");
                }
            }

            // Status counts — folded into header value cell in the v0.4.2
            // consolidated layout.
            var crit  = result.Audits.Count(a => a.Severity == AuditSeverity.Critical);
            var warn  = result.Audits.Count(a => a.Severity == AuditSeverity.Warning);
            var empty = result.PlanRecommendations.Count;

            // v0.4.2 (bug 4): diagnostic logging so we can tell genuine "all
            // melds optimal" from silent optimizer failure. Logged at Debug
            // level so it doesn't flood normal logs.
            DalamudServices.Log.Debug(
                $"Advisor: pieces={pieces.Count} audits={result.Audits.Count} " +
                $"crit={crit} warn={warn} planRecs={result.PlanRecommendations.Count} " +
                $"candidates={candidates.Count}");

            // Rec rows.
            if (candidates.Count == 0)
            {
                // v0.4.2 (bug 4): explicit "everything looks fine" message so
                // the section doesn't render as 3 blank rows. Communicates
                // that we looked and found nothing, vs appearing broken.
                SetAdvisorRow(advisorRec1, "All guaranteed slots filled · no upgrades suggested");
                SetAdvisorRow(advisorRec2, null);
                SetAdvisorRow(advisorRec3, null);
            }
            else
            {
                SetAdvisorRow(advisorRec1, candidates.ElementAtOrDefault(0));
                SetAdvisorRow(advisorRec2, candidates.ElementAtOrDefault(1));
                SetAdvisorRow(advisorRec3, candidates.ElementAtOrDefault(2));
            }

            // Header value cell: status counts plus the clickable /goblin
            // glyph. v0.4.2 consolidated layout.
            if (advisorHeader != null)
            {
                advisorHeader->SetText($"{crit}c · {warn}w · {empty}e   ▶ /goblin");
            }
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "StatusPanelInjector: UpdateAdvisor threw.");
            ClearAdvisorRows("(advisor error)");
        }
    }

    private void SetAdvisorRow(AtkTextNode* node, string? text)
    {
        if (node == null) return;
        node->SetText(string.IsNullOrEmpty(text) ? "—" : text);
    }

    private void ClearAdvisorRows(string placeholder)
    {
        SetAdvisorRow(advisorRec1, placeholder);
        SetAdvisorRow(advisorRec2, null);
        SetAdvisorRow(advisorRec3, null);
        // Header keeps its label; clear the value-cell status.
        if (advisorHeader != null) advisorHeader->SetText("▶ /goblin");
    }

    // ── CPR-ported core helper ──────────────────────────────────────────

    /// <summary>
    /// Clone the existing label + number text nodes from the parent component,
    /// link them in via sibling pointers, allocate fresh string buffers,
    /// and bump the parent's height by 20px. Returns the new number node
    /// (the value cell on the right) for caller to call <c>SetText</c> on.
    /// </summary>
    /// <remarks>
    /// Adapted from CharacterPanelRefined (MIT). The original supports an
    /// <c>hideOriginal</c> flag and color-copy mode; we only need the simple
    /// "add a gray-text row" case, so those branches are dropped.
    /// See CPR's CharacterStatusAugments.cs AddStatRow for the full version.
    /// </remarks>
    private static AtkTextNode* AddStatRow(AtkComponentNode* parentNode, string label)
    {
        if (parentNode == null) return null;

        var collisionNode = parentNode->Component->UldManager.RootNode;
        if (collisionNode == null) return null;

        // Existing rows are paired (label, number) walked from the collision node.
        var numberNode = (AtkTextNode*)collisionNode->PrevSiblingNode;
        if (numberNode == null) return null;
        var labelNode  = (AtkTextNode*)numberNode->AtkResNode.PrevSiblingNode;
        if (labelNode == null) return null;

        // Make room.
        parentNode->AtkResNode.Height += 20;
        collisionNode->Height          += 20;

        // Clone the number node (right side, value cell).
        var newNumberNode = NodeUtil.CloneNode(numberNode);
        var prevSiblingBeforeLabel = labelNode->AtkResNode.PrevSiblingNode;
        labelNode->AtkResNode.PrevSiblingNode  = (AtkResNode*)newNumberNode;
        newNumberNode->AtkResNode.NextSiblingNode = (AtkResNode*)labelNode;
        // v0.4.2 fix (bug 3): was -24, which placed the new row 4px ABOVE the
        // original content's bottom edge and caused visible overlap with the
        // last vanilla row under each parent. -20 aligns the new row exactly
        // at the old bottom edge (i.e., Y = old height before the +20 bump).
        newNumberNode->AtkResNode.Y    = parentNode->AtkResNode.Height - 20;
        newNumberNode->TextColor       = InjectedRowColor;
        NodeUtil.AllocateFreshTextBuffer(newNumberNode);

        // Clone the label node (left side, descriptor).
        var newLabelNode = NodeUtil.CloneNode(labelNode);
        newNumberNode->AtkResNode.PrevSiblingNode = (AtkResNode*)newLabelNode;
        newLabelNode->AtkResNode.PrevSiblingNode  = prevSiblingBeforeLabel;
        newLabelNode->AtkResNode.NextSiblingNode  = (AtkResNode*)newNumberNode;
        newLabelNode->AtkResNode.Y    = parentNode->AtkResNode.Height - 20;
        newLabelNode->TextColor       = InjectedRowColor;
        NodeUtil.AllocateFreshTextBuffer(newLabelNode);
        newLabelNode->SetText(label);

        parentNode->Component->UldManager.UpdateDrawNodeList();
        return newNumberNode;
    }

    private void ClearPointers()
    {
        characterStatusPtr = null;
        critBpValue = null;  detBpValue = null;  dhBpValue = null;
        sksGcdValue = null;  sksBpValue = null;
        advisorHeader = null;
        advisorRec1 = null;  advisorRec2 = null;  advisorRec3 = null;
    }
}
