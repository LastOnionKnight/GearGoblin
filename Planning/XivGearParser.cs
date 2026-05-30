using GearGoblin.Core;
using GearGoblin.Core.Materia;
// Planning/XivGearParser.cs
// Parses XIVGear API responses into BisGearset.
//
// XIVGear response is wrapped: {"name": "...", "description": "...",
//   "items": { "Weapon": {...}, "Head": {...}, ... } }
// or a "sheet" with multiple sets, in which case we take the first.
//
// XIVGear slot keys:
//   Weapon, Head, Body, Hand, Legs, Feet,
//   Ears, Neck, Wrist, RingLeft, RingRight
// Each item has { id, materia: [{ id, ... }, ...] }

using System.Collections.Generic;
using System.Text.Json;
using GearGoblin.Services;

namespace GearGoblin.Planning;

public static class XivGearParser
{
    public static BisGearset? Parse(string json, string sourceUrl, bool isSheet)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // For a sheet, the structure has a "sets" array; pick the first.
        // For a single set, the data is at root level.
        JsonElement set;
        if (isSheet && root.TryGetProperty("sets", out var sets) &&
            sets.ValueKind == JsonValueKind.Array && sets.GetArrayLength() > 0)
        {
            set = sets[0];
        }
        else
        {
            set = root;
        }

        var name        = set.GetStringOrEmpty("name");
        var description = set.GetStringOrEmpty("description");

        // Job is sometimes at root, sometimes on the set
        var jobAbbrev = root.GetStringOrEmpty("job");
        if (string.IsNullOrEmpty(jobAbbrev))
            jobAbbrev = set.GetStringOrEmpty("job");
        var jobId = JobAbbrevToId(jobAbbrev);

        var slots = new List<BisSlot>();
        if (set.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Object)
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

        return new BisGearset
        {
            Name        = name,
            SourceUrl   = sourceUrl,
            Source      = "xivgear",
            JobId       = jobId,
            Slots       = slots,
            Description = description,
        };
    }

    private static void AddSlot(List<BisSlot> slots, JsonElement items, string key, EquipSlot slot)
    {
        if (!items.TryGetProperty(key, out var item)) return;
        if (item.ValueKind != JsonValueKind.Object) return;
        if (!item.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.Number) return;

        var itemId = idEl.GetUInt32();
        if (itemId == 0) return;

        var melds = new List<BisMeld>();
        if (item.TryGetProperty("materia", out var matArr) && matArr.ValueKind == JsonValueKind.Array)
        {
            int idx = 0;
            foreach (var mat in matArr.EnumerateArray())
            {
                if (mat.TryGetProperty("id", out var mid) && mid.ValueKind == JsonValueKind.Number)
                {
                    melds.Add(new BisMeld
                    {
                        SlotIndex = idx,
                        StatName  = "", // resolved later via Lumina if needed
                    });
                }
                idx++;
            }
        }

        slots.Add(new BisSlot
        {
            Slot   = slot,
            ItemId = itemId,
            Melds  = melds,
        });
    }

    private static uint JobAbbrevToId(string abbrev) => abbrev?.ToUpperInvariant() switch
    {
        "PLD" => 19, "WAR" => 21, "DRK" => 32, "GNB" => 37,
        "MNK" => 20, "DRG" => 22, "NIN" => 30, "SAM" => 34,
        "RPR" => 39, "VPR" => 41,
        "BRD" => 23, "MCH" => 31, "DNC" => 38,
        "BLM" => 25, "SMN" => 27, "RDM" => 35, "PCT" => 42,
        "WHM" => 24, "SCH" => 28, "AST" => 33, "SGE" => 40,
        _ => 0,
    };
}

