using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using HarmonyLib.Tools;

namespace NoMaxOrderLimit;

[BepInProcess("Supermarket Simulator.exe")]
[BepInPlugin("SvS.NoMaxOrderLimit", "NoMaxOrderLimit", "1.0.0")]
public class NoMaxOrderLimitPlugin : BasePlugin
{
    public static ManualLogSource Logger { get; private set; } = default!;

    public override void Load()
    {
        HarmonyLib.Tools.Logger.ChannelFilter = HarmonyLib.Tools.Logger.LogChannel.Info | HarmonyLib.Tools.Logger.LogChannel.Warn;
        // Enable logging to file
        HarmonyFileLog.Enabled = true;
        // Optional: specify path to the log file to generate
        HarmonyFileLog.FileWriterPath = "C:\\Users\\Noe\\Desktop\\log.txt";

        AddComponent<EnvironmentOverloadTracker>();

        Log.LogInfo($"NoMaxOrderLimitPlugin is loaded!");

        Logger = Log;

        Harmony.CreateAndPatchAll(typeof(Patches), "SvS.NoMaxOrderLimit");


    }
}
