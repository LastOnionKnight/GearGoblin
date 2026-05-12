using System;
using System.Numerics;
using System.Reflection;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using GearGoblin.Services;

namespace GearGoblin.UI;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private static readonly string s_versionString = ResolveVersion();

    public MainWindow(Plugin plugin) : base("GearGoblin###GearGoblinMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(640, 460),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        Size      = new Vector2(820, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var player = DalamudServices.ObjectTable.LocalPlayer;
        if (player is null)
        {
            ImGui.TextDisabled("Not logged in.");
            return;
        }

        var job = player.ClassJob.Value.Abbreviation.ExtractText();
        var lvl = player.Level;
        ImGui.Text($"{player.Name} — {job} Lv {lvl}");
        ImGui.SameLine();
        if (ImGui.SmallButton("Refresh")) { /* placeholder for future cache invalidation */ }

        // v0.3 badge — right-aligned in the header line
        var avail = ImGui.GetContentRegionAvail();
        var badgeText = $"v{s_versionString}";
        var badgeWidth = ImGui.CalcTextSize(badgeText).X + 12;
        ImGui.SameLine(ImGui.GetCursorPosX() + avail.X - badgeWidth);
        ImGui.TextColored(new Vector4(0.55f, 0.75f, 1f, 1f), badgeText);

        ImGui.Separator();

        if (ImGui.BeginTabBar("##goblintabs"))
        {
            if (ImGui.BeginTabItem("Current Gear"))
            {
                DrawCurrentGear();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Plan"))
            {
                PlanTab.Draw(plugin.Inventory);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Materia"))
            {
                MateriaTab.Draw(plugin.Inventory);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("About"))
            {
                DrawAbout();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawCurrentGear()
    {
        var equipped = plugin.Inventory.ReadEquipped();
        if (equipped.Count == 0)
        {
            ImGui.TextDisabled("No equipped items detected.");
            return;
        }
        var ilvl = plugin.Inventory.CalculateAverageItemLevel(equipped);
        ImGui.Text($"Average Item Level: {ilvl}");
        ImGui.Spacing();
        if (ImGui.BeginTable("##gear", 4,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInner | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Slot",    ImGuiTableColumnFlags.WidthFixed, 90);
            ImGui.TableSetupColumn("Item",    ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("iLvl",    ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Materia", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();
            foreach (var piece in equipped)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text(piece.Slot.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(piece.IsHighQuality ? $"{piece.Name} ★" : piece.Name);
                ImGui.TableNextColumn(); ImGui.Text(piece.ItemLevel.ToString());
                ImGui.TableNextColumn();
                if (piece.Materia.Count == 0)
                {
                    ImGui.TextDisabled("—");
                }
                else
                {
                    foreach (var m in piece.Materia)
                    {
                        ImGui.TextUnformatted($"+{m.StatValue} {m.StatName}");
                        if (m.SlotIndex < piece.Materia.Count - 1) ImGui.SameLine();
                    }
                }
            }
            ImGui.EndTable();
        }
    }

    private void DrawAbout()
    {
        ImGui.TextUnformatted("GearGoblin");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.55f, 0.75f, 1f, 1f), $"v{s_versionString}");

        ImGui.Spacing();
        ImGui.TextWrapped(
            "BiS planner, gear inventory reader, and materia advisor for FFXIV. " +
            "As of v0.4.5 it replaces CharacterPanelRefined entirely — derived stats, " +
            "breakpoint hints, real GCD, and a Materia Advisor section all inject directly " +
            "into the native Character window.");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("By LastOnionKnight");
        ImGui.TextDisabled("Refia Rakkiri — the Last Onion Knight (Aisling O'Callaghan, Cork)");

        // ── v0.4.5 ────────────────────────────────────────────────────────
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.5f, 1f), "v0.4.5 — full CPR replacement:");
        ImGui.BulletText("Compact derived rows per substat:");
        ImGui.Indent();
        ImGui.BulletText("Crit: chance · damage multiplier · damage increase · next tier");
        ImGui.BulletText("Det: damage increase · next tier");
        ImGui.BulletText("DH: chance · damage increase · next tier");
        ImGui.Unindent();
        ImGui.BulletText("Tenacity row (tank role): damage out · damage taken");
        ImGui.BulletText("Piety row (healer role): MP/tick");
        ImGui.BulletText("Speed section: real GCD + breakpoint + speed damage (consolidated)");
        ImGui.BulletText("CPR coexistence: auto-detects CharacterPanelRefined and steps aside");
        ImGui.BulletText("Per-section toggles for every derivation group");
        ImGui.BulletText("First-inject chat log signature so you can verify which version is loaded");

        // ── v0.4.2 ────────────────────────────────────────────────────────
        ImGui.Spacing();
        ImGui.TextUnformatted("v0.4.2 fixes:");
        ImGui.BulletText("Materia Advisor footer no longer scrolls off the panel");
        ImGui.BulletText("Critical Hit breakpoint hint now renders reliably (label-walk, not positional)");
        ImGui.BulletText("Det/DH injected rows no longer overlap vanilla stat values");
        ImGui.BulletText("Empty advisor shows clear status text instead of dashes");
        ImGui.BulletText("Advisor section consolidated from 6 rows to 4 (header carries status counts)");

        // ── v0.4.1 ────────────────────────────────────────────────────────
        ImGui.Spacing();
        ImGui.TextUnformatted("v0.4.1 — Tonberry Tactics handoff:");
        ImGui.BulletText("/goblinexport command writes a clipboard-ready JSON of your equipped gear");
        ImGui.BulletText("Compatible with the Tonberry Tactics web optimizer (TLF Gear Division)");
        ImGui.BulletText("Dalamud SDK 15 compat (AddonEventData signature update)");

        // ── v0.4.0 ────────────────────────────────────────────────────────
        ImGui.Spacing();
        ImGui.TextUnformatted("v0.4.0 — native Character window integration:");
        ImGui.BulletText("Breakpoint hints injected under each substat row");
        ImGui.BulletText("Real GCD derivation injected under Skill/Spell Speed");
        ImGui.BulletText("Materia Advisor section injected under Gear");
        ImGui.BulletText("Click the advisor header to open the standalone /goblin window");

        // ── v0.3.x ────────────────────────────────────────────────────────
        ImGui.Spacing();
        ImGui.TextUnformatted("Earlier (v0.3.x) — standalone window:");
        ImGui.BulletText("Stat sheet with breakpoint analysis");
        ImGui.BulletText("Plan mode: recommended materia for empty meld slots");
        ImGui.BulletText("Audit mode: review existing melds for overcap and tier issues");
        ImGui.BulletText("Pure-math vs Balance-preset weighting");
        ImGui.BulletText("Etro / XIVGear BiS comparison");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextDisabled("Materia formulas re-derived from public datamining sources");
        ImGui.TextDisabled("(Akhmorning Allagan Studies, FFXIV datamining repo).");
        ImGui.TextDisabled("AtkNode injection patterns adapted from CharacterPanelRefined (MIT).");

        ImGui.Spacing();
        ImGui.TextDisabled("\"No gear. No hope. No pants. Just onions.\" — TLF");
    }

    private static string ResolveVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v is null ? "0.3.2" : $"{v.Major}.{v.Minor}.{v.Build}";
    }
}
