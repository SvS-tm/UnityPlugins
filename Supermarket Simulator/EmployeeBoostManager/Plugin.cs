using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace EmployeeBoostManager;

[BepInProcess("Supermarket Simulator.exe")]
[BepInPlugin(PluginId, "Employee Boost Manager", "1.2.0")]
public class Plugin : BasePlugin
{
    private const string PluginId = "svs-tm.employee-boost-manager";

    public static ManualLogSource Logger { get; private set; } = default!;
    public static Configuration Configuration { get; private set; } = default!;

    public override void Load()
    {
        Logger = Log;
        Configuration = new Configuration(Config);

        AddComponent<EmployeeBoostMenu>();
        Log.LogInfo("[BoostFix v2] Native local boost actions, verified purchases, and live three-segment meters enabled (single-player).");
        Log.LogInfo("Worker group tabs enabled; selections persist per tab and purchases apply to the current tab only.");

        Log.LogInfo($"{PluginId} is loaded!");
    }
}
