using GearGoblin.Services;

namespace GearGoblin.Test {
    public class T {
        public void M() {
            var sheet = DalamudServices.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
            var row = sheet.GetRowOrDefault(1);
            var x = row.Value.BaseParam[0].ValueNullable;
            var y = row.Value.BaseParamValue[0];
            var z = row.Value.LevelItem.Value.CriticalHit;
            var w = row.Value.BaseParamModifier;
        }
    }
}
