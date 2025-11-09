using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace NoMaxOrderLimit;

[BepInProcess("Supermarket Simulator.exe")]
[BepInPlugin(PluginId, "NoMaxOrderLimit", "1.0.0")]
public class Plugin : BasePlugin
{
    private const string PluginId = "SvS-Tm.NoMaxOrderLimit";

    public static ManualLogSource Logger { get; private set; } = default!;

    public override void Load()
    {
        AddComponent<EnvironmentOverloadTracker>();

        Log.LogInfo($"{PluginId} is loaded!");

        Logger = Log;

        Harmony.CreateAndPatchAll(typeof(Patches), PluginId);
    }
}
