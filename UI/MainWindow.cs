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
        ImGui.TextWrapped("BiS planner, gear inventory reader, and materia advisor for FFXIV.");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("By LastOnionKnight");
        ImGui.TextDisabled("Refia Rakkiri — the Last Onion Knight");

        ImGui.Spacing();
        ImGui.TextUnformatted("v0.3.0 features:");
        ImGui.BulletText("Stat sheet with breakpoint analysis");
        ImGui.BulletText("Plan mode: recommended materia for empty meld slots");
        ImGui.BulletText("Audit mode: review existing melds for issues");
        ImGui.BulletText("Pure-math vs Balance-preset weighting");
        ImGui.BulletText("Etro / XIVGear BiS comparison");

        ImGui.Spacing();
        ImGui.TextUnformatted("v0.3.1 fixes:");
        ImGui.BulletText("Plan/Audit now use real per-piece slot counts from item data");
        ImGui.BulletText("Materia tier mapping corrected (was off by four tiers)");
        ImGui.BulletText("Equipment slots read from item category, not inventory index");
        ImGui.BulletText("Etro: shields/off-hands now parsed correctly");

        ImGui.Spacing();
        ImGui.TextUnformatted("v0.3.2 fixes:");
        ImGui.BulletText("Plan/Audit no longer crash on combination-slot items");
        ImGui.BulletText("Hardened against duplicate-slot inventory edge cases");

        ImGui.Spacing();
        ImGui.TextDisabled("Materia formulas re-derived from public datamining sources");
        ImGui.TextDisabled("(Akhmorning Allagan Studies, FFXIV datamining repo).");
    }

    private static string ResolveVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v is null ? "0.3.2" : $"{v.Major}.{v.Minor}.{v.Build}";
    }
}
