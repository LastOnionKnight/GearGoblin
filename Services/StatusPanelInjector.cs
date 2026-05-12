// Services/StatusPanelInjector.cs
//
// v0.4.5 — full CPR-replacement release.
// ============================================================================
//
// Inject GearGoblin's derived stat data and Materia Advisor directly into
// FFXIV's native CharacterStatus addon. As of v0.4.5 we replace
// CharacterPanelRefined entirely: each substat gets a single compact derived
// row containing the chance / damage multiplier / damage-increase
// contribution AND the breakpoint hint, all on one line. Tenacity and Piety
// get role-gated rows. Materia Advisor section remains four rows.
//
// Lineage:
//   v0.4.0  first injection — breakpoint hints + GCD + Materia Advisor
//   v0.4.1  /goblinexport + Dalamud SDK compat (AddonEventData signature)
//   v0.4.2  4 bug fixes: footer off-panel, missing Crit hint, row overlap,
//           empty-advisor blank rows; advisor consolidated 6→4 rows
//   v0.4.5  THIS RELEASE
//           - Replace separate "next tier:" rows with combined compact
//             derived rows (chance · damage · DI · next +N)
//           - Add Tenacity row (tank role only)
//           - Add Piety row (healer role only)
//           - Speed section consolidated to (GCD real) + (next + dmg)
//           - CPR detection: if CharacterPanelRefined is active, skip
//             derivation injection unless ForceDerivationsOverCpr is set
//           - Per-section toggles via Configuration
//           - Visible v0.4.5 chat-log signature on first inject so users
//             can verify which version is actually running
//
// Injection patterns (AddStatRow, node walks, hardcoded section IDs) are
// adapted from CharacterPanelRefined (MIT). See LICENSES/CharacterPanelRefined-MIT.txt.
//
// Lifecycle:
//   PostSetup            → InjectAllRows (every time the addon opens)
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

public sealed unsafe class StatusPanelInjector : IDisposable
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

    private static readonly ByteColor InjectedRowColor =
        new() { A = 0xFF, R = 0xA0, G = 0xA0, B = 0xA0 };

    private static readonly ByteColor AdvisorAccentColor =
        new() { A = 0xFF, R = 0xC9, G = 0xB2, B = 0x7E };

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
            "StatusPanelInjector v0.4.5: registered AddonLifecycle listeners for CharacterStatus.");
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
    {
        try
        {
            characterStatusPtr = (AtkUnitBase*)args.Addon.Address;

            cprDetectedActive = CprDetection.IsCprActive();
            if (cprDetectedActive && !plugin.Configuration.ForceDerivationsOverCpr)
            {
                DalamudServices.Log.Info(
                    "StatusPanelInjector v0.4.5: CharacterPanelRefined detected as active; " +
                    "skipping derived-stat injection (set ForceDerivationsOverCpr=true to override). " +
                    "Breakpoint hints, real GCD, and Materia Advisor will still inject normally.");
            }

            InjectAllRows();
            UpdateAllValues();

            if (!firstInjectLogged)
            {
                firstInjectLogged = true;
                DalamudServices.Log.Info(
                    "StatusPanelInjector v0.4.5: first inject complete. " +
                    $"CPR active: {cprDetectedActive}. " +
                    $"Derivations enabled: {WillInjectDerivations()}.");
            }
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "StatusPanelInjector: PostSetup injection failed.");
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
        try { UpdateAllValues(); }
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
                "StatusPanelInjector v0.4.5: OffensiveProperties node not found; derivations skipped.");
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
                "StatusPanelInjector v0.4.5: could not identify all three offensive substat rows " +
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
                $"StatusPanelInjector v0.4.5: speed section ({sectionId}) not found; speed rows skipped.");
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
                $"StatusPanelInjector v0.4.5: '{wantedLabel}' component not found; speed rows skipped.");
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
                "StatusPanelInjector v0.4.5: Role Properties section not found; role rows skipped.");
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
                $"StatusPanelInjector v0.4.5: '{wantedLabel}' component not found in Role Properties.");
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
                "StatusPanelInjector v0.4.5: Gear node not found; Materia Advisor skipped.");
            return;
        }

        var avgIlvlComponent = (AtkComponentNode*)gear->ChildNode;

        advisorHeader = AddStatRow(avgIlvlComponent, "── Materia Advisor ──");
        advisorRec1   = AddStatRow(avgIlvlComponent, "");
        advisorRec2   = AddStatRow(avgIlvlComponent, "");
        advisorRec3   = AddStatRow(avgIlvlComponent, "");

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
                    "StatusPanelInjector v0.4.5: AddEvent returned null; advisor header will be non-interactive.");
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

            DalamudServices.Log.Debug(
                $"Advisor: pieces={pieces.Count} audits={result.Audits.Count} " +
                $"crit={crit} warn={warn} planRecs={result.PlanRecommendations.Count} " +
                $"candidates={candidates.Count}");

            if (candidates.Count == 0)
            {
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

            if (advisorHeader != null)
            {
                advisorHeader->SetText($"{crit}c · {warn}w · {empty}e   ▶ /goblin");
            }
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "StatusPanelInjector v0.4.5: UpdateAdvisor threw.");
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
        if (advisorHeader != null) advisorHeader->SetText("▶ /goblin");
    }

    // ── Header click → invoke /goblin ───────────────────────────────────

    private void OnAdvisorHeaderClick(AddonEventType type, AddonEventData data)
    {
        try
        {
            DalamudServices.CommandManager.ProcessCommand("/goblin");
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "StatusPanelInjector v0.4.5: /goblin invoke failed.");
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
    /// </summary>
    private static AtkTextNode* AddStatRow(AtkComponentNode* parentNode, string label)
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
        collisionNode->Height          += 20;

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

        if (prevSiblingBeforeLabel != null)
            prevSiblingBeforeLabel->NextSiblingNode = (AtkResNode*)newLabelNode;

        parentNode->Component->UldManager.UpdateDrawNodeList();

        return newNumberNode;
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
    }
}
