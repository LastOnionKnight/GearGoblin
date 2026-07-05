// UI/CharacterTab.cs
//
// The new landing tab for /tt. Consolidates four pieces of identity
// information into one cohesive surface:
//   1. Hero region    — name, class, level, FC tag, iLvl pill
//   2. Stats strip    — substats with derived effects + next-tier hints
//   3. Materia advisor — top 3 recommendations (or empty-state line)
//   4. Gear table     — 12 equipped slots + Soul Crystal
//
// Long-term goal: this tab replaces the StatusPanelInjector entirely.
// The injection path stays in v0.6.5.x for backward compat but is
// deprecated in v0.6.6 and removed in v0.7.0. Until then, the Character
// tab and the native-panel injection coexist — the tab is the "new way,"
// the injector is the "fossil." We control rendering inside our own
// window, so the cloned-cell-overflow, lifecycle-event-miss, and
// CharacterPanelRefined-collision class of bugs cannot occur here.
//
// THIS IS A SKELETON. v0.6.6 alpha — visual fidelity is intentionally
// low. Each section method below is wired to its real data source and
// renders the content in plain ImGui primitives so the tab is functional
// from day one. The visual polish (typography stack, section-head rule
// lines, stat-card chrome, advisor row ranking, gear table stripes)
// comes in subsequent passes, layering Claude Design v0.2.0's design
// system onto this scaffolding section by section.
//
// Reference deliverable: character-tab/ (Claude Design v0.2.0).
//   - README.md             : 12 ImGui port flags
//   - styles.css            : design tokens (using existing TT :root vars)
//   - Character.html        : composed page, runnable in browser
//   - components/*.jsx      : per-section visual targets

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using GearGoblin.Materia;
using GearGoblin.Core.Materia;
using GearGoblin.Services;
using GearGoblin.Theme;

namespace GearGoblin.UI;

public static class CharacterTab
{
    public static void Draw(Plugin plugin, IPlayerCharacter player)
    {
        var snap = StatReader.ReadCurrent();
        if (snap is null)
        {
            ImGui.TextDisabled("Player data unavailable. Log in to see your Character panel.");
            return;
        }

        var s = snap.Value;
        var profile = JobProfiles.GetOrDefault(s.JobId);
        var mod = LevelTable.Get(s.Level);

        var equipped = plugin.Inventory.ReadEquipped();
        var ilvl = equipped.Count > 0 ? plugin.Inventory.CalculateAverageItemLevel(equipped) : 0;

        Theme.TtChrome.Eyebrow(plugin.Fonts, "Character · Substat Profile");
        Theme.TtChrome.Quip(plugin.Fonts, "Derived from equipped gear at i" + ilvl + ". Cap marks show the next relevant breakpoint — fill past it is wasted budget.");
        ImGui.Spacing();
        ImGui.Spacing();

        DrawGauges(plugin, s, profile, mod);

        ImGui.Spacing();
        ImGui.Spacing();
        
        using (plugin.Fonts.Pixel.PushOrNull())
        {
            ImGui.TextColored(Theme.TtChrome.CobaltBright, $"{Theme.TtChrome.GlyphEyebrow} AVERAGE ITEM LEVEL");
        }
        ImGui.Separator();
        ImGui.Spacing();
        
        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
        {
            ImGui.TextColored(Theme.TtChrome.GoldBright, ilvl.ToString());
            ImGui.SameLine();
            ImGui.TextColored(Theme.TtChrome.FgMuted, $" · {equipped.Count} / 13 slots filled ·");
            ImGui.SameLine();
        }
        ImGui.TextColored(Theme.TtChrome.FgFaint, "Materia Advisor injected below by Tonberry Tactics. Substat derivations provided by CharacterPanelRefined.");
    }

    private static void DrawGauges(Plugin plugin, StatSnapshot s, JobProfile profile, LevelMod mod)
    {
        // Draw the gauges similar to the stats strip
        if (profile.Role == Role.Crafter || profile.Role == Role.Gatherer)
        {
            ImGui.TextDisabled("Battle stats not applicable for this class.");
            return;
        }

        // Gauge caps are the current-tier substat ceilings used as the fill
        // reference. Fill and state are derived from the player's REAL stats
        // (s.*), never from a fixed mock percentage.
        DrawGauge(plugin, "Critical Hit", s.Crit, 3174, Theme.TtChrome.MatCrit,
            "Highest-value substat for most jobs — scales both rate and multiplier.");
        DrawGauge(plugin, "Determination", s.Det, 3000, Theme.TtChrome.MatDet,
            "No breakpoints — smooth linear value all the way up.");

        if (UsesSks(profile))
        {
            DrawGauge(plugin, "Skill Speed", s.SkS, 904, Theme.TtChrome.MatSks,
                "Tune to a GCD tier; points spent between tiers are inert.");
        }
        else if (UsesSps(profile))
        {
            DrawGauge(plugin, "Spell Speed", s.SpS, 904, Theme.TtChrome.MatSps,
                "Tune to a GCD tier; points spent between tiers are inert.");
        }

        DrawGauge(plugin, "Direct Hit", s.DH, 2030, Theme.TtChrome.MatDh,
            "Raises hit frequency; no breakpoints, flat value per point.");

        if (profile.Role == Role.Tank)
            DrawGauge(plugin, "Tenacity", s.Ten, 2000, Theme.TtChrome.MatTen,
                "Tank substat — outgoing damage plus mitigation.");
        else if (profile.Role == Role.Healer)
            DrawGauge(plugin, "Piety", s.Pie, 2000, Theme.TtChrome.MatPie,
                "Healer MP sustain — no combat value beyond regen.");
    }

    private static void DrawGauge(Plugin plugin, string name, int val, int cap, Vector4 accent, string note)
    {
        // Real fill: the player's actual substat over the tier ceiling.
        float ratio = cap > 0 ? Math.Clamp(val / (float)cap, 0f, 1f) : 0f;
        bool over = val > cap;
        bool near = !over && ratio >= 0.90f;
        var fillColor = over ? Theme.TtChrome.Over : (near ? Theme.TtChrome.GoldBright : accent);

        Theme.TtChrome.BeginPanel("gauge_" + name, 90f);

        // Top row: name + real value / cap + state pill
        using (plugin.Fonts.CinzelEmphasis.PushOrNull())
        {
            ImGui.TextColored(Theme.TtChrome.Fg, name);
        }
        ImGui.SameLine();
        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
        {
            ImGui.TextColored(Theme.TtChrome.GoldBright, val.ToString());
            ImGui.SameLine(0, 4);
            ImGui.TextColored(Theme.TtChrome.FgFaint, $"/ {cap} cap");
        }
        ImGui.SameLine(0, 10);
        var stateLabel = over ? "OVER CAP" : (near ? "NEAR CAP" : "OK");
        var stateColor = over ? Theme.TtChrome.Over : (near ? Theme.TtChrome.Warn : Theme.TtChrome.Ok);
        Theme.TtChrome.PillBox(stateLabel, stateColor);

        // Track
        ImGui.Spacing();
        var p = ImGui.GetCursorScreenPos();
        var w = ImGui.GetContentRegionAvail().X;
        var h = 10f;
        var drawList = ImGui.GetWindowDrawList();

        // bg
        drawList.AddRectFilled(p, new Vector2(p.X + w, p.Y + h), ImGui.GetColorU32(Theme.TtChrome.Sink), 5f);
        drawList.AddRect(p, new Vector2(p.X + w, p.Y + h), ImGui.GetColorU32(Theme.TtChrome.LineSoft), 5f);

        // fill — real ratio, state-driven color
        var fillW = w * ratio;
        drawList.AddRectFilled(p, new Vector2(p.X + fillW, p.Y + h), ImGui.GetColorU32(fillColor), 5f);

        // cap mark — the tier ceiling sits at the right edge (100%)
        var capX = p.X + w - 1f;
        drawList.AddRectFilled(new Vector2(capX, p.Y - 2), new Vector2(capX + 2, p.Y + h + 2), ImGui.GetColorU32(Theme.TtChrome.GoldBright));

        ImGui.Dummy(new Vector2(w, h));

        // Note
        ImGui.Spacing();
        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
        {
            ImGui.TextColored(Theme.TtChrome.FgMuted, note);
        }

        Theme.TtChrome.EndPanel();
        ImGui.Spacing();
    }

    internal static bool WantsMateriaTabFocus;

    private static bool UsesSks(JobProfile p) =>
        p.Role == Role.Tank || p.Role == Role.MeleeDps || p.Role == Role.PhysicalRangedDps;

    private static bool UsesSps(JobProfile p) =>
        p.Role == Role.MagicalRangedDps || p.Role == Role.Healer;
}

