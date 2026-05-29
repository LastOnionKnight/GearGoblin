using GearGoblin.Planning;

namespace GearGoblin.Services;

public interface IGearsetImporter
{
    PlanImportResult ImportFromClipboard();
    PlanImportResult ImportFromString(string wireString);
}
