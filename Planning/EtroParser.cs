using System;
using System.Collections.Generic;
using System.Text.Json;
using GearGoblin.Core;

namespace GearGoblin.Planning;

/// <summary>Parses an Etro gearset API response into the neutral BiS model.</summary>
public static class EtroParser
{
    public static BisGearset? Parse(string json, string sourceUrl)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var name = root.GetStringOrEmpty("name");
        var description = root.GetStringOrEmpty("description");
        var jobAbbrev = root.GetStringOrEmpty("jobAbbrev");

        var slots = new List<BisSlot>();
        AddSlot(slots, root, "weapon",   EquipSlot.MainHand);
        AddSlot(slots, root, "offHand",  EquipSlot.OffHand);
        AddSlot(slots, root, "head",     EquipSlot.Head);
        AddSlot(slots, root, "body",     EquipSlot.Body);
        AddSlot(slots, root, "hands",    EquipSlot.Hands);
        AddSlot(slots, root, "legs",     EquipSlot.Legs);
        AddSlot(slots, root, "feet",     EquipSlot.Feet);
        AddSlot(slots, root, "ears",     EquipSlot.Earring);
        AddSlot(slots, root, "neck",     EquipSlot.Necklace);
        AddSlot(slots, root, "wrists",   EquipSlot.Bracelet);
        AddSlot(slots, root, "fingerL",  EquipSlot.RingLeft);
        AddSlot(slots, root, "fingerR",  EquipSlot.RingRight);

        return new BisGearset
        {
            Name = name,
            SourceUrl = sourceUrl,
            Source = "etro",
            JobId = JobAbbrevToId(jobAbbrev),
            Slots = slots,
            Description = description,
        };
    }

    private static void AddSlot(List<BisSlot> slots, JsonElement root, string key, EquipSlot slot)
    {
        if (!root.TryGetProperty(key, out var prop) || prop.ValueKind != JsonValueKind.Number)
            return;

        var itemId = prop.GetUInt32();
        if (itemId == 0)
            return;

        var melds = new List<BisMeld>();
        bool meldDataComplete = TryResolveMelds(root, itemId, melds);
        slots.Add(BisMetadataResolver.HydrateItem(slot, itemId, melds, meldDataComplete));
    }

    /// <summary>
    /// Etro exposes materia as an item-id keyed object whose nested keys are
    /// meld-slot indices and values are materia item IDs. If that section is
    /// absent, mark the slot unresolved rather than assuming "no melds".
    /// </summary>
    private static bool TryResolveMelds(JsonElement root, uint itemId, List<BisMeld> melds)
    {
        if (!root.TryGetProperty("materia", out var materiaRoot) || materiaRoot.ValueKind != JsonValueKind.Object)
            return false;

        if (!materiaRoot.TryGetProperty(itemId.ToString(), out var itemMateria) ||
            itemMateria.ValueKind != JsonValueKind.Object)
        {
            // Materia object exists and has no entry for this item: treat as a
            // known empty target for this slot.
            return true;
        }

        bool complete = true;
        foreach (var property in itemMateria.EnumerateObject())
        {
            if (!int.TryParse(property.Name, out var slotIndex))
            {
                complete = false;
                continue;
            }

            if (!TryGetMateriaItemId(property.Value, out var materiaItemId))
                continue;

            var resolved = BisMetadataResolver.ResolveMateriaItem(materiaItemId, slotIndex);
            if (resolved is null)
                complete = false;
            else
                melds.Add(resolved);
        }

        return complete;
    }

    private static bool TryGetMateriaItemId(JsonElement element, out uint itemId)
    {
        itemId = 0;

        if (element.ValueKind == JsonValueKind.Number && element.TryGetUInt32(out var numeric))
        {
            itemId = numeric;
            return numeric != 0;
        }

        if (element.ValueKind == JsonValueKind.String &&
            uint.TryParse(element.GetString(), out var parsed) && parsed != 0)
        {
            itemId = parsed;
            return true;
        }

        return false;
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

internal static class JsonShim
{
    public static string GetStringOrEmpty(this JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return string.Empty;

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }
}
