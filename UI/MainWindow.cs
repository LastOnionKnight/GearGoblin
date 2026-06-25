// UI/MainWindow.cs
//
// v0.4.7 adds:
//   Feedback     — pre-filled GitHub issue URL + clipboard fallback for
//                  Discord/DM. Category radio drives label + title prefix;
//                  diagnostic block auto-attaches when the checkbox is on.
//                  No webhooks, no analytics, no auto-submit.
//
// Tab order (v0.4.7):
//   Character | Quick Start | Plan | Materia | Settings | Diagnostics | Feedback | About
//
// v0.4.6 tabs (still here):
//   Quick Start  — first-time-user workflow guide for the export–optimize–
//                  import loop. Always the first tab.
//   Settings     — every Configuration toggle as a checkbox. Per-stat
//                  derivation toggles grey out when CPR is detected and
//                  ForceDerivationsOverCpr is off — visual feedback for
//                  what's actually injecting.
//   Diagnostics  — live read of StatusPanelInjector.DiagnosticSnapshot,
//                  "Force Reinject" button, "Copy /goblininfo" button.
//                  Exists because the v0.4.5 advisor-visibility bug had
//                  no observable surface — now it does.

using System;
using System.Diagnostics;
using System.Net;
using System.Numerics;
using System.Reflection;
using System.Text;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using GearGoblin.Services;
using GearGoblin.Theme;          // v0.6.0 — for FontPushExtensions.PushOrNull()

namespace GearGoblin.UI;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private static readonly string s_versionString = ResolveVersion();

    // v0.6.5.2 — Refresh button timestamp. Tracks when the user last clicked
    // Refresh so we can fade out a "✓ refreshed" confirmation label over
    // the following 2 seconds. Static because there's only one MainWindow
    // instance per plugin load; lives at class scope so the fade survives
    // across draw frames without needing a per-instance field.
    private static DateTime s_lastRefreshTime = DateTime.MinValue;
    private static string? _aboutChangelog = null;

    // v0.4.7 Feedback tab state. Persistent across frames so typing isn't lost.
    // Buffer is byte[] (not string) to match Dalamud.Bindings.ImGui's
    // InputTextMultiline signature: (label, byte[], Vector2 size, ImGuiInputTextFlags).
    // We read via UTF-8 decode on demand and clear via Array.Clear.
    private readonly byte[] feedbackBuf = new byte[4096];
    private int    feedbackCategory    = 0;      // index into FeedbackCategories
    private bool   feedbackIncludeDiag = true;
    private string feedbackLastAction  = string.Empty;

    private static readonly string[] FeedbackCategories =
        { "Bug / glitch", "Feature idea", "Confusion / unclear", "Just saying hi" };
    private static readonly string[] FeedbackLabels =
        { "bug", "enhancement", "ux", "feedback" };
    private const string FeedbackRepoUrl = "https://github.com/LastOnionKnight/GearGoblin";

    /// <summary>
    /// Decode the feedback buffer to a trimmed string. The buffer is
    /// zero-padded, so TrimEnd('\0') gives us just the user-typed content.
    /// </summary>
    private string ReadFeedbackText() =>
        System.Text.Encoding.UTF8.GetString(feedbackBuf).TrimEnd('\0');

    private void ClearFeedbackBuffer() =>
        Array.Clear(feedbackBuf, 0, feedbackBuf.Length);

    public MainWindow(Plugin plugin) : base("Tonberry Tactics###GearGoblinMain")
    {
        // v0.4.7.1 "Brand Convergence": title text changes to the new product
        // name but the ImGui ID suffix (###GearGoblinMain) deliberately stays
        // the same. ImGui keys window state (size, position, docking) by the
        // string after ###; preserving it means users keep their saved layout
        // when they update from v0.4.7.
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(640, 460),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        Size      = new Vector2(820, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var player = DalamudServices.ObjectTable.LocalPlayer;
        if (player is null)
        {
            ImGui.TextDisabled("Not logged in.");
            return;
        }

        // v1.0 — TtChrome wraps the entire window
        Theme.TtChrome.Push();
        try
        {
            DrawBody(player);
        }
        finally
        {
            Theme.TtChrome.Pop();
        }
    }

    private void DrawBody(Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter player)
    {
        DrawIdentityBar(player);
        ImGui.Separator();
        
        // Main content area - leave space for footer
        ImGui.BeginChild("##content", new Vector2(0, -36f), false, ImGuiWindowFlags.None);

        if (ImGui.BeginTabBar("##goblintabs"))
        {
            if (ImGui.BeginTabItem("Quick Start"))
            {
                DrawQuickStart();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Character"))
            {
                CharacterTab.Draw(plugin, player);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Plan"))
            {
                PlanTab.Draw(plugin);
                ImGui.EndTabItem();
            }
            
            var materiaFlags = ImGuiTabItemFlags.None;
            if (CharacterTab.WantsMateriaTabFocus)
            {
                materiaFlags = ImGuiTabItemFlags.SetSelected;
                CharacterTab.WantsMateriaTabFocus = false;
            }
            if (ImGui.BeginTabItem("Materia", materiaFlags))
            {
                MateriaTab.Draw(plugin);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Settings"))
            {
                DrawSettings();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Diagnostics"))
            {
                DrawDiagnostics();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Feedback"))
            {
                DrawFeedback();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("About"))
            {
                DrawAbout();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        ImGui.EndChild();
        
        DrawFooter(player);
    }

    private void DrawIdentityBar(Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter player)
    {
        var jobAbbr = player.ClassJob.Value.Abbreviation.ExtractText();
        var lvl = player.Level;
        var world = player.HomeWorld.Value.Name.ExtractText();

        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.TtChrome.Sink);
        ImGui.BeginChild("##identity", new Vector2(0, 76), false, ImGuiWindowFlags.NoScrollbar);

        var identityDrawList = ImGui.GetWindowDrawList();
        var identityMin = ImGui.GetWindowPos();
        var identitySize = ImGui.GetWindowSize();
        identityDrawList.AddRectFilled(
            identityMin,
            new Vector2(identityMin.X + identitySize.X, identityMin.Y + identitySize.Y),
            ImGui.GetColorU32(Theme.TtChrome.Sink));
        
        // Add some top padding
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 10f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 16f);

        // Job Stone
        if (plugin.Brand.JobStones.TryGetValue(jobAbbr, out var tex) && tex != null)
        // Logo and Title block
        ImGui.BeginGroup();
        if (plugin.Brand.CircleLogo != null)
        {
            ImGui.Image(plugin.Brand.CircleLogo.Handle, new Vector2(64, 64));
            ImGui.SameLine(0, 16);
        }
        ImGui.BeginGroup();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 8f);
        using (this.plugin.Fonts.CinzelHeader.PushOrNull())
        {
            ImGui.TextColored(Theme.TtChrome.GoldBright, "GearGoblin");
        }
        // Author block
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4f);
        ImGui.TextUnformatted("A component of ");
        ImGui.SameLine(0, 4);
        ImGui.TextColored(Theme.TtChrome.GoldBright, "Tonberry Tactics");
        ImGui.EndGroup();
        ImGui.EndGroup();

        // Right side: World and Refresh
        var avail = ImGui.GetContentRegionAvail();
        
        // Layout: World text (right aligned), then Refresh button
        // Calculate widths
        var worldLabel = "WORLD";
        var refreshLabel = "REFRESH";
        float worldWidth = 100f; // Approximation
        float refreshWidth = 80f; // Approximation
        
        ImGui.SameLine(ImGui.GetWindowWidth() - 200f);
        
        ImGui.BeginGroup();
        // Vertically center the right block within the 76px tall masthead
        ImGui.SetCursorPosY(22f);
        using (plugin.Fonts.JetBrainsMonoBody.PushOrNull())
        {
            ImGui.TextColored(Theme.TtChrome.FgFaint, worldLabel);
            ImGui.TextColored(Theme.TtChrome.Fg2, world);
        }
        ImGui.EndGroup();

        ImGui.SameLine(0, 16f);
        
        // Refresh Button
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 6f);
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.TtChrome.Rgba(45, 108, 223, 0.10f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.TtChrome.Rgba(45, 108, 223, 0.20f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.TtChrome.Cobalt);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.TtChrome.LineCobalt);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
        
        using (plugin.Fonts.Pixel.PushOrNull())
        {
            if (ImGui.Button("REFRESH", new Vector2(0, 24)))
            {
                try { _ = plugin.Inventory.ReadEquipped(); }
                catch (Exception ex) { DalamudServices.Log.Warning(ex, "Refresh threw."); }
                s_lastRefreshTime = DateTime.UtcNow;
            }
        }
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(4);
        
        var sinceRefresh = (DateTime.UtcNow - s_lastRefreshTime).TotalSeconds;
        if (sinceRefresh >= 0 && sinceRefresh < 2.0)
        {
            var alpha = (float)Math.Max(0.0, 1.0 - sinceRefresh / 2.0);
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.49f, 0.75f, 0.77f, alpha), "✓");
        }

        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawFooter(Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter player)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.TtChrome.Rgba(8, 15, 26, 0.7f));
        ImGui.BeginChild("##footer", new Vector2(0, 36), false, ImGuiWindowFlags.NoScrollbar);
        
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 10f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 16f);
        
        using (plugin.Fonts.Pixel.PushOrNull())
        {
            ImGui.TextColored(Theme.TtChrome.Tonberry, "LIVE");
            ImGui.SameLine(0, 8f);
            ImGui.TextColored(Theme.TtChrome.GoldDim, "· The Onion Knight stands ready");
        }
        
        var jobAbbr = player.ClassJob.Value.Abbreviation.ExtractText();
        var ilvl = plugin.Inventory.CalculateAverageItemLevel(plugin.Inventory.ReadEquipped()).ToString();
        
        ImGui.SameLine(ImGui.GetWindowWidth() - 140f);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 2f);
        
        Theme.TtChrome.Pill(jobAbbr, Theme.TtChrome.CobaltBright);
        ImGui.SameLine(0, 8f);
        Theme.TtChrome.Pill($"iLv {ilvl}", Theme.TtChrome.Ok);
        
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    // ── v0.4.6 Quick Start tab ──────────────────────────────────────────
    //
    // First tab for a reason: new users open /goblin and the loudest
    // confusion in field testing is "what does this plugin actually do
    // and what's that scary string." This tab answers both before the
    // user touches any other surface. Content lives in code rather than
    // an external help file so it ships with the plugin and stays in
    // sync with whatever version is actually loaded.

    private void DrawQuickStart()
    {
        Theme.TtChrome.BeginPanel("quickstart_panel");
        Theme.TtChrome.Eyebrow(this.plugin.Fonts, "Quick Start · The Loop");
        ImGui.Spacing();
        Theme.TtChrome.Quip(this.plugin.Fonts, "The export–optimize–import loop, in plain English.");
        ImGui.TextDisabled("In-game plugin · web app · same product, two halves.");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        Theme.TtChrome.Eyebrow(this.plugin.Fonts, "WHAT THIS PLUGIN DOES");
        ImGui.TextWrapped(
            "The Tonberry Tactics in-game plugin reads your equipped gear, gives you derived stats and " +
            "breakpoint hints in the Character window, and exports your gearset to a copy-pasteable string. " +
            "The companion website (tonberrytactics.pages.dev) consumes that string, runs an optimizer " +
            "in your browser, and produces a plan string going the other direction. The plan string gets " +
            "pasted back into the game as an actionable meld checklist. Two halves, one product, " +
            "round-trip in roughly thirty seconds.");
        ImGui.Spacing();
        ImGui.Spacing();

        Theme.TtChrome.Eyebrow(this.plugin.Fonts, "THE LOOP");
        ImGui.Spacing();

        // Step 1: EXPORT
        ImGui.TextColored(Theme.TtChrome.GoldBright, $"{Theme.TtChrome.GlyphForward} 1. EXPORT");
        ImGui.Indent();
        ImGui.TextUnformatted("In-game:  /ttexport");
        ImGui.TextWrapped(
            "Copies your current gear to your clipboard as GG-EXPORT:v1:<base64>. The scary-looking " +
            "string is just your gear list in a portable format — every piece, every materia, every stat. " +
            "Treat it like a save file you can paste into a website.");
        ImGui.Unindent();
        ImGui.Spacing();

        // Step 2: OPTIMIZE
        ImGui.TextColored(Theme.TtChrome.GoldBright, $"{Theme.TtChrome.GlyphForward} 2. OPTIMIZE");
        ImGui.Indent();
        ImGui.TextUnformatted("In your browser:  https://tonberrytactics.pages.dev");
        ImGui.TextWrapped(
            "Paste the GG-EXPORT:v1: string into the import box. The site runs an optimizer against your " +
            "gear (\"swap your earring's Det for Direct Hit and you'll hit the next speed tier\") and emits " +
            "a GG-PLAN:v1:<base64> string with the recommended melds.");
        ImGui.Unindent();
        ImGui.Spacing();

        // Step 3: IMPORT
        ImGui.TextColored(Theme.TtChrome.GoldBright, $"{Theme.TtChrome.GlyphForward} 3. IMPORT");
        ImGui.Indent();
        ImGui.TextUnformatted("In-game:  /ttimport");
        ImGui.TextWrapped(
            "Reads the GG-PLAN:v1: string from your clipboard and turns it into an active plan with a " +
            "meld checklist (\"Diamond Earring slot 1 ← Savage Aim XII\"). Tick boxes as you meld.");
        ImGui.Spacing();
        ImGui.TextColored(Theme.TtChrome.Warn,
            "v0.6.5: command parses + validates plan strings successfully. In-game persistence + checklist UI ship in v0.6.6.");
        ImGui.Unindent();
        ImGui.Spacing();
        ImGui.Spacing();

        // TLF manifesto opener — sets the voice up front
        Theme.TtChrome.Eyebrow(this.plugin.Fonts, "TLF MANIFESTO");
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TtChrome.FgMuted);
        ImGui.TextUnformatted("    We carry the lantern. We carry the knife. We do not run.");
        ImGui.TextUnformatted("    We step forward, one slot at a time, until the math is done.");
        ImGui.PopStyleColor();
        ImGui.Spacing();
        ImGui.SameLine(); Theme.TtChrome.Pill("offline",       Theme.TtChrome.FgMuted);
        ImGui.SameLine(); Theme.TtChrome.Pill("no backend",    Theme.TtChrome.FgMuted);
        ImGui.SameLine(); Theme.TtChrome.Pill("round-trip v1", Theme.TtChrome.Gold);
        ImGui.Spacing();
        ImGui.Spacing();

        // Slash commands cheat sheet
        Theme.TtChrome.Eyebrow(this.plugin.Fonts, "SLASH COMMANDS");
        ImGui.Separator();
        if (ImGui.BeginTable("##slashcmds", 2,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInner | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Command", ImGuiTableColumnFlags.WidthFixed, 180);
            ImGui.TableSetupColumn("What it does", ImGuiTableColumnFlags.WidthStretch);

            CmdRow("/tt",        "Open this window.");
            CmdRow("/ttexport",  "Export equipped gear to clipboard as GG-EXPORT:v1:...");
            CmdRow("/ttimport",  "Import a plan from clipboard. Pair with /ttexport + Tonberry Tactics.");
            CmdRow("/ttinfo",    "Print diagnostic state to chat. Use this when reporting bugs.");

            ImGui.EndTable();
        }
        ImGui.Spacing();
        ImGui.Spacing();

        // What you see in the Character window
        Theme.TtChrome.Eyebrow(this.plugin.Fonts, "WHAT YOU'LL SEE IN THE CHARACTER WINDOW");
        ImGui.Separator();
        ImGui.TextWrapped(
            "Below \"Average Item Level\" in the Gear section, Tonberry Tactics injects a Materia Advisor: " +
            "the header line shows status counts (e.g. \"0c · 0w · 0e · ▶ /tt\"), and up to three " +
            "recommendation rows below it. If your gear is already optimal, you'll see " +
            "\"All guaranteed slots filled · no upgrades suggested.\" Click the header to open this window.");
        ImGui.Spacing();
        ImGui.TextWrapped(
            "If CharacterPanelRefined is also installed (recommended), CPR provides the substat " +
            "derivations and Tonberry Tactics contributes only the Materia Advisor. Both plugins together is " +
            "the default deployment as of v0.4.6.");
        ImGui.Spacing();
        ImGui.Spacing();

        // Bug report flow
        Theme.TtChrome.Eyebrow(this.plugin.Fonts, "WHEN SOMETHING LOOKS WRONG");
        ImGui.Separator();
        ImGui.BulletText("Open the Diagnostics tab. Confirm \"Materia Advisor injected: Yes\".");
        ImGui.BulletText("Run /ttinfo in-game. Copy the chat block (or use the button on Diagnostics).");
        ImGui.BulletText("File an issue at github.com/LastOnionKnight/GearGoblin with the block attached.");
        ImGui.Spacing();
        ImGui.Spacing();

        // Tips
        Theme.TtChrome.Eyebrow(this.plugin.Fonts, "TIPS");
        ImGui.Separator();
        ImGui.BulletText("Plan tab: paste an Etro or XIVGear URL and diff it against your equipped gear slot-by-slot.");
        ImGui.BulletText("Materia tab: shows current melds with overcap and tier audits.");
        ImGui.BulletText("Settings tab: toggles for every derivation row; greyed out when CPR is providing them.");
        ImGui.BulletText("Diagnostics tab: \"Force Reinject\" re-runs the advisor without closing the Character window.");
        ImGui.Spacing();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TtChrome.GoldDim);
        if (plugin.Brand.LanternMark != null)
        {
            var iconSize = new System.Numerics.Vector2(16, 16);
            ImGui.Image(plugin.Brand.LanternMark.Handle, iconSize, System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, Theme.TtChrome.GoldDim);
            ImGui.SameLine();
            ImGui.TextColored(Theme.TtChrome.GoldDim, "  The Onion Knight stands ready  ");
            ImGui.SameLine();
            ImGui.Image(plugin.Brand.LanternMark.Handle, iconSize, System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, Theme.TtChrome.GoldDim);
        }
        else
        {
            ImGui.TextUnformatted($"  {Theme.TtChrome.GlyphCorner}  The Onion Knight stands ready  {Theme.TtChrome.GlyphCorner}");
        }
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TtChrome.FgFaint);
        ImGui.TextUnformatted($"  TONBERRY TACTICS · v{s_versionString} · NO GEAR · NO HOPE · NO PANTS · JUST ONIONS");
        ImGui.PopStyleColor();

        Theme.TtChrome.EndPanel();
    }

    private static void CmdRow(string cmd, string desc)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.TextColored(Theme.TtChrome.GoldBright, cmd);
        ImGui.TableNextColumn(); ImGui.TextUnformatted(desc);
    }

    // ── v0.4.6 Settings tab ─────────────────────────────────────────────

    private void DrawSettings()
    {
        Theme.TtChrome.BeginPanel("settings_panel");
        var configService = plugin.ConfigService;
        var cfg  = configService.Current;
        var diag = plugin.StatusPanel.GetDiagnostics();

        Theme.TtChrome.Eyebrow(this.plugin.Fonts, "Native Character-window injection");
        ImGui.Spacing();

        var nativeOn = cfg.EnableNativeStatPanel;
        if (ImGui.Checkbox("Enable native stat-panel injection (Materia Advisor, derivations, GCD)", ref nativeOn))
        {
            configService.SetEnableNativeStatPanel(nativeOn);
        }
        ImGui.TextDisabled("Off = /tt window is the only UI surface. Reopen the Character window for changes.");
        ImGui.Spacing();

        // CPR coexistence section.
        ImGui.Spacing();
        Theme.TtChrome.Eyebrow(this.plugin.Fonts, "CharacterPanelRefined coexistence");
        if (diag.CprDetected)
        {
            ImGui.TextColored(Theme.TtChrome.Ok,
                "✓ CPR detected — derivation rows deferred to CPR by default.");
        }
        else
        {
            ImGui.TextDisabled("CPR not detected — GearGoblin provides full derivations itself.");
        }
        ImGui.Spacing();

        var force = cfg.ForceDerivationsOverCpr;
        if (ImGui.Checkbox("Force GG derivations even when CPR is active (will double-render rows)", ref force))
        {
            configService.SetForceDerivationsOverCpr(force);
        }

        var compact = cfg.CompactDerivationLayout;
        if (ImGui.Checkbox("Compact one-line derivation layout (denser, saves vertical space)", ref compact))
        {
            configService.SetCompactDerivationLayout(compact);
        }
        ImGui.Spacing();

        // Per-stat toggles.
        ImGui.Spacing();
        Theme.TtChrome.Eyebrow(this.plugin.Fonts, "Per-stat derivation rows");
        var disabled = diag.CprDetected && !cfg.ForceDerivationsOverCpr;
        if (disabled)
        {
            ImGui.TextDisabled("(Skipped while CPR is active — toggle 'Force GG derivations' above to override.)");
            ImGui.BeginDisabled();
        }

        var enableDer = cfg.EnableDerivedStatInjection;
        if (ImGui.Checkbox("Master toggle: derived stat injection", ref enableDer))
        {
            configService.SetEnableDerivedStatInjection(enableDer);
        }

        var crit = cfg.ShowCritDerivations;
        if (ImGui.Checkbox("Critical Hit  (chance · ×damage · DI · breakpoint)", ref crit))
        {
            configService.SetShowCritDerivations(crit);
        }
        var det = cfg.ShowDetDerivations;
        if (ImGui.Checkbox("Determination  (damage increase · breakpoint)", ref det))
        {
            configService.SetShowDetDerivations(det);
        }
        var dh = cfg.ShowDhDerivations;
        if (ImGui.Checkbox("Direct Hit  (chance · DI · breakpoint)", ref dh))
        {
            configService.SetShowDhDerivations(dh);
        }
        var speed = cfg.ShowSpeedDerivations;
        if (ImGui.Checkbox("Skill / Spell Speed  (real GCD · speed damage · breakpoint)", ref speed))
        {
            configService.SetShowSpeedDerivations(speed);
        }
        var ten = cfg.ShowTenacityRow;
        if (ImGui.Checkbox("Tenacity row  (tank jobs: +damage · −damage taken)", ref ten))
        {
            configService.SetShowTenacityRow(ten);
        }
        var piety = cfg.ShowPietyRow;
        if (ImGui.Checkbox("Piety row  (healer jobs: MP/tick)", ref piety))
        {
            configService.SetShowPietyRow(piety);
        }

        if (disabled) ImGui.EndDisabled();

        ImGui.Spacing();
        Theme.TtChrome.Eyebrow(this.plugin.Fonts, "Logging");
        var verbose = cfg.EnableVerboseInjectorLogging;
        if (ImGui.Checkbox("Verbose injector logging (Materia Advisor per-update lines)", ref verbose))
        {
            configService.SetEnableVerboseInjectorLogging(verbose);
        }
        ImGui.TextDisabled("Recommended on after v0.4.6 update so we can verify the advisor-visibility fix.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled("Changes save automatically. Reopen the Character window for injector toggles to take effect.");

        Theme.TtChrome.EndPanel();
    }

    // ── v0.4.6 Diagnostics tab ──────────────────────────────────────────

    private void DrawDiagnostics()
    {
        Theme.TtChrome.BeginPanel("diagnostics_panel");
        var diag = plugin.StatusPanel.GetDiagnostics();

        Theme.TtChrome.Eyebrow(this.plugin.Fonts, "StatusPanelInjector — live state");
        ImGui.Spacing();

        if (ImGui.BeginTable("##diag", 2,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInner | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthFixed, 240);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);

            Row("Character panel attached",
                diag.PanelAttached ? "Yes" : "No — open the Character window");
            Row("CPR detected",
                diag.CprDetected ? "Yes" : "No");
            Row("Derivations enabled",
                diag.DerivationsEnabled ? "Yes" : "No (deferred to CPR or master toggle off)");
            Row("Materia Advisor injected",
                diag.AdvisorSectionPresent
                    ? "Yes"
                    : (diag.PanelAttached ? "No — gear node not found?" : "—"));
            Row("Advisor recommendations",
                diag.AdvisorRecCount.ToString());
            Row("Advisor empty-state",
                diag.AdvisorEmptyState ? "Yes (all materia optimal)" : "No");
            Row("Advisor errored",
                diag.AdvisorErrored ? "Yes — check /xllog for stack trace" : "No");
            Row("Outer-addon height growth",
                $"{diag.InjectedHeightPx} px");
            Row("Last inject result",
                diag.LastInjectResult);
            Row("Last inject time (UTC)",
                diag.LastInjectTime == default ? "—" : diag.LastInjectTime.ToString("HH:mm:ss"));
            Row("Last update tick (UTC)",
                diag.LastUpdateTime == default ? "—" : diag.LastUpdateTime.ToString("HH:mm:ss"));

            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("Force Reinject (re-run UpdateAllValues now)"))
        {
            plugin.StatusPanel.ForceReinject();
        }
        ImGui.SameLine();
        if (ImGui.Button("Copy /ttinfo block to clipboard"))
        {
            ImGui.SetClipboardText(plugin.BuildGoblinInfoString());
        }
        ImGui.TextDisabled("Use 'Copy' when reporting bugs — paste it into the GitHub issue or DM.");

        ImGui.Spacing();
        ImGui.Spacing();
        Theme.TtChrome.Eyebrow(this.plugin.Fonts, "How to read this");
        ImGui.TextWrapped(
            "• 'Materia Advisor injected: Yes' + 'recommendations: 0' + 'empty-state: Yes' is the healthy near-BiS " +
            "state — the panel shows an 'All guaranteed slots filled' row.\n" +
            "• 'Outer-addon height growth' near zero with CPR active suggests the v0.4.6 fix isn't running — " +
            "verify v0.4.6 is actually loaded (/xllog should have 'StatusPanelInjector v0.4.6').\n" +
            "• 'Advisor errored: Yes' means the optimizer threw on your gearset — paste the /xllog stack trace into a bug report.");
        Theme.TtChrome.EndPanel();
    }

    private static void Row(string label, string value)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.TextUnformatted(label);
        ImGui.TableNextColumn(); ImGui.TextUnformatted(value);
    }

    // ── v0.4.7 Feedback tab ─────────────────────────────────────────────
    //
    // Beta needs a feedback funnel that doesn't require a backend. The
    // approach: pre-fill a GitHub issue URL with title/body/labels and
    // open it in the user's default browser. For users without GitHub,
    // a "Copy" button puts the same payload on the clipboard for pasting
    // into Discord or a DM. The diagnostic block (same one /goblininfo
    // emits) gets auto-attached when the user leaves the checkbox on,
    // turning vague bug reports into something triagable.
    //
    // No webhook secrets in plugin source. No server to maintain. If
    // volume eventually justifies it, v0.5.x can add a Worker proxy
    // that mirrors to Discord server-side.

    private void DrawFeedback()
    {
        Theme.TtChrome.BeginPanel("feedback_panel");
        Theme.TtChrome.Eyebrow(this.plugin.Fonts, "Feedback");
        ImGui.Spacing();
        Theme.TtChrome.Quip(this.plugin.Fonts, "GearGoblin is in beta. Feedback genuinely shapes what ships next.");
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.TextUnformatted("Category");
        for (int i = 0; i < FeedbackCategories.Length; i++)
        {
            if (ImGui.RadioButton(FeedbackCategories[i], feedbackCategory == i))
                feedbackCategory = i;
            if (i < FeedbackCategories.Length - 1) ImGui.SameLine();
        }
        ImGui.Spacing();

        ImGui.TextUnformatted("Your message");
        ImGui.InputTextMultiline(
            "##feedbackText",
            feedbackBuf,
            new Vector2(-1, 180),
            ImGuiInputTextFlags.None);
        ImGui.Spacing();

        ImGui.Checkbox(
            "Include diagnostic info (recommended for bugs)",
            ref feedbackIncludeDiag);
        ImGui.TextDisabled(
            "Attaches the same block /ttinfo prints — plugin version, " +
            "job, advisor state, etc. No personal info.");
        ImGui.Spacing();

        var hasText = !string.IsNullOrWhiteSpace(ReadFeedbackText());
        if (!hasText) ImGui.BeginDisabled();

        if (ImGui.Button("Open GitHub issue (pre-filled)"))
        {
            try
            {
                var url = BuildGitHubIssueUrl();
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                feedbackLastAction =
                    $"✓ Opened GitHub issue in your browser at {DateTime.Now:HH:mm:ss}. " +
                    "Edit and submit there.";
                ClearFeedbackBuffer();
            }
            catch (Exception ex)
            {
                feedbackLastAction =
                    $"Couldn't open browser ({ex.GetType().Name}: {ex.Message}). " +
                    "Try the Copy button below.";
            }
        }

        ImGui.SameLine();

        if (ImGui.Button("Copy to clipboard for Discord / DM"))
        {
            var payload = BuildFeedbackPayload();
            ImGui.SetClipboardText(payload);
            feedbackLastAction =
                $"✓ Copied to clipboard at {DateTime.Now:HH:mm:ss}. " +
                "Paste in the FC Discord or DM Refia directly.";
            ClearFeedbackBuffer();
        }

        if (!hasText) ImGui.EndDisabled();
        ImGui.Spacing();

        if (!string.IsNullOrEmpty(feedbackLastAction))
        {
            ImGui.TextColored(Theme.TtChrome.Ok, feedbackLastAction);
        }

        ImGui.Spacing();
        ImGui.Spacing();
        Theme.TtChrome.Eyebrow(this.plugin.Fonts, "Where does this go?");
        ImGui.TextWrapped(
            "GitHub issues at " + FeedbackRepoUrl + "/issues. If you don't have a " +
            "GitHub account, the Copy button gives you the same payload to paste " +
            "into Discord or DM — both work. The diagnostic block is what makes " +
            "bug reports actually fixable, so leave it checked when reporting bugs.");
        ImGui.Spacing();
        ImGui.TextDisabled(
            "No analytics, no telemetry, no auto-submit. Nothing leaves your " +
            "machine unless you click one of those buttons.");
        Theme.TtChrome.EndPanel();
    }

    /// <summary>
    /// Build a GitHub "new issue" URL with title, body, and labels pre-filled
    /// via query string. GitHub supports this natively at
    /// /issues/new?title=...&body=...&labels=... — opens the issue-creation
    /// form with everything ready for the user to review and submit.
    /// </summary>
    private string BuildGitHubIssueUrl()
    {
        var categoryLabel = FeedbackCategories[feedbackCategory];
        var ghLabel       = FeedbackLabels[feedbackCategory];
        var title         = $"[v{s_versionString}] [{categoryLabel}] ";
        var body          = BuildFeedbackPayload();

        var titleEnc = WebUtility.UrlEncode(title);
        var bodyEnc  = WebUtility.UrlEncode(body);
        var labelEnc = WebUtility.UrlEncode(ghLabel);

        return $"{FeedbackRepoUrl}/issues/new" +
               $"?title={titleEnc}&body={bodyEnc}&labels={labelEnc}";
    }

    /// <summary>
    /// Single source of truth for the feedback payload — used by both the
    /// GitHub-issue URL constructor and the clipboard-copy path. Markdown
    /// headings render correctly on GitHub and in Discord (mostly).
    /// </summary>
    private string BuildFeedbackPayload()
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Category");
        sb.AppendLine(FeedbackCategories[feedbackCategory]);
        sb.AppendLine();
        sb.AppendLine("### Message");
        sb.AppendLine(ReadFeedbackText().Trim());
        sb.AppendLine();
        if (feedbackIncludeDiag)
        {
            sb.AppendLine("### Diagnostic info");
            sb.AppendLine("```");
            sb.Append(plugin.BuildGoblinInfoString());
            sb.AppendLine("```");
        }
        return sb.ToString();
    }

    // ── About ───────────────────────────────────────────────────────────

    private void DrawAbout()
    {
        Theme.TtChrome.BeginPanel("about_panel");
        // v0.6.0 — eyebrow in Press Start 2P @ 10px to match the web's
        // .brand-eyebrow micro-label treatment.
        Theme.TtChrome.Eyebrow(plugin.Fonts, "TLF GEAR DIVISION · OPERATIONS BRIEF");
        ImGui.Spacing();

        // v0.4.7.1: brand header — circle-logo + wordmark side by side,
        // gracefully falling back to text-only if the asset didn't load.
        // v0.6.0: wordmark renders in Cinzel @ 32px (display serif),
        // version line in Press Start 2P @ 10px (pixel micro-label).
        var logo = plugin.Brand.CircleLogo;
        if (logo != null)
        {
            const float logoSize = 64f;
            ImGui.Image(logo.Handle, new Vector2(logoSize, logoSize));
            ImGui.SameLine();
            // Vertical-center the wordmark next to the logo.
            var cursorY = ImGui.GetCursorPosY();
            ImGui.SetCursorPosY(cursorY + (logoSize - ImGui.GetTextLineHeight() * 2f) * 0.5f);
            ImGui.BeginGroup();
            using (plugin.Fonts.CinzelDisplay.PushOrNull())
            {
                ImGui.TextColored(Theme.TtChrome.GoldBright, "TONBERRY TACTICS");
            }
            using (plugin.Fonts.Pixel.PushOrNull())
            {
                ImGui.TextDisabled($"in-game plugin · v{s_versionString}");
            }
            ImGui.EndGroup();
        }
        else
        {
            using (plugin.Fonts.CinzelDisplay.PushOrNull())
            {
                ImGui.TextColored(Theme.TtChrome.GoldBright, "TONBERRY TACTICS");
            }
            ImGui.SameLine();
            using (plugin.Fonts.Pixel.PushOrNull())
            {
                ImGui.TextDisabled($"v{s_versionString}");
            }
        }

        ImGui.Spacing();
        // v0.6.0 — description in EB Garamond @ 15px (body serif),
        // matching the web's `.brand-sub` / Manifesto prose treatment.
        using (plugin.Fonts.GaramondBody.PushOrNull())
        {
            ImGui.TextWrapped(
                "BiS planner, gear inventory reader, and materia advisor for FFXIV. " +
                "The in-game half of Tonberry Tactics. Pairs with the companion website " +
                "(tonberrytactics.pages.dev) over a copy-pasteable export/plan string format. " +
                "Coexists comfortably with CharacterPanelRefined — CPR provides the substat " +
                "derivations, Tonberry Tactics contributes the Materia Advisor, real GCD when " +
                "CPR isn't job-aware, the export pipeline, and a diagnostic surface for " +
                "verifying what actually injected.");
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("By Refia Rakkiri — Last Onion Knight");

        // What's New — auto-generated from CHANGELOG.md via release.ps1
        ImGui.Spacing();
        if (_aboutChangelog == null)
        {
            try
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GearGoblin.Resources.about-changelog.txt");
                if (stream != null)
                {
                    using var reader = new System.IO.StreamReader(stream);
                    _aboutChangelog = reader.ReadToEnd();
                }
                else
                {
                    _aboutChangelog = "Changelog resource not found. (Requires clean build via release.ps1)";
                }
            }
            catch (Exception ex)
            {
                _aboutChangelog = $"Error loading changelog: {ex.Message}";
            }
        }
        
        ImGui.TextUnformatted(_aboutChangelog);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextDisabled("Materia formulas re-derived from public datamining sources");
        ImGui.TextDisabled("(Akhmorning Allagan Studies, FFXIV datamining repo).");
        ImGui.TextDisabled("AtkNode injection patterns adapted from CharacterPanelRefined (MIT).");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TtChrome.GoldDim);
        if (plugin.Brand.LanternMark != null)
        {
            var iconSize = new System.Numerics.Vector2(16, 16);
            ImGui.Image(plugin.Brand.LanternMark.Handle, iconSize, System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, Theme.TtChrome.GoldDim);
            ImGui.SameLine();
            ImGui.TextColored(Theme.TtChrome.GoldDim, "  The Onion Knight stands ready  ");
            ImGui.SameLine();
            ImGui.Image(plugin.Brand.LanternMark.Handle, iconSize, System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, Theme.TtChrome.GoldDim);
        }
        else
        {
            ImGui.TextUnformatted($"  {Theme.TtChrome.GlyphCorner}  The Onion Knight stands ready  {Theme.TtChrome.GlyphCorner}");
            ImGui.PopStyleColor();
        }
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TtChrome.FgFaint);
        ImGui.TextUnformatted($"  TONBERRY TACTICS · v{s_versionString} · NO GEAR · NO HOPE · NO PANTS · JUST ONIONS");
        ImGui.PopStyleColor();

        Theme.TtChrome.EndPanel();
    }

    /// <summary>
    /// Resolves the plugin's display version string from the loaded assembly.
    ///
    /// v0.6.5.2 fix — previous implementation returned only
    /// <c>$"{v.Major}.{v.Minor}.{v.Build}"</c> (three components), which
    /// silently truncated the Revision component for patch-level releases.
    /// Every v0.6.5.x release (v0.6.5.1, v0.6.5.2, etc.) rendered as plain
    /// "0.6.5" in the header badge / footer / Feedback tab / "in-game
    /// plugin · v" subtitle, making it impossible to tell from the UI
    /// which build was loaded — even when the underlying DLL was current.
    /// We only noticed because <c>/ttinfo</c>'s new (v0.6.5.1) flow and
    /// the About tab's "What's New" entries gave secondary signals.
    ///
    /// New behavior: emit four components when Revision is non-zero,
    /// three when it isn't. So v0.6.5 (Revision=0) still renders as
    /// "0.6.5" — no churn to existing display — while v0.6.5.1 renders
    /// as "0.6.5.1", v0.6.5.2 as "0.6.5.2", and so on. The empty-version
    /// fallback "0.3.2" is unchanged (kept for safety; should be
    /// unreachable since the assembly always has a Version).
    ///
    /// v0.6.5.3a: prefer AssemblyInformationalVersionAttribute when present.
    /// This is the canonical .NET home for human-friendly version strings
    /// that can carry letter suffixes ("0.6.5.3a"), pre-release tags, or
    /// build metadata — things AssemblyVersion can't represent (it's
    /// strictly Major.Minor.Build.Revision, validated by CS7034). When the
    /// csproj sets &lt;InformationalVersion&gt; explicitly, that's what users
    /// see in the header pill / About tab / /ttinfo. Fall back to formatting
    /// AssemblyVersion the v0.6.5.2 way if no InformationalVersion is set.
    ///
    /// v0.6.6: made internal (was private) so Plugin.cs BuildGoblinInfoString
    /// can share the same resolution logic. Previously, the in-game header
    /// pill read one version (via this method) while /ttinfo printed a
    /// different version (duplicate logic in Plugin.cs that didn't consult
    /// InformationalVersion). Single source of truth for display versions now.
    /// </summary>
    internal static string ResolveVersion()
    {
        // 1) AssemblyInformationalVersion — the canonical display version.
        var info = Assembly.GetExecutingAssembly()
                            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                            ?.InformationalVersion;
        if (!string.IsNullOrEmpty(info))
        {
            // Belt-and-suspenders: if the SDK has appended a "+commitHash" or
            // "+SourceRevisionId" suffix (which we also disable in csproj via
            // IncludeSourceRevisionInInformationalVersion=false), strip it for
            // user-facing display.
            var plusIdx = info.IndexOf('+');
            return plusIdx >= 0 ? info.Substring(0, plusIdx) : info;
        }

        // 2) Fall back to AssemblyVersion-based formatting (v0.6.5.2 behavior).
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        if (v is null) return "0.3.2";
        return v.Revision > 0
            ? $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}"
            : $"{v.Major}.{v.Minor}.{v.Build}";
    }
}


