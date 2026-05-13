// Plugin.cs
//
// v0.4.6 — adds the /goblininfo slash command, which dumps the current
// StatusPanelInjector diagnostic snapshot to chat in a copy-paste-friendly
// format. Same payload is also offered via a button on the Diagnostics tab.
// Bug reports become "paste me your /goblininfo" instead of "send me a
// screenshot of the Character window and your /xllog."
//
// Lineage:
//   v0.4.0  Plugin scaffolding + StatusPanelInjector wiring
//   v0.4.1  /goblinexport command + GearsetExporter
//   v0.4.6  /goblininfo command + BuildGoblinInfoString() (this release)

using System;
using System.Text;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using GearGoblin.Services;
using GearGoblin.UI;

namespace GearGoblin;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "GearGoblin";

    private const string CommandName       = "/goblin";
    private const string ExportCommandName = "/goblinexport";   // v0.4.1
    private const string InfoCommandName   = "/goblininfo";     // v0.4.6
    private const string ImportCommandName = "/goblinimport";   // v0.4.7

    public Configuration Configuration { get; }
    public WindowSystem  WindowSystem  { get; } = new("GearGoblin");

    public InventoryReader  Inventory { get; }
    public GearsetExporter  Exporter  { get; }   // v0.4.1
    public GearsetImporter  Importer  { get; }   // v0.4.7 (scaffold; full body next session)

    // v0.4.0: native injection into the CharacterStatus addon.
    public StatusPanelInjector StatusPanel { get; }

    private readonly MainWindow mainWindow;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        // Inject Dalamud services into the static container.
        pluginInterface.Create<DalamudServices>();

        // Load or create config.
        Configuration = DalamudServices.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(DalamudServices.PluginInterface);

        // Services.
        Inventory   = new InventoryReader();
        Exporter    = new GearsetExporter(Inventory);                       // v0.4.1
        Importer    = new GearsetImporter(this);                            // v0.4.7 (scaffold)
        StatusPanel = new StatusPanelInjector(this);

        // UI.
        mainWindow = new MainWindow(this);
        WindowSystem.AddWindow(mainWindow);

        DalamudServices.PluginInterface.UiBuilder.Draw         += DrawUI;
        DalamudServices.PluginInterface.UiBuilder.OpenConfigUi += ToggleMain;
        DalamudServices.PluginInterface.UiBuilder.OpenMainUi   += ToggleMain;

        // Commands.
        DalamudServices.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open GearGoblin. Usage: /goblin"
        });
        DalamudServices.CommandManager.AddHandler(ExportCommandName, new CommandInfo(OnExportCommand)
        {
            HelpMessage = "Export your equipped gearset to clipboard for use in Tonberry Tactics."
        });
        DalamudServices.CommandManager.AddHandler(InfoCommandName, new CommandInfo(OnInfoCommand)    // v0.4.6
        {
            HelpMessage = "Print GearGoblin diagnostics to chat. Useful for bug reports."
        });
        DalamudServices.CommandManager.AddHandler(ImportCommandName, new CommandInfo(OnImportCommand)  // v0.4.7
        {
            HelpMessage = "Import a GG-PLAN:v1: plan string from clipboard. Pair with /goblinexport and Tonberry Tactics."
        });

        DalamudServices.Log.Info($"GearGoblin v{GetType().Assembly.GetName().Version} loaded.");
    }

    public void Dispose()
    {
        // Tear down in reverse construction order. StatusPanel must dispose
        // before the WindowSystem so its click-handler unregistration runs
        // while Dalamud's services are still alive.
        StatusPanel?.Dispose();

        DalamudServices.PluginInterface.UiBuilder.Draw         -= DrawUI;
        DalamudServices.PluginInterface.UiBuilder.OpenConfigUi -= ToggleMain;
        DalamudServices.PluginInterface.UiBuilder.OpenMainUi   -= ToggleMain;

        DalamudServices.CommandManager.RemoveHandler(CommandName);
        DalamudServices.CommandManager.RemoveHandler(ExportCommandName);    // v0.4.1
        DalamudServices.CommandManager.RemoveHandler(InfoCommandName);      // v0.4.6
        DalamudServices.CommandManager.RemoveHandler(ImportCommandName);    // v0.4.7

        WindowSystem.RemoveAllWindows();
        mainWindow.Dispose();
    }

    private void OnCommand(string command, string args) => ToggleMain();

    /// <summary>
    /// v0.4.1: /goblinexport command. Serializes the currently-equipped
    /// gearset (job, level, items, melded materia) to a base64-encoded JSON
    /// string and copies it to the system clipboard for use in the Tonberry
    /// Tactics web app at tonberrytactics.pages.dev. Pure read; doesn't open
    /// any UI.
    /// </summary>
    private void OnExportCommand(string command, string args) => Exporter.ExportToClipboard();

    /// <summary>
    /// v0.4.6: /goblininfo command. Prints the StatusPanelInjector's current
    /// DiagnosticSnapshot to chat in a fenced block so users can paste it
    /// into a bug report. Same payload as the "Copy /goblininfo" button on
    /// the Diagnostics tab of the standalone /goblin window.
    /// </summary>
    private void OnInfoCommand(string command, string args)
    {
        try
        {
            var info = BuildGoblinInfoString();
            // Print line-by-line so each line gets the chat-window timestamp
            // and is independently copy-clickable. Discrete lines also avoid
            // a single huge entry that the chat log might truncate.
            foreach (var line in info.Split('\n'))
            {
                DalamudServices.ChatGui.Print(line);
            }
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "OnInfoCommand: BuildGoblinInfoString threw.");
            DalamudServices.ChatGui.PrintError($"[GearGoblin] /goblininfo failed: {ex.Message}");
        }
    }

    /// <summary>
    /// v0.4.7: /goblinimport command. Reads a GG-PLAN:v1: string from the
    /// system clipboard (or from <paramref name="args"/> if the user passed
    /// the wire string inline), validates it via GearsetImporter, and
    /// persists the resulting plan into Configuration.JobPlans under
    /// PlanMode.Imported. Surfaces results to chat.
    ///
    /// <para>
    /// Status as of v0.4.7 scaffold commit: the validation flow exists in
    /// GearsetImporter (prefix check, base64 decode, JSON parse, schema
    /// version, emitter identity), but the clipboard read and the
    /// Configuration persist steps are still TODO. This handler is
    /// registered and reachable; it just reports honestly that the wiring
    /// isn't complete yet. Replace this comment when the persist body lands.
    /// </para>
    /// </summary>
    private void OnImportCommand(string command, string args)
    {
        try
        {
            // If the user passed the wire string inline (e.g. /goblinimport GG-PLAN:v1:abc),
            // route to ImportFromString; otherwise pull from clipboard.
            var result = !string.IsNullOrWhiteSpace(args)
                ? Importer.ImportFromString(args)
                : Importer.ImportFromClipboard();

            if (!result.Success)
            {
                DalamudServices.ChatGui.PrintError(
                    $"[GearGoblin] Import failed: {result.ErrorMessage}");
                return;
            }

            // Scaffold notice — replace once Configuration persist is wired.
            DalamudServices.ChatGui.Print(
                "[GearGoblin] Plan parsed successfully. " +
                $"({result.Payload?.Melds.Count ?? 0} meld(s) recommended.) " +
                "Persisting to active plan: not yet wired in v0.4.7 scaffold. " +
                "Full /goblinimport implementation lands in the next build.");

            foreach (var warning in result.Warnings)
            {
                DalamudServices.ChatGui.Print($"[GearGoblin] Warning: {warning}");
            }
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "OnImportCommand: importer threw.");
            DalamudServices.ChatGui.PrintError($"[GearGoblin] /goblinimport failed: {ex.Message}");
        }
    }

    /// <summary>
    /// v0.4.6: build a human-readable, copy-paste-friendly diagnostic block.
    /// Used by both the /goblininfo slash command and the Diagnostics tab's
    /// "Copy to clipboard" button.
    ///
    /// <para>
    /// Output shape (~15 lines): plugin version + player + job + iLvl,
    /// then every field of the StatusPanelInjector.DiagnosticSnapshot, then
    /// a trailing instruction line directing users to attach their /xllog
    /// if reporting a bug. Wrapped in '─────' separator lines so it's
    /// visually distinct in chat or in a GitHub issue.
    /// </para>
    /// </summary>
    public string BuildGoblinInfoString()
    {
        var sb = new StringBuilder();
        var diag = StatusPanel.GetDiagnostics();
        var asm  = GetType().Assembly.GetName().Version;
        var v    = asm is null ? "?.?.?" : $"{asm.Major}.{asm.Minor}.{asm.Build}";

        sb.AppendLine("───── GearGoblin /goblininfo ─────");
        sb.AppendLine($"Plugin version       : v{v}");

        var player = DalamudServices.ObjectTable.LocalPlayer;
        if (player is not null)
        {
            var job = player.ClassJob.Value.Abbreviation.ExtractText();
            sb.AppendLine($"Player               : {player.Name} — {job} Lv {player.Level}");
        }
        else
        {
            sb.AppendLine("Player               : (not logged in)");
        }

        sb.AppendLine("──── Injector state ────");
        sb.AppendLine($"Character panel attached : {(diag.PanelAttached ? "yes" : "no")}");
        sb.AppendLine($"CPR detected             : {(diag.CprDetected ? "yes" : "no")}");
        sb.AppendLine($"Derivations enabled      : {(diag.DerivationsEnabled ? "yes" : "no")}");
        sb.AppendLine($"Advisor section injected : {(diag.AdvisorSectionPresent ? "yes" : "no")}");
        sb.AppendLine($"Advisor recommendations  : {diag.AdvisorRecCount}");
        sb.AppendLine($"Advisor empty-state      : {(diag.AdvisorEmptyState ? "yes (all materia optimal)" : "no")}");
        sb.AppendLine($"Advisor errored          : {(diag.AdvisorErrored ? "YES — check /xllog" : "no")}");
        sb.AppendLine($"Outer-addon height grew  : {diag.InjectedHeightPx} px");
        sb.AppendLine($"Last inject result       : {diag.LastInjectResult}");
        sb.AppendLine($"Last inject time (UTC)   : {(diag.LastInjectTime == default ? "—" : diag.LastInjectTime.ToString("HH:mm:ss"))}");
        sb.AppendLine($"Last update tick (UTC)   : {(diag.LastUpdateTime == default ? "—" : diag.LastUpdateTime.ToString("HH:mm:ss"))}");
        sb.AppendLine("─────────────────────────────");
        sb.AppendLine("If reporting a bug, attach the relevant /xllog lines (search 'StatusPanelInjector v0.4.6').");

        return sb.ToString();
    }

    /// <summary>
    /// Toggle the standalone /goblin window. Public so the v0.4.0
    /// StatusPanelInjector can invoke it from the in-addon Materia Advisor
    /// footer's click handler.
    /// </summary>
    public void ToggleMain() => mainWindow.Toggle();

    private void DrawUI() => WindowSystem.Draw();
}
