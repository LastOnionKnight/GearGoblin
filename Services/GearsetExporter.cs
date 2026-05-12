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
public sealed class GearsetExporter
{
    private const string Prefix        = "GG-EXPORT:v1:";
    private const int    SchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented        = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly InventoryReader inventory;

    public GearsetExporter(InventoryReader inventory)
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

        try
        {
            var jobRowId = player.ClassJob.RowId;
            var jobAbbr  = player.ClassJob.Value.Abbreviation.ExtractText();

            var character = new ExportCharacterV1(
                Job:              jobRowId,
                JobAbbreviation:  jobAbbr,
                Level:            (int)player.Level,
                AverageItemLevel: inventory.CalculateAverageItemLevel(equipped)
            );

            var pieces = equipped.Select(p => new ExportPieceV1(
                Slot:              p.Slot.ToString(),
                ItemId:            p.ItemId,
                Name:              p.Name,
                ItemLevel:         p.ItemLevel,
                IsHighQuality:     p.IsHighQuality,
                MateriaSlotCount:  p.MateriaSlotCount,
                IsOvermeldAllowed: p.IsOvermeldAllowed,
                Materia:           p.Materia.Select(m => new ExportMateriaV1(
                    SlotIndex: m.SlotIndex,
                    MateriaId: m.MateriaId,
                    Grade:     m.Grade,
                    StatName:  m.StatName,
                    StatValue: m.StatValue
                )).ToList()
            )).ToList();

            var payload = new ExportPayloadV1(
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

    // =========================================================================
    // Wire-format DTOs. These are the exact shapes that serialize to JSON.
    // Schema is versioned via the GG-EXPORT:v1: prefix; new schema versions
    // get new record types (ExportPayloadV2, etc.) rather than mutating these.
    // =========================================================================

    private sealed record ExportPayloadV1(
        int                  V,
        string               Plugin,
        string               Version,
        string               ExportedAt,
        ExportCharacterV1    Character,
        List<ExportPieceV1>  Equipped
    );

    private sealed record ExportCharacterV1(
        uint   Job,
        string JobAbbreviation,
        int    Level,
        int    AverageItemLevel
    );

    private sealed record ExportPieceV1(
        string                  Slot,
        uint                    ItemId,
        string                  Name,
        uint                    ItemLevel,
        bool                    IsHighQuality,
        byte                    MateriaSlotCount,
        bool                    IsOvermeldAllowed,
        List<ExportMateriaV1>   Materia
    );

    private sealed record ExportMateriaV1(
        int    SlotIndex,
        ushort MateriaId,
        byte   Grade,
        string StatName,
        int    StatValue
    );
}
