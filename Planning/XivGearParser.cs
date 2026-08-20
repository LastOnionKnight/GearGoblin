using System;
using System.Collections.Generic;
using System.Text.Json;
using GearGoblin.Core;

namespace GearGoblin.Planning;

/// <summary>
/// Parses XIVGear's current /basedata response (SheetStatsExport) into the
/// source-neutral BiS model. The endpoint returns a sheet shape even when the
/// source URL identifies a single set.
/// </summary>
public static class XivGearParser
{
    public static BisGearset? Parse(string json, string sourceUrl)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var set = SelectSet(root);
        if (set is null)
            return null;

        var setValue = set.Value;
        var name = setValue.GetStringOrEmpty("name");
        var description = setValue.GetStringOrEmpty("description");

        var jobAbbrev = setValue.GetStringOrEmpty("jobOverride");
        if (string.IsNullOrEmpty(jobAbbrev))
            jobAbbrev = root.GetStringOrEmpty("job");
        if (string.IsNullOrEmpty(jobAbbrev))
            jobAbbrev = setValue.GetStringOrEmpty("job");

        var slots = new List<BisSlot>();
        if (setValue.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Object)
        {
            AddSlot(slots, items, "Weapon",    EquipSlot.MainHand);
            AddSlot(slots, items, "OffHand",   EquipSlot.OffHand);
            AddSlot(slots, items, "Head",      EquipSlot.Head);
            AddSlot(slots, items, "Body",      EquipSlot.Body);
            AddSlot(slots, items, "Hand",      EquipSlot.Hands);
            AddSlot(slots, items, "Legs",      EquipSlot.Legs);
            AddSlot(slots, items, "Feet",      EquipSlot.Feet);
            AddSlot(slots, items, "Ears",      EquipSlot.Earring);
            AddSlot(slots, items, "Neck",      EquipSlot.Necklace);
            AddSlot(slots, items, "Wrist",     EquipSlot.Bracelet);
            AddSlot(slots, items, "RingLeft",  EquipSlot.RingLeft);
            AddSlot(slots, items, "RingRight", EquipSlot.RingRight);
        }

        uint? foodItemId = null;
        if (setValue.TryGetProperty("food", out var food) &&
            food.ValueKind == JsonValueKind.Number &&
            food.TryGetUInt32(out var foodId) && foodId != 0)
        {
            foodItemId = foodId;
        }

        return new BisGearset
        {
            Name = name,
            SourceUrl = sourceUrl,
            Source = "xivgear",
            JobId = JobAbbrevToId(jobAbbrev),
            Slots = slots,
            Description = description,
            FoodItemId = foodItemId,
        };
    }

    private static JsonElement? SelectSet(JsonElement root)
    {
        if (root.TryGetProperty("sets", out var sets) && sets.ValueKind == JsonValueKind.Array)
        {
            foreach (var candidate in sets.EnumerateArray())
            {
                if (candidate.ValueKind != JsonValueKind.Object)
                    continue;

                bool isSeparator = candidate.TryGetProperty("isSeparator", out var sep) &&
                                   sep.ValueKind == JsonValueKind.True;
                if (!isSeparator)
                    return candidate;
            }
            return null;
        }

        // Backward-compatible fallback for old shortlink/single-set payloads.
        return root.ValueKind == JsonValueKind.Object ? root : null;
    }

    private static void AddSlot(List<BisSlot> slots, JsonElement items, string key, EquipSlot slot)
    {
        if (!items.TryGetProperty(key, out var item) || item.ValueKind != JsonValueKind.Object)
            return;
        if (!item.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.Number)
            return;

        var itemId = idEl.GetUInt32();
        if (itemId == 0)
            return;

        var melds = new List<BisMeld>();
        bool meldDataComplete = item.TryGetProperty("materia", out var materia) &&
                                materia.ValueKind == JsonValueKind.Array;

        if (meldDataComplete)
        {
            int slotIndex = 0;
            foreach (var entry in materia.EnumerateArray())
            {
                if (TryReadMateriaItemId(entry, out var materiaItemId))
                {
                    var resolved = BisMetadataResolver.ResolveMateriaItem(materiaItemId, slotIndex);
                    if (resolved is null)
                        meldDataComplete = false;
                    else
                        melds.Add(resolved);
                }
                slotIndex++;
            }
        }

        slots.Add(BisMetadataResolver.HydrateItem(slot, itemId, melds, meldDataComplete));
    }

    private static bool TryReadMateriaItemId(JsonElement entry, out uint materiaItemId)
    {
        materiaItemId = 0;

        if (entry.ValueKind == JsonValueKind.Null || entry.ValueKind == JsonValueKind.Undefined)
            return false;

        JsonElement idElement;
        if (entry.ValueKind == JsonValueKind.Object)
        {
            if (!entry.TryGetProperty("id", out idElement))
                return false;
        }
        else
        {
            idElement = entry;
        }

        if (idElement.ValueKind != JsonValueKind.Number || !idElement.TryGetInt64(out var rawId))
            return false;

        // XIVGear uses -1/null for an empty materia slot.
        if (rawId <= 0 || rawId > uint.MaxValue)
            return false;

        materiaItemId = (uint)rawId;
        return true;
    }

    private static uint JobAbbrevToId(string? abbrev) => abbrev?.ToUpperInvariant() switch
    {
        "CRP" => 8, "BSM" => 9, "ARM" => 10, "GSM" => 11,
        "LTW" => 12, "WVR" => 13, "ALC" => 14, "CUL" => 15,
        "MIN" => 16, "BTN" => 17, "FSH" => 18,
        "PLD" => 19, "MNK" => 20, "WAR" => 21, "DRG" => 22,
        "BRD" => 23, "WHM" => 24, "BLM" => 25, "SMN" => 27,
        "SCH" => 28, "NIN" => 30, "MCH" => 31, "DRK" => 32,
        "AST" => 33, "SAM" => 34, "RDM" => 35, "GNB" => 37,
        "DNC" => 38, "RPR" => 39, "SGE" => 40, "VPR" => 41,
        "PCT" => 42,
        _ => 0,
    };
}
