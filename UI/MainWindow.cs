using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using GearGoblin.Services;

namespace GearGoblin.UI;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin) : base("GearGoblin###GearGoblinMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        Size      = new Vector2(720, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // Dalamud v14 moved LocalPlayer off IClientState to IObjectTable.
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
        if (ImGui.SmallButton("Refresh")) { /* nothing cached yet, but keeps the muscle memory */ }

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
                ImGui.TextDisabled("Coming in v0.2: paste an Etro/XIVGear link or pick a casual preset.");
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Materia"))
            {
                ImGui.TextDisabled("Coming in v0.4: meld plan, breakpoint analysis, sell-vs-meld.");
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
}
