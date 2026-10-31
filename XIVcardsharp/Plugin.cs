using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DalamudDesignKit;
using DalamudDesignKit.Theme;
using ECommons;
using ECommons.DalamudServices;
using XIVcardsharp.Execution;
using XIVcardsharp.Interop;
using XIVcardsharp.UI;

namespace XIVcardsharp;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "XIVcardsharp";
    private const string CommandName = "/cardsharp";

    public static Plugin Instance { get; private set; } = null!;
    public static Configuration Configuration { get; private set; } = null!;

    public readonly WindowSystem WindowSystem = new("XIVcardsharp");
    private readonly MainWindow _mainWindow;
    private readonly IDalamudPluginInterface _pluginInterface;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        Instance = this;
        _pluginInterface = pluginInterface;
        ECommonsMain.Init(pluginInterface, this, Module.All);

        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(pluginInterface);

        // Initialize DalamudDesignKit and activate purple accent theme
        DesignKit.Initialize(pluginInterface.UiBuilder);
        ThemeTokens.Current = PalettePresets.HyperViolet;

        // Load game cards from Lumina into TriadCardDB
        LuminaCardLoader.LoadCards(Svc.Data);

        _mainWindow = new MainWindow();
        WindowSystem.AddWindow(_mainWindow);

        _pluginInterface.UiBuilder.Draw += DrawUI;
        _pluginInterface.UiBuilder.OpenConfigUi += ToggleUI;
        _pluginInterface.UiBuilder.OpenMainUi += ToggleUI;

        Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the XIVcardsharp control panel."
        });

        Svc.Framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        UniversalTriadManager.Tick();
    }

    private void OnCommand(string command, string args)
    {
        ToggleUI();
    }

    private void ToggleUI()
    {
        _mainWindow.IsOpen = !_mainWindow.IsOpen;
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnFrameworkUpdate;
        Svc.Commands.RemoveHandler(CommandName);

        _pluginInterface.UiBuilder.Draw -= DrawUI;
        _pluginInterface.UiBuilder.OpenConfigUi -= ToggleUI;
        _pluginInterface.UiBuilder.OpenMainUi -= ToggleUI;

        WindowSystem.RemoveAllWindows();
        _mainWindow.Dispose();

        UniversalTriadManager.Reset();
        ECommonsMain.Dispose();
    }
}
