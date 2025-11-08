using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace UnlimitedRestockers;

[BepInProcess("Supermarket Simulator.exe")]
[BepInPlugin("SvS.UnlimitedRestockers", "UnlimitedRestockers", "1.1.0")]
public class UnlimitedRestockersPlugin : BasePlugin
{
    public static ManualLogSource Logger { get; private set; } = default!;

    public override void Load()
    {
        var setting = new Setting(Config);

        var component = AddComponent<RestockersManager>();

        component.Setting = setting;

        Log.LogInfo("UnlimitedRestockersPlugin is loaded!");

        Logger = Log;
    }
}
