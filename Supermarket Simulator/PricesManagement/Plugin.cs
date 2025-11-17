using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace PricesManagement;

[BepInProcess("Supermarket Simulator.exe")]
[BepInPlugin(PluginId, "Prices Management", "1.1.0")]
public class Plugin : BasePlugin
{
    private const string PluginId = "svs-tm.prices-management";

    public static ManualLogSource Logger { get; private set; } = default!;

    public static Configuration Configuration { get; private set; } = default!;

    public override void Load()
    {
        Configuration = new Configuration(Config);
        Logger = Log;

        Harmony.CreateAndPatchAll(typeof(Patches), PluginId);

        Log.LogInfo($"{PluginId} is loaded!");
    }
}
