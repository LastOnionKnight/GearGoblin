// UI/MainWindow.cs
//
// v0.4.7 adds:
//   Feedback     — pre-filled GitHub issue URL + clipboard fallback for
//                  Discord/DM. Category radio drives label + title prefix;
//                  diagnostic block auto-attaches when the checkbox is on.
//                  No webhooks, no analytics, no auto-submit.
//
// Tab order (v0.4.7):
//   Quick Start | Current Gear | Plan | Materia | Settings | Diagnostics | Feedback | About
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

namespace GearGoblin.UI;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private static readonly string s_versionString = ResolveVersion();

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

    public MainWindow(Plugin plugin) : base("GearGoblin###GearGoblinMain")
    {
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

        // v0.4.7 — TLF Lite palette wraps the entire window so child
        // backgrounds, tab chrome, frames, separators, and body text
        // all read in the TLF Gear Division visual language.
        Theme.TlfTheme.Push();
        try
        {
            DrawBody(player);
        }
        finally
        {
            Theme.TlfTheme.Pop();
        }
    }

    private void DrawBody(Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter player)
    {
        var job = player.ClassJob.Value.Abbreviation.ExtractText();
        var lvl = player.Level;
        ImGui.Text($"{player.Name} — {job} Lv {lvl}");
        ImGui.SameLine();
        if (ImGui.SmallButton("Refresh")) { /* placeholder for future cache invalidation */ }

        // Version badge — right-aligned in the header, TLF gold.
        var avail = ImGui.GetContentRegionAvail();
        var badgeText = $"v{s_versionString}";
        var badgeWidth = ImGui.CalcTextSize(badgeText).X + 12;
        ImGui.SameLine(ImGui.GetCursorPosX() + avail.X - badgeWidth);
        ImGui.TextColored(Theme.TlfTheme.GoldBright, badgeText);

        ImGui.Separator();

        if (ImGui.BeginTabBar("##goblintabs"))
        {
            if (ImGui.BeginTabItem("Quick Start"))
            {
                DrawQuickStart();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Current Gear"))
            {
                DrawCurrentGear();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Plan"))
            {
                PlanTab.Draw(plugin.Inventory);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Materia"))
            {
                MateriaTab.Draw(plugin.Inventory);
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
        ImGui.TextColored(Theme.TlfTheme.GoldBright, "GearGoblin → Tonberry Tactics → GearGoblin");
        ImGui.TextDisabled("The export–optimize–import loop, in plain English.");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // TLF manifesto opener — sets the voice up front
        Theme.TlfTheme.Eyebrow("TLF MANIFESTO");
        Theme.TlfTheme.Credo(
            "We carry the lantern. We carry the knife. We do not run.");
        Theme.TlfTheme.Credo(
            "We step forward, one slot at a time, until the math is done.");
        ImGui.Spacing();
        ImGui.SameLine(); Theme.TlfTheme.Pill("offline",       Theme.TlfTheme.Ice);
        ImGui.SameLine(); Theme.TlfTheme.Pill("no backend",    Theme.TlfTheme.Ice);
        ImGui.SameLine(); Theme.TlfTheme.Pill("round-trip v1", Theme.TlfTheme.Gold);
        ImGui.Spacing();
        ImGui.Spacing();

        Theme.TlfTheme.Eyebrow("WHAT THIS PLUGIN DOES");
        ImGui.TextWrapped(
            "GearGoblin reads your equipped gear, gives you derived stats and breakpoint hints in the " +
            "Character window, and exports your gearset to a copy-pasteable string. The companion website " +
            "Tonberry Tactics consumes that string, runs an optimizer over your gear, and produces a plan " +
            "string going the other direction. The plan string gets pasted back into the game " +
            "as an actionable meld checklist.");
        ImGui.Spacing();
        ImGui.Spacing();

        Theme.TlfTheme.Eyebrow("THE LOOP");
        ImGui.Spacing();

        // Step 1: EXPORT
        Theme.TlfTheme.Advisor("1. EXPORT");
        ImGui.Indent();
        ImGui.TextUnformatted("In-game:  /goblinexport");
        ImGui.TextWrapped(
            "Copies your current gear to your clipboard as GG-EXPORT:v1:<base64>. The scary-looking " +
            "string is just your gear list in a portable format — every piece, every materia, every stat. " +
            "Treat it like a save file you can paste into a website.");
        ImGui.Unindent();
        ImGui.Spacing();

        // Step 2: OPTIMIZE
        Theme.TlfTheme.Advisor("2. OPTIMIZE");
        ImGui.Indent();
        ImGui.TextUnformatted("In your browser:  https://tonberrytactics.pages.dev");
        ImGui.TextWrapped(
            "Paste the GG-EXPORT:v1: string into the import box. The site runs an optimizer against your " +
            "gear (\"swap your earring's Det for Direct Hit and you'll hit the next speed tier\") and emits " +
            "a GG-PLAN:v1:<base64> string with the recommended melds.");
        ImGui.Unindent();
        ImGui.Spacing();

        // Step 3: IMPORT
        Theme.TlfTheme.Advisor("3. IMPORT");
        ImGui.Indent();
        ImGui.TextUnformatted("In-game:  /goblinimport");
        ImGui.TextWrapped(
            "Reads the GG-PLAN:v1: string from your clipboard and turns it into an active plan with a " +
            "meld checklist (\"Diamond Earring slot 1 ← Savage Aim XII\"). Tick boxes as you meld.");
        ImGui.Spacing();
        ImGui.TextColored(Theme.TlfTheme.Warning,
            "v0.4.7 scaffold: command is registered and validates plan strings, but persistence + checklist UI land in the next build.");
        ImGui.Unindent();
        ImGui.Spacing();
        ImGui.Spacing();

        // Slash commands cheat sheet
        Theme.TlfTheme.Eyebrow("SLASH COMMANDS");
        ImGui.Separator();
        if (ImGui.BeginTable("##slashcmds", 2,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInner | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Command", ImGuiTableColumnFlags.WidthFixed, 180);
            ImGui.TableSetupColumn("What it does", ImGuiTableColumnFlags.WidthStretch);

            CmdRow("/goblin",       "Open this window.");
            CmdRow("/goblinexport", "Export equipped gear to clipboard as GG-EXPORT:v1:...");
            CmdRow("/goblinimport", "Import a plan from clipboard. Pair with /goblinexport + Tonberry Tactics.");
            CmdRow("/goblininfo",   "Print diagnostic state to chat. Use this when reporting bugs.");

            ImGui.EndTable();
        }
        ImGui.Spacing();
        ImGui.Spacing();

        // What you see in the Character window
        Theme.TlfTheme.Eyebrow("WHAT YOU'LL SEE IN THE CHARACTER WINDOW");
        ImGui.Separator();
        ImGui.TextWrapped(
            "Below \"Average Item Level\" in the Gear section, GearGoblin injects a Materia Advisor: " +
            "the header line shows status counts (e.g. \"0c · 0w · 0e · ▶ /goblin\"), and up to three " +
            "recommendation rows below it. If your gear is already optimal, you'll see " +
            "\"All guaranteed slots filled · no upgrades suggested.\" Click the header to open this window.");
        ImGui.Spacing();
        ImGui.TextWrapped(
            "If CharacterPanelRefined is also installed (recommended), CPR provides the substat " +
            "derivations and GearGoblin contributes only the Materia Advisor. Both plugins together is " +
            "the default deployment as of v0.4.6.");
        ImGui.Spacing();
        ImGui.Spacing();

        // Bug report flow
        Theme.TlfTheme.Eyebrow("WHEN SOMETHING LOOKS WRONG");
        ImGui.Separator();
        ImGui.BulletText("Open the Diagnostics tab. Confirm \"Materia Advisor injected: Yes\".");
        ImGui.BulletText("Run /goblininfo in-game. Copy the chat block (or use the button on Diagnostics).");
        ImGui.BulletText("File an issue at github.com/LastOnionKnight/GearGoblin with the block attached.");
        ImGui.Spacing();
        ImGui.Spacing();

        // Tips
        Theme.TlfTheme.Eyebrow("TIPS");
        ImGui.Separator();
        ImGui.BulletText("Plan tab: paste an Etro or XIVGear URL and diff it against your equipped gear slot-by-slot.");
        ImGui.BulletText("Materia tab: shows current melds with overcap and tier audits.");
        ImGui.BulletText("Settings tab: toggles for every derivation row; greyed out when CPR is providing them.");
        ImGui.BulletText("Diagnostics tab: \"Force Reinject\" re-runs the advisor without closing the Character window.");
        ImGui.Spacing();

        Theme.TlfTheme.StandingReadyFooter(s_versionString);
    }

    private static void CmdRow(string cmd, string desc)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.TextColored(Theme.TlfTheme.GoldBright, cmd);
        ImGui.TableNextColumn(); ImGui.TextUnformatted(desc);
    }

    private void DrawCurrentGear()
    {
        var equipped = plugin.Inventory.ReadEquipped();
        if (equipped.Count == 0)
        {
            ImGui.TextDisabled("No equipped items detected.");
            return;
        }
        var ilvl = plugin.Inventory.CalculateAverageItemLevel(equipped);
        ImGui.Text($"Average Item Level: {ilvl}");
        ImGui.Spacing();
        if (ImGui.BeginTable("##gear", 4,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInner | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Slot",    ImGuiTableColumnFlags.WidthFixed, 90);
            ImGui.TableSetupColumn("Item",    ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("iLvl",    ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Materia", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();
            foreach (var piece in equipped)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text(piece.Slot.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(piece.IsHighQuality ? $"{piece.Name} ★" : piece.Name);
                ImGui.TableNextColumn(); ImGui.Text(piece.ItemLevel.ToString());
                ImGui.TableNextColumn();
                if (piece.Materia.Count == 0)
                {
                    ImGui.TextDisabled("—");
                }
                else
                {
                    foreach (var m in piece.Materia)
                    {
                        ImGui.TextUnformatted($"+{m.StatValue} {m.StatName}");
                        if (m.SlotIndex < piece.Materia.Count - 1) ImGui.SameLine();
                    }
                }
            }
            ImGui.EndTable();
        }
    }

    // ── v0.4.6 Settings tab ─────────────────────────────────────────────

    private void DrawSettings()
    {
        var cfg  = plugin.Configuration;
        var diag = plugin.StatusPanel.GetDiagnostics();
        var dirty = false;

        ImGui.TextColored(Theme.TlfTheme.GoldBright, "Native Character-window injection");
        ImGui.Separator();
        ImGui.Spacing();

        var nativeOn = cfg.EnableNativeStatPanel;
        if (ImGui.Checkbox("Enable native stat-panel injection (Materia Advisor, derivations, GCD)", ref nativeOn))
        {
            cfg.EnableNativeStatPanel = nativeOn;
            dirty = true;
        }
        ImGui.TextDisabled("Off = /goblin window is the only UI surface. Reopen the Character window for changes.");
        ImGui.Spacing();

        // CPR coexistence section.
        ImGui.TextColored(Theme.TlfTheme.Lantern, "CharacterPanelRefined coexistence");
        ImGui.Separator();
        if (diag.CprDetected)
        {
            ImGui.TextColored(Theme.TlfTheme.ShipBright,
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
            cfg.ForceDerivationsOverCpr = force;
            dirty = true;
        }

        var compact = cfg.CompactDerivationLayout;
        if (ImGui.Checkbox("Compact one-line derivation layout (denser, saves vertical space)", ref compact))
        {
            cfg.CompactDerivationLayout = compact;
            dirty = true;
        }
        ImGui.Spacing();

        // Per-stat toggles.
        ImGui.TextColored(Theme.TlfTheme.Lantern, "Per-stat derivation rows");
        ImGui.Separator();
        var disabled = diag.CprDetected && !cfg.ForceDerivationsOverCpr;
        if (disabled)
        {
            ImGui.TextDisabled("(Skipped while CPR is active — toggle 'Force GG derivations' above to override.)");
            ImGui.BeginDisabled();
        }

        var enableDer = cfg.EnableDerivedStatInjection;
        if (ImGui.Checkbox("Master toggle: derived stat injection", ref enableDer))
        {
            cfg.EnableDerivedStatInjection = enableDer;
            dirty = true;
        }

        var crit = cfg.ShowCritDerivations;
        if (ImGui.Checkbox("Critical Hit  (chance · ×damage · DI · breakpoint)", ref crit))
        {
            cfg.ShowCritDerivations = crit;
            dirty = true;
        }
        var det = cfg.ShowDetDerivations;
        if (ImGui.Checkbox("Determination  (damage increase · breakpoint)", ref det))
        {
            cfg.ShowDetDerivations = det;
            dirty = true;
        }
        var dh = cfg.ShowDhDerivations;
        if (ImGui.Checkbox("Direct Hit  (chance · DI · breakpoint)", ref dh))
        {
            cfg.ShowDhDerivations = dh;
            dirty = true;
        }
        var speed = cfg.ShowSpeedDerivations;
        if (ImGui.Checkbox("Skill / Spell Speed  (real GCD · speed damage · breakpoint)", ref speed))
        {
            cfg.ShowSpeedDerivations = speed;
            dirty = true;
        }
        var ten = cfg.ShowTenacityRow;
        if (ImGui.Checkbox("Tenacity row  (tank jobs: +damage · −damage taken)", ref ten))
        {
            cfg.ShowTenacityRow = ten;
            dirty = true;
        }
        var piety = cfg.ShowPietyRow;
        if (ImGui.Checkbox("Piety row  (healer jobs: MP/tick)", ref piety))
        {
            cfg.ShowPietyRow = piety;
            dirty = true;
        }

        if (disabled) ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.TextColored(Theme.TlfTheme.Lantern, "Logging");
        ImGui.Separator();
        var verbose = cfg.EnableVerboseInjectorLogging;
        if (ImGui.Checkbox("Verbose injector logging (Materia Advisor per-update lines)", ref verbose))
        {
            cfg.EnableVerboseInjectorLogging = verbose;
            dirty = true;
        }
        ImGui.TextDisabled("Recommended on after v0.4.6 update so we can verify the advisor-visibility fix.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled("Changes save automatically. Reopen the Character window for injector toggles to take effect.");

        if (dirty)
        {
            cfg.Save();
        }
    }

    // ── v0.4.6 Diagnostics tab ──────────────────────────────────────────

    private void DrawDiagnostics()
    {
        var diag = plugin.StatusPanel.GetDiagnostics();

        ImGui.TextColored(Theme.TlfTheme.GoldBright, "StatusPanelInjector — live state");
        ImGui.Separator();
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
        if (ImGui.Button("Copy /goblininfo block to clipboard"))
        {
            ImGui.SetClipboardText(plugin.BuildGoblinInfoString());
        }
        ImGui.TextDisabled("Use 'Copy' when reporting bugs — paste it into the GitHub issue or DM.");

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.TextColored(Theme.TlfTheme.Lantern, "How to read this");
        ImGui.Separator();
        ImGui.TextWrapped(
            "• 'Materia Advisor injected: Yes' + 'recommendations: 0' + 'empty-state: Yes' is the healthy near-BiS " +
            "state — the panel shows an 'All guaranteed slots filled' row.\n" +
            "• 'Outer-addon height growth' near zero with CPR active suggests the v0.4.6 fix isn't running — " +
            "verify v0.4.6 is actually loaded (/xllog should have 'StatusPanelInjector v0.4.6').\n" +
            "• 'Advisor errored: Yes' means the optimizer threw on your gearset — paste the /xllog stack trace into a bug report.");
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
        ImGui.TextColored(Theme.TlfTheme.GoldBright,
            "Tell Refia what's working and what isn't.");
        ImGui.TextDisabled(
            "GearGoblin is in beta. Feedback genuinely shapes what ships next.");
        ImGui.Spacing();
        ImGui.Separator();
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
            "Attaches the same block /goblininfo prints — plugin version, " +
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
            ImGui.TextColored(Theme.TlfTheme.ShipBright, feedbackLastAction);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextColored(Theme.TlfTheme.Lantern, "Where does this go?");
        ImGui.TextWrapped(
            "GitHub issues at " + FeedbackRepoUrl + "/issues. If you don't have a " +
            "GitHub account, the Copy button gives you the same payload to paste " +
            "into Discord or DM — both work. The diagnostic block is what makes " +
            "bug reports actually fixable, so leave it checked when reporting bugs.");
        ImGui.Spacing();
        ImGui.TextDisabled(
            "No analytics, no telemetry, no auto-submit. Nothing leaves your " +
            "machine unless you click one of those buttons.");
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
        Theme.TlfTheme.Eyebrow("TLF GEAR DIVISION · OPERATIONS BRIEF");
        ImGui.Spacing();
        ImGui.TextUnformatted("GearGoblin");
        ImGui.SameLine();
        ImGui.TextColored(Theme.TlfTheme.GoldBright, $"v{s_versionString}");

        ImGui.Spacing();
        ImGui.TextWrapped(
            "BiS planner, gear inventory reader, and materia advisor for FFXIV. " +
            "As of v0.4.6 GearGoblin sits comfortably alongside CharacterPanelRefined — " +
            "CPR provides the substat derivations, GearGoblin contributes the Materia Advisor, " +
            "real GCD when CPR isn't job-aware, the Tonberry Tactics export pipeline, and a " +
            "diagnostic surface for verifying what actually injected.");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("By LastOnionKnight");
        ImGui.TextDisabled("Refia Rakkiri — the Last Onion Knight (Aisling O'Callaghan, Cork)");

        // ── v0.4.6 ────────────────────────────────────────────────────────
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.5f, 1f), "v0.4.6 — \"Coexistence\":");
        ImGui.BulletText("FIX: Materia Advisor now visible when CPR is active");
        ImGui.Indent();
        ImGui.BulletText("Advisor rows were being injected but rendered past the addon's clip boundary;");
        ImGui.BulletText("v0.4.6 tracks total injected height and grows the outer addon's RootNode to match");
        ImGui.Unindent();
        ImGui.BulletText("Instrumented advisor logging — replaces the v0.4.5 aspirational \"will inject normally\"");
        ImGui.BulletText("Quick Start tab: first-time-user workflow guide for the export-optimize-import loop");
        ImGui.BulletText("Settings tab: every v0.4.5 toggle surfaced as a checkbox (was config-file-only)");
        ImGui.BulletText("Diagnostics tab: live injector state, force-reinject button, copyable status block");
        ImGui.BulletText("/goblininfo slash command: dumps diagnostics to chat for bug reports");
        ImGui.BulletText("release.ps1: dotnet-build gate + commit-message BOM fix");

        // ── v0.4.5 ────────────────────────────────────────────────────────
        ImGui.Spacing();
        ImGui.TextUnformatted("v0.4.5 — full derivation suite + CPR coexistence:");
        ImGui.BulletText("Compact derived rows per substat (Crit / Det / DH)");
        ImGui.BulletText("Tenacity row (tank role): damage out · damage taken");
        ImGui.BulletText("Piety row (healer role): MP/tick");
        ImGui.BulletText("Speed section: real GCD + breakpoint + speed damage (consolidated)");
        ImGui.BulletText("CPR coexistence: auto-detects CharacterPanelRefined and steps aside");
        ImGui.BulletText("Per-section toggles for every derivation group");

        // ── v0.4.2 ────────────────────────────────────────────────────────
        ImGui.Spacing();
        ImGui.TextUnformatted("v0.4.2 fixes:");
        ImGui.BulletText("Materia Advisor footer no longer scrolls off the panel");
        ImGui.BulletText("Critical Hit breakpoint hint now renders reliably (label-walk, not positional)");
        ImGui.BulletText("Det/DH injected rows no longer overlap vanilla stat values");
        ImGui.BulletText("Empty advisor shows clear status text instead of dashes");
        ImGui.BulletText("Advisor section consolidated from 6 rows to 4 (header carries status counts)");

        // ── v0.4.1 ────────────────────────────────────────────────────────
        ImGui.Spacing();
        ImGui.TextUnformatted("v0.4.1 — Tonberry Tactics handoff:");
        ImGui.BulletText("/goblinexport command writes a clipboard-ready JSON of your equipped gear");
        ImGui.BulletText("Compatible with the Tonberry Tactics web optimizer (TLF Gear Division)");
        ImGui.BulletText("Dalamud SDK 15 compat (AddonEventData signature update)");

        // ── v0.4.0 ────────────────────────────────────────────────────────
        ImGui.Spacing();
        ImGui.TextUnformatted("v0.4.0 — native Character window integration:");
        ImGui.BulletText("Breakpoint hints injected under each substat row");
        ImGui.BulletText("Real GCD derivation injected under Skill/Spell Speed");
        ImGui.BulletText("Materia Advisor section injected under Gear");
        ImGui.BulletText("Click the advisor header to open the standalone /goblin window");

        // ── v0.3.x ────────────────────────────────────────────────────────
        ImGui.Spacing();
        ImGui.TextUnformatted("Earlier (v0.3.x) — standalone window:");
        ImGui.BulletText("Stat sheet with breakpoint analysis");
        ImGui.BulletText("Plan mode: recommended materia for empty meld slots");
        ImGui.BulletText("Audit mode: review existing melds for overcap and tier issues");
        ImGui.BulletText("Pure-math vs Balance-preset weighting");
        ImGui.BulletText("Etro / XIVGear BiS comparison");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextDisabled("Materia formulas re-derived from public datamining sources");
        ImGui.TextDisabled("(Akhmorning Allagan Studies, FFXIV datamining repo).");
        ImGui.TextDisabled("AtkNode injection patterns adapted from CharacterPanelRefined (MIT).");

        Theme.TlfTheme.StandingReadyFooter(s_versionString);
    }

    private static string ResolveVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v is null ? "0.3.2" : $"{v.Major}.{v.Minor}.{v.Build}";
    }
}
