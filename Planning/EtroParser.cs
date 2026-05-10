// Planning/EtroParser.cs
// Parses Etro API responses into BisGearset.
//
// Etro response shape (relevant fields):
// {
//   "id": "<uuid>",
//   "name": "VPR 7.5 BiS",
//   "jobAbbrev": "VPR",
//   "weapon": <itemId>,
//   "head":   <itemId>,
//   "body":   <itemId>,
//   "hands":  <itemId>,
//   "legs":   <itemId>,
//   "feet":   <itemId>,
//   "ears":   <itemId>,
//   "neck":   <itemId>,
//   "wrists": <itemId>,
//   "fingerL":<itemId>,
//   "fingerR":<itemId>,
//   "materia": {
//     "<itemId>": { "<slotIdx>": "<materiaItemId>", ... },
//     ...
//   }
// }
//
// We map jobAbbrev to the FFXIV ClassJob row ID via a lookup.

using System.Collections.Generic;
using System.Text.Json;
using GearGoblin.Services;

namespace GearGoblin.Planning;

public static class EtroParser
{
    public static BisGearset? Parse(string json, string sourceUrl)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var name        = root.GetStringOrEmpty("name");
        var description = root.GetStringOrEmpty("description");
        var jobAbbrev   = root.GetStringOrEmpty("jobAbbrev");
        var jobId       = JobAbbrevToId(jobAbbrev);

        var slots = new List<BisSlot>();
        AddSlot(slots, root, "weapon",  EquipSlot.MainHand);
        AddSlot(slots, root, "offHand", EquipSlot.OffHand);
        AddSlot(slots, root, "head",    EquipSlot.Head);
        AddSlot(slots, root, "body",    EquipSlot.Body);
        AddSlot(slots, root, "hands",   EquipSlot.Hands);
        AddSlot(slots, root, "legs",    EquipSlot.Legs);
        AddSlot(slots, root, "feet",    EquipSlot.Feet);
        AddSlot(slots, root, "ears",    EquipSlot.Earring);
        AddSlot(slots, root, "neck",    EquipSlot.Necklace);
        AddSlot(slots, root, "wrists",  EquipSlot.Bracelet);
        AddSlot(slots, root, "fingerL", EquipSlot.RingLeft);
        AddSlot(slots, root, "fingerR", EquipSlot.RingRight);

        return new BisGearset
        {
            Name        = name,
            SourceUrl   = sourceUrl,
            Source      = "etro",
            JobId       = jobId,
            Slots       = slots,
            Description = description,
        };
    }

    private static void AddSlot(List<BisSlot> slots, JsonElement root, string key, EquipSlot slot)
    {
        if (!root.TryGetProperty(key, out var prop)) return;
        if (prop.ValueKind != JsonValueKind.Number) return;
        var itemId = prop.GetUInt32();
        if (itemId == 0) return;

        slots.Add(new BisSlot
        {
            Slot   = slot,
            ItemId = itemId,
            // Item name and ilvl will be looked up via Lumina by the diff layer;
            // Etro's response doesn't include them inline.
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

internal static class JsonShim
{
    public static string GetStringOrEmpty(this JsonElement e, string prop)
    {
        if (!e.TryGetProperty(prop, out var p)) return "";
        return p.ValueKind == JsonValueKind.String ? (p.GetString() ?? "") : "";
    }
}
