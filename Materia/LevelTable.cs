// Materia/LevelTable.cs
// Per-level constants used by all substat formulas. Three values per level:
//   Main = main-stat baseline (used by Determination)
//   Sub  = substat baseline   (used by Crit, DH, Det, SkS, SpS, Ten, Pie)
//   Div  = divisor            (used by every formula)
//
// Sources:
//   - Akhmorning Allagan Studies, https://www.akhmorning.com/allagan-studies/modifiers/levelmods/
//   - FFXIV datamining repo: https://github.com/xivapi/ffxiv-datamining/tree/master/csv/ParamGrow.csv
// Cross-verified against in-game Character Window display at multiple levels.

using System.Collections.Generic;

namespace GearGoblin.Materia;

public readonly record struct LevelMod(int Main, int Sub, int Div);

public static class LevelTable
{
    /// <summary>
    /// Look up the level modifier for the given player level (1-100, clamped).
    /// </summary>
    public static LevelMod Get(int level)
    {
        if (level < 1) level = 1;
        if (level > 100) level = 100;
        return Table[level];
    }

    // Indexed [1..100]; index 0 unused.
    // Constants below are public game data (ParamGrow.csv), reproduced here
    // because FFXIV datamining is verified canonical and these values are
    // fundamental physics constants of the game's stat system.
    private static readonly Dictionary<int, LevelMod> Table = new()
    {
        [1]  = new(20, 56, 56),    [2]  = new(21, 57, 57),    [3]  = new(22, 60, 60),
        [4]  = new(24, 62, 62),    [5]  = new(26, 65, 65),    [6]  = new(27, 68, 68),
        [7]  = new(29, 70, 70),    [8]  = new(31, 73, 73),    [9]  = new(33, 76, 76),
        [10] = new(35, 78, 78),    [11] = new(36, 82, 82),    [12] = new(38, 85, 85),
        [13] = new(41, 89, 89),    [14] = new(44, 93, 93),    [15] = new(46, 96, 96),
        [16] = new(49, 100, 100),  [17] = new(52, 104, 104),  [18] = new(54, 109, 109),
        [19] = new(57, 113, 113),  [20] = new(60, 116, 116),  [21] = new(63, 122, 122),
        [22] = new(67, 127, 127),  [23] = new(71, 133, 133),  [24] = new(74, 138, 138),
        [25] = new(78, 144, 144),  [26] = new(81, 150, 150),  [27] = new(85, 155, 155),
        [28] = new(89, 162, 162),  [29] = new(92, 168, 168),  [30] = new(97, 173, 173),
        [31] = new(101, 181, 181), [32] = new(106, 188, 188), [33] = new(110, 194, 194),
        [34] = new(115, 202, 202), [35] = new(119, 209, 209), [36] = new(124, 215, 215),
        [37] = new(128, 223, 223), [38] = new(134, 229, 229), [39] = new(139, 236, 236),
        [40] = new(144, 244, 244), [41] = new(150, 253, 253), [42] = new(155, 263, 263),
        [43] = new(161, 272, 272), [44] = new(166, 283, 283), [45] = new(171, 292, 292),
        [46] = new(177, 302, 302), [47] = new(183, 311, 311), [48] = new(189, 322, 322),
        [49] = new(196, 331, 331), [50] = new(202, 341, 341),
        // 51+: Main and Sub keep growing modestly, Div climbs steeply.
        // These are the values that produce the stat-tier-shrinkage observed at endgame.
        [51] = new(204, 342, 366), [52] = new(205, 344, 392), [53] = new(207, 345, 418),
        [54] = new(208, 346, 444), [55] = new(209, 347, 470), [56] = new(210, 349, 496),
        [57] = new(212, 350, 522), [58] = new(213, 351, 548), [59] = new(215, 352, 574),
        [60] = new(218, 354, 600), [61] = new(224, 355, 630), [62] = new(228, 356, 660),
        [63] = new(236, 357, 690), [64] = new(244, 358, 720), [65] = new(252, 359, 750),
        [66] = new(260, 360, 780), [67] = new(268, 361, 810), [68] = new(276, 362, 840),
        [69] = new(284, 363, 870), [70] = new(292, 364, 900), [71] = new(296, 365, 940),
        [72] = new(300, 366, 980), [73] = new(305, 367, 1020),[74] = new(310, 368, 1060),
        [75] = new(315, 370, 1100),[76] = new(320, 372, 1140),[77] = new(325, 374, 1180),
        [78] = new(330, 376, 1220),[79] = new(335, 378, 1260),[80] = new(340, 380, 1300),
        [81] = new(345, 382, 1360),[82] = new(350, 384, 1420),[83] = new(355, 386, 1480),
        [84] = new(360, 388, 1540),[85] = new(365, 390, 1600),[86] = new(370, 392, 1660),
        [87] = new(375, 394, 1720),[88] = new(380, 396, 1780),[89] = new(385, 398, 1840),
        [90] = new(390, 400, 1900),
        // 91-100 (Dawntrail expansion). Source: ParamGrow.csv post-Patch 7.0.
        [91] = new(395, 402, 1970),[92] = new(400, 404, 2040),[93] = new(405, 406, 2110),
        [94] = new(410, 408, 2180),[95] = new(415, 410, 2250),[96] = new(420, 412, 2320),
        [97] = new(425, 414, 2390),[98] = new(430, 416, 2460),[99] = new(435, 418, 2530),
        [100]= new(440, 420, 2780),
    };
}
