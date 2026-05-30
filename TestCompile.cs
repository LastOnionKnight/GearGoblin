using GearGoblin.Services;
using Lumina.Excel.Sheets;
using System;

namespace GearGoblin.Test {
    public class T {
        public void M() {
            var sheet = DalamudServices.DataManager.GetExcelSheet<Item>();
            foreach (var item in sheet) {
                if (item.Name.ExtractText().Contains("Phantom Rapier Umbrae")) {
                    Console.WriteLine($"Found {item.Name.ExtractText()}");
                    Console.WriteLine($"IsHQ: {item.CanBeHq}");
                    for (int i = 0; i < 6; i++) {
                        var p = item.BaseParam[i].ValueNullable;
                        if (p != null && p.Value.RowId != 0) {
                            Console.WriteLine($"Param[{i}]: {p.Value.RowId} Value: {item.BaseParamValue[i]}");
                        }
                    }
                    var ilvl = item.LevelItem.ValueNullable;
                    if (ilvl != null) {
                        Console.WriteLine($"iLvl Cap: {ilvl.Value.CriticalHit}");
                        Console.WriteLine($"BaseParamMod: {item.BaseParamModifier}");
                    }
                }
            }
        }
    }
}
