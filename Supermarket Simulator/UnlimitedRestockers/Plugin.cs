using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace UnlimitedRestockers;

[BepInProcess("Supermarket Simulator.exe")]
[BepInPlugin(PluginId, "Unlimited Restockers", "2.0.0")]
public class Plugin : BasePlugin
{
    private const string PluginId = "svs-tm.unlimited-restockers";

    public static ManualLogSource Logger { get; private set; } = default!;

    public static Configuration Configuration { get; private set; } = default!;

    public override void Load()
    {
        Configuration = new Configuration(Config);
        Logger = Log;

        Harmony.CreateAndPatchAll(typeof(Patches), PluginId);

        AddComponent<RestockersManager>();
        AddComponent<RestockerSelectionMenu>();

        Log.LogInfo($"{PluginId} is loaded!");
    }
}
