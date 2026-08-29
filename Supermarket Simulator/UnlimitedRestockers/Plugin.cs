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
        Log.LogInfo("[HireFix v2] Extra-ID single-player Clerk spawn patch enabled; original IDs 1-6 unchanged.");

        if (Configuration.TraceHiring.Value)
        {
            Harmony.CreateAndPatchAll(typeof(HiringDiagnostics), PluginId + ".hire-trace");
            Log.LogInfo($"[HireTrace v2] Logging enabled. Game={UnityEngine.Application.version}, Unity={UnityEngine.Application.unityVersion}");
        }

        AddComponent<RestockersManager>();
        AddComponent<RestockerSelectionMenu>();

        Log.LogInfo($"{PluginId} is loaded!");
    }
}
