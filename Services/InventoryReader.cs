using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Inventory;
using Lumina.Excel.Sheets;

namespace GearGoblin.Services;

/// <summary>
/// Reads the player's current gear and materia state through Dalamud's inventory API.
/// Designed to be cheap to call — caches nothing internally; UI is responsible for
/// refresh cadence.
/// </summary>
public class InventoryReader
{
    /// <summary>The 13 equippable slots Dalamud exposes via GameInventoryType.EquippedItems.</summary>
    public static readonly EquipSlot[] AllSlots =
    {
        EquipSlot.MainHand, EquipSlot.OffHand, EquipSlot.Head, EquipSlot.Body,
        EquipSlot.Hands,    EquipSlot.Waist,   EquipSlot.Legs, EquipSlot.Feet,
        EquipSlot.Earring,  EquipSlot.Necklace,EquipSlot.Bracelet,
        EquipSlot.RingLeft, EquipSlot.RingRight,
    };

    /// <summary>
    /// Snapshot the equipped gearset. Returns one entry per non-empty slot.
    /// Empty slots are omitted; callers expecting full coverage should fill gaps.
    /// </summary>
    public List<EquippedPiece> ReadEquipped()
    {
        var result = new List<EquippedPiece>(13);
        var items  = DalamudServices.GameInventory.GetInventoryItems(GameInventoryType.EquippedItems);

        foreach (var item in items)
        {
            if (item.ItemId == 0) continue;

            var sheetItem = DalamudServices.DataManager.GetExcelSheet<Item>().GetRowOrDefault(item.ItemId);
            if (sheetItem is null) continue;

            var slot = SlotFromInventoryIndex((int)item.InventorySlot);
            var piece = new EquippedPiece
            {
                Slot          = slot,
                ItemId        = item.ItemId,
                Name          = sheetItem.Value.Name.ExtractText(),
                ItemLevel     = sheetItem.Value.LevelItem.RowId,
                IsHighQuality = item.IsHq,
                Materia       = ReadMateriaFromItem(item),
            };
            result.Add(piece);
        }

        return result;
    }

    /// <summary>Total ilvl across equipped pieces, with 2H weapons counted in both MH and OH.</summary>
    public int CalculateAverageItemLevel(IReadOnlyList<EquippedPiece> equipped)
    {
        if (equipped.Count == 0) return 0;

        int total = 0, slots = 0;
        var bySlot = equipped.ToDictionary(p => p.Slot);

        // Standard 13-slot average; treat a 2H main-hand as filling the off-hand too.
        foreach (var slot in AllSlots)
        {
            if (bySlot.TryGetValue(slot, out var piece))
            {
                total += (int)piece.ItemLevel;
                slots++;
            }
            else if (slot == EquipSlot.OffHand && bySlot.TryGetValue(EquipSlot.MainHand, out var mh))
            {
                // Two-handed weapon path: borrow the MH ilvl for the empty OH slot.
                total += (int)mh.ItemLevel;
                slots++;
            }
        }

        return slots == 0 ? 0 : total / slots;
    }

    private List<MateriaMeld> ReadMateriaFromItem(GameInventoryItem item)
    {
        var melds = new List<MateriaMeld>(5);

        // GameInventoryItem exposes Materia and MateriaGrade as fixed-size 5-element
        // collections, indexed by meld slot. A zero ID means the slot is empty.
        for (int i = 0; i < 5; i++)
        {
            var materiaId = item.Materia[i];
            var grade     = item.MateriaGrade[i];
            if (materiaId == 0) continue;

            var (statName, statValue) = ResolveMateria(materiaId, grade);
            melds.Add(new MateriaMeld
            {
                SlotIndex = i,
                MateriaId = materiaId,
                Grade     = grade,
                StatName  = statName,
                StatValue = statValue,
            });
        }
        return melds;
    }

    private (string name, int value) ResolveMateria(ushort materiaId, byte grade)
    {
        var sheet = DalamudServices.DataManager.GetExcelSheet<Materia>();
        var row = sheet.GetRowOrDefault(materiaId);
        if (row is null) return ("?", 0);

        // Materia rows expose BaseParam (the stat) and Value[grade] (the magnitude).
        var paramRow = row.Value.BaseParam.ValueNullable;
        var statName = paramRow?.Name.ExtractText() ?? "?";
        var value    = grade < row.Value.Value.Count ? row.Value.Value[grade] : (short)0;

        return (statName, value);
    }

    private static EquipSlot SlotFromInventoryIndex(int idx) => idx switch
    {
        0  => EquipSlot.MainHand,
        1  => EquipSlot.OffHand,
        2  => EquipSlot.Head,
        3  => EquipSlot.Body,
        4  => EquipSlot.Hands,
        6  => EquipSlot.Legs,
        7  => EquipSlot.Feet,
        8  => EquipSlot.Earring,
        9  => EquipSlot.Necklace,
        10 => EquipSlot.Bracelet,
        11 => EquipSlot.RingLeft,
        12 => EquipSlot.RingRight,
        _  => EquipSlot.Unknown,
    };
}

public enum EquipSlot
{
    Unknown,
    MainHand, OffHand,
    Head, Body, Hands, Waist, Legs, Feet,
    Earring, Necklace, Bracelet,
    RingLeft, RingRight,
}

public class EquippedPiece
{
    public EquipSlot Slot          { get; set; }
    public uint      ItemId        { get; set; }
    public string    Name          { get; set; } = "";
    public uint      ItemLevel     { get; set; }
    public bool      IsHighQuality { get; set; }
    public List<MateriaMeld> Materia { get; set; } = new();
}

public class MateriaMeld
{
    public int    SlotIndex { get; set; }
    public ushort MateriaId { get; set; }
    public byte   Grade     { get; set; }
    public string StatName  { get; set; } = "";
    public int    StatValue { get; set; }
}
