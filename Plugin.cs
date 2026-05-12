using System;
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

    public Configuration Configuration { get; }
    public WindowSystem  WindowSystem  { get; } = new("GearGoblin");

    public InventoryReader  Inventory { get; }
    public GearsetExporter  Exporter  { get; }   // v0.4.1

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
    /// Toggle the standalone /goblin window. Public so the v0.4.0
    /// StatusPanelInjector can invoke it from the in-addon Materia Advisor
    /// footer's click handler.
    /// </summary>
    public void ToggleMain() => mainWindow.Toggle();

    private void DrawUI() => WindowSystem.Draw();
}
