using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Inventory;
using Lumina.Excel.Sheets;

// Disambiguate the Lumina materia sheet type from our GearGoblin.Materia namespace.
using LuminaMateria = Lumina.Excel.Sheets.Materia;

namespace GearGoblin.Services;

/// <summary>
/// Reads the player's current gear and materia state through Dalamud's inventory API.
/// Designed to be cheap to call — caches nothing internally; UI is responsible for
/// refresh cadence.
/// </summary>
public class InventoryReader
{
    public static readonly EquipSlot[] AllSlots =
    {
        EquipSlot.MainHand, EquipSlot.OffHand, EquipSlot.Head, EquipSlot.Body,
        EquipSlot.Hands,    EquipSlot.Legs,    EquipSlot.Feet,
        EquipSlot.Earring,  EquipSlot.Necklace,EquipSlot.Bracelet,
        EquipSlot.RingLeft, EquipSlot.RingRight,
    };

    /// <summary>
    /// Snapshot the equipped gearset. Returns one entry per non-empty slot.
    ///
    /// Bug D fix (v0.3.1): we no longer guess the slot from the inventory array index.
    /// The array layout changed between Dalamud versions (the Waist slot was deprecated
    /// and removed, which shifted Legs→5, Feet→6, accessories→7-11). Instead of tracking
    /// that mapping, we read EquipSlotCategory from the Item itself: that field on the
    /// Lumina Item row is what the game uses to decide where an item can be equipped,
    /// and it never changes shape. We also fall back to the array index for the few
    /// pieces (weapons with no category set) where the item doesn't carry a category.
    /// </summary>
    public List<EquippedPiece> ReadEquipped()
    {
        var result = new List<EquippedPiece>(13);
        var items  = DalamudServices.GameInventory.GetInventoryItems(GameInventoryType.EquippedItems);

        // The first ring slot we see goes into RingLeft, the second into RingRight.
        // EquipSlotCategory groups both rings under the same category, so we need
        // a counter to distinguish them in iteration order.
        int ringsSeen = 0;

        foreach (var item in items)
        {
            if (item.ItemId == 0) continue;

            // v0.6.5 — Strip HQ offset before sheet lookup. Dalamud's
            // GameInventoryItem.ItemId carries the HQ offset (+1_000_000)
            // for high-quality items; the Item Excel sheet only has rows
            // for base IDs. Crafted gear that real players use is almost
            // always HQ — without this strip, every HQ piece returned
            // null from GetRowOrDefault and was silently dropped via the
            // sheetItem null-check below, which is why pre-v0.6.5 users
            // saw 3-7 piece exports (vendor-only gear) instead of the
            // full 13-piece set. IsHighQuality continues to carry the
            // HQ flag on the wire so the web knows the quality state.
            var baseItemId = item.ItemId >= 1_000_000
                ? item.ItemId - 1_000_000
                : item.ItemId;

            var sheetItem = DalamudServices.DataManager.GetExcelSheet<Item>().GetRowOrDefault(baseItemId);
            if (sheetItem is null) continue;

            var slotCategory = sheetItem.Value.EquipSlotCategory.RowId;
            var slot         = SlotFromCategory(slotCategory, ref ringsSeen);

            // Fall back to index-based mapping if the item has no usable category.
            if (slot == EquipSlot.Unknown)
                slot = SlotFromInventoryIndex((int)item.InventorySlot);

            var piece = new EquippedPiece
            {
                Slot              = slot,
                ItemId            = baseItemId,
                Name              = sheetItem.Value.Name.ExtractText(),
                ItemLevel         = sheetItem.Value.LevelItem.RowId,
                IsHighQuality     = item.IsHq,
                MateriaSlotCount  = sheetItem.Value.MateriaSlotCount,
                IsOvermeldAllowed = sheetItem.Value.IsAdvancedMeldingPermitted,
                Materia           = ReadMateriaFromItem(item),
            };
            result.Add(piece);
        }

        return result;
    }

    public int CalculateAverageItemLevel(IReadOnlyList<EquippedPiece> equipped)
    {
        if (equipped.Count == 0) return 0;

        int total = 0, slots = 0;
        // Defensive: skip duplicate-slot entries rather than crash if the slot
        // mapping ever returns two pieces in the same slot.
        var bySlot = new Dictionary<EquipSlot, EquippedPiece>();
        foreach (var p in equipped)
        {
            if (bySlot.ContainsKey(p.Slot)) continue;
            bySlot[p.Slot] = p;
        }

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
        var sheet = DalamudServices.DataManager.GetExcelSheet<LuminaMateria>();
        var row = sheet.GetRowOrDefault(materiaId);
        if (row is null) return ("?", 0);

        var paramRow = row.Value.BaseParam.ValueNullable;
        var statName = paramRow?.Name.ExtractText() ?? "?";
        var value    = grade < row.Value.Value.Count ? row.Value.Value[grade] : (short)0;

        return (statName, value);
    }

    /// <summary>
    /// Map the Item sheet's EquipSlotCategory row ID to our EquipSlot enum.
    /// EquipSlotCategory IDs are stable game data — the row indices haven't changed since 2.0.
    /// </summary>
    /// <param name="categoryId">EquipSlotCategory RowId from Item sheet.</param>
    /// <param name="ringsSeen">Counter incremented when we identify a ring; first ring is Left, second is Right.</param>
    private static EquipSlot SlotFromCategory(uint categoryId, ref int ringsSeen)
    {
        return categoryId switch
        {
            1  => EquipSlot.MainHand,  // Main Hand
            2  => EquipSlot.OffHand,   // Off Hand
            3  => EquipSlot.Head,
            4  => EquipSlot.Body,
            5  => EquipSlot.Hands,
            6  => EquipSlot.Waist,     // deprecated slot — kept for compat
            7  => EquipSlot.Legs,
            8  => EquipSlot.Feet,
            9  => EquipSlot.Earring,
            10 => EquipSlot.Necklace,
            11 => EquipSlot.Bracelet,
            12 => AssignRing(ref ringsSeen),
            13 => EquipSlot.MainHand,  // Two-handed weapon (occupies both MH+OH)
            // 14-17 are "combination" slot categories (body+head+hands as one piece, etc.)
            // We don't try to map these by category — they fall back to inventory index,
            // where the game still reports them in a single slot. Mapping them to Body
            // here caused duplicate-Body crashes when both a combo item and a real Body
            // piece were equipped on different jobs in the player's gearset history.
            // 18+ are exotic/soul-stone categories we don't display
            _  => EquipSlot.Unknown,
        };

        static EquipSlot AssignRing(ref int ringsSeen)
        {
            var slot = ringsSeen == 0 ? EquipSlot.RingLeft : EquipSlot.RingRight;
            ringsSeen++;
            return slot;
        }
    }

    /// <summary>
    /// Fallback mapping based on Dalamud's GameInventory array index order.
    /// Updated for current Dalamud: Waist was removed, so legs is index 5, not 6.
    /// Used only when EquipSlotCategory is missing or unrecognized.
    /// </summary>
    private static EquipSlot SlotFromInventoryIndex(int idx) => idx switch
    {
        0  => EquipSlot.MainHand,
        1  => EquipSlot.OffHand,
        2  => EquipSlot.Head,
        3  => EquipSlot.Body,
        4  => EquipSlot.Hands,
        5  => EquipSlot.Legs,
        6  => EquipSlot.Feet,
        7  => EquipSlot.Earring,
        8  => EquipSlot.Necklace,
        9  => EquipSlot.Bracelet,
        10 => EquipSlot.RingLeft,
        11 => EquipSlot.RingRight,
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
    public EquipSlot Slot              { get; set; }
    public uint      ItemId            { get; set; }
    public string    Name              { get; set; } = "";
    public uint      ItemLevel         { get; set; }
    public bool      IsHighQuality     { get; set; }

    /// <summary>Number of guaranteed materia slots the item ships with (0-2).</summary>
    public byte      MateriaSlotCount  { get; set; }

    /// <summary>Whether the player can overmeld additional slots beyond the guaranteed count.</summary>
    public bool      IsOvermeldAllowed { get; set; }

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
