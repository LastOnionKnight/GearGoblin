using System.Collections.Generic;

namespace GearGoblin.Services;

public interface IInventoryReader
{
    List<EquippedPiece> ReadEquipped();
    int CalculateAverageItemLevel(IReadOnlyList<EquippedPiece> equipped);
}
