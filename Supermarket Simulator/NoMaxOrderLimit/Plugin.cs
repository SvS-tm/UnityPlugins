using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace NoMaxOrderLimit;

[BepInProcess("Supermarket Simulator.exe")]
[BepInPlugin(PluginId, "No Max Order Limit", "2.0.0")]
public class Plugin : BasePlugin
{
    private const string PluginId = "svs-tm.no-max-order-limit";

    public static ManualLogSource Logger { get; private set; } = default!;

    public override void Load()
    {
        AddComponent<EnvironmentOverloadTracker>();

        Log.LogInfo($"{PluginId} is loaded!");

        Logger = Log;

        Harmony.CreateAndPatchAll(typeof(Patches), PluginId);
    }
}
