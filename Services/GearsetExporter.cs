using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Dalamud.Bindings.ImGui;

namespace GearGoblin.Services;

/// <summary>
/// Serializes the currently-equipped gearset to a base64-encoded JSON string
/// suitable for pasting into the Tonberry Tactics web app. v0.4.1.
///
/// <para>
/// Wire format: <c>GG-EXPORT:v1:&lt;base64(json)&gt;</c>. The version segment
/// is part of the prefix (not just the JSON body) so the web-app side can
/// reject incompatible payloads cleanly without trying to decode them. Bump
/// to <c>v2:</c> when the schema changes shape.
/// </para>
///
/// <para>
/// The schema is intentionally decoupled from internal types
/// (<see cref="EquippedPiece"/>, <see cref="MateriaMeld"/>) via dedicated DTO
/// records below. Internal refactors don't break the export contract.
/// </para>
/// </summary>
public sealed class GearsetExporter : IGearsetExporter
{
    private const string Prefix        = "GG-EXPORT:v2:";
    private const int    SchemaVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented        = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IInventoryReader inventory;

    public GearsetExporter(IInventoryReader inventory)
    {
        this.inventory = inventory;
    }

    /// <summary>
    /// Build the export payload, copy it to the system clipboard, and report
    /// the outcome to the chat log. Returns true on success, false on any
    /// pre-condition failure (not logged in, no gear, serialization fault).
    /// </summary>
    public bool ExportToClipboard()
    {
        var player = DalamudServices.ObjectTable.LocalPlayer;
        if (player is null)
        {
            DalamudServices.ChatGui.PrintError("[GearGoblin] Cannot export: not logged in.");
            return false;
        }

        var equipped = inventory.ReadEquipped();
        if (equipped.Count == 0)
        {
            DalamudServices.ChatGui.PrintError("[GearGoblin] Cannot export: no gear equipped.");
            return false;
        }

        var snap = GearGoblin.Materia.StatReader.ReadCurrent();
        if (snap == null)
        {
            DalamudServices.ChatGui.PrintError("[GearGoblin] Cannot export: unable to read character stats.");
            return false;
        }

        try
        {
            var jobRowId = player.ClassJob.RowId;
            var jobAbbr  = player.ClassJob.Value.Abbreviation.ExtractText();

            var character = new GearGoblin.Core.ExportCharacterV2(
                Job:              jobRowId,
                JobAbbreviation:  jobAbbr,
                Level:            (int)player.Level,
                AverageItemLevel: inventory.CalculateAverageItemLevel(equipped),
                TotalStats:       BuildTotalStats(snap.Value, equipped)
            );

            var pieces = equipped.Select(p => new GearGoblin.Core.ExportPieceV1(
                Slot:              p.Slot.ToString(),
                ItemId:            p.ItemId,
                Name:              p.Name,
                ItemLevel:         p.ItemLevel,
                IsHighQuality:     p.IsHighQuality,
                MateriaSlotCount:  p.MateriaSlotCount,
                IsOvermeldAllowed: p.IsOvermeldAllowed,
                Materia:           p.Materia.Select(m => new GearGoblin.Core.ExportMateriaV1(
                    SlotIndex: m.SlotIndex,
                    MateriaId: m.MateriaId,
                    Grade:     m.Grade,
                    StatName:  m.StatName,
                    StatValue: m.StatValue
                )).ToList(),
                SubstatCap:        (uint)p.SubstatCap,
                BaseSubstats:      p.BaseSubstats.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value)
            )).ToList();

            var payload = new GearGoblin.Core.ExportPayloadV2(
                V:          SchemaVersion,
                Plugin:     "GearGoblin",
                Version:    typeof(GearsetExporter).Assembly.GetName().Version?.ToString() ?? "?",
                ExportedAt: DateTime.UtcNow.ToString("o"),
                Character:  character,
                Equipped:   pieces
            );

            var json    = JsonSerializer.Serialize(payload, JsonOptions);
            var bytes   = Encoding.UTF8.GetBytes(json);
            var encoded = Prefix + Convert.ToBase64String(bytes);

            ImGui.SetClipboardText(encoded);

            DalamudServices.ChatGui.Print(
                $"[GearGoblin] Exported {equipped.Count} pieces ({encoded.Length} chars). " +
                "Clipboard ready. Paste at tonberrytactics.pages.dev");
            DalamudServices.Log.Info(
                $"Gearset export: job={jobRowId} ({jobAbbr}) level={player.Level} " +
                $"pieces={equipped.Count} jsonBytes={bytes.Length}");

            return true;
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "Gearset export failed");
            DalamudServices.ChatGui.PrintError(
                "[GearGoblin] Export failed. Check /xllog for details.");
            return false;
        }
    }
    private List<GearGoblin.Core.TotalStat> BuildTotalStats(GearGoblin.Core.Materia.StatSnapshot s, IReadOnlyList<EquippedPiece> equipped)
    {
        var mod = GearGoblin.Core.Materia.LevelTable.Get(s.Level);
        int totalGearCap = equipped.Sum(p => p.SubstatCap);
        
        int subCap = mod.Sub + totalGearCap;
        int mainCap = mod.Main + totalGearCap;

        return new List<GearGoblin.Core.TotalStat>
        {
            new("Critical Hit", s.Crit, GearGoblin.Core.Caps.HasNoCap("Critical Hit") ? null : subCap),
            new("Direct Hit", s.DH, GearGoblin.Core.Caps.HasNoCap("Direct Hit") ? null : subCap),
            new("Determination", s.Det, GearGoblin.Core.Caps.HasNoCap("Determination") ? null : mainCap),
            new("Skill Speed", s.SkS, GearGoblin.Core.Caps.HasNoCap("Skill Speed") ? null : subCap),
            new("Spell Speed", s.SpS, GearGoblin.Core.Caps.HasNoCap("Spell Speed") ? null : subCap),
            new("Tenacity", s.Ten, GearGoblin.Core.Caps.HasNoCap("Tenacity") ? null : subCap),
            new("Piety", s.Pie, GearGoblin.Core.Caps.HasNoCap("Piety") ? null : mainCap)
        };
    }
}
