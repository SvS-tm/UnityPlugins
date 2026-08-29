using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using Clerk = SupermarketSimulator.Clerk.Clerk;
using NetworkEmployeeManager = __Project__.Scripts.Multiplayer.NetworkEmployeeManager;

namespace UnlimitedRestockers;

// Observe the native game's successful path without changing arguments, results,
// worker collections, money, or management data. This is not a spawn replacement.
public static class HiringDiagnostics
{
    private static int sequence;

    [HarmonyPrefix, HarmonyPatch(typeof(RestockerItem), nameof(RestockerItem.Hire))]
    public static void BeforeGameButton(RestockerItem __instance) => Record("Game UI Hire ENTER", () =>
        $"id={__instance.RestockerId}, hired={__instance.Hired}; {DescribeManagers()}");

    [HarmonyPostfix, HarmonyPatch(typeof(RestockerItem), nameof(RestockerItem.Hire))]
    public static void AfterGameButton(RestockerItem __instance) => Record("Game UI Hire EXIT", () =>
        $"id={__instance.RestockerId}, hired={__instance.Hired}; {DescribeManagers()}");

    [HarmonyPrefix, HarmonyPatch(typeof(EmployeeManager), nameof(EmployeeManager.HireRestocker))]
    public static void BeforeHire(EmployeeManager __instance, int __0, float __1) => Record("HireRestocker ENTER", () =>
        $"requestedId={__0}, cost={__1}; {DescribeManager(__instance)}");

    [HarmonyPostfix, HarmonyPatch(typeof(EmployeeManager), nameof(EmployeeManager.HireRestocker))]
    public static void AfterHire(EmployeeManager __instance, int __0) => Record("HireRestocker EXIT", () =>
        $"requestedId={__0}; {DescribeManager(__instance)}");

    [HarmonyPrefix, HarmonyPatch(typeof(EmployeeManager), nameof(EmployeeManager.SpawnRestocker))]
    public static void BeforeSpawn(EmployeeManager __instance, int __0) => Record("SpawnRestocker ENTER", () =>
        $"requestedId={__0}; {DescribeManager(__instance)}");

    [HarmonyPostfix, HarmonyPatch(typeof(EmployeeManager), nameof(EmployeeManager.SpawnRestocker))]
    public static void AfterSpawn(EmployeeManager __instance, int __0) => Record("SpawnRestocker EXIT", () =>
        $"requestedId={__0}; {DescribeManager(__instance)}");

    [HarmonyPrefix, HarmonyPatch(typeof(EmployeeGenerator), nameof(EmployeeGenerator.SpawnRestocker))]
    public static void BeforeGenerator(Clerk __0) => Record("EmployeeGenerator.SpawnRestocker ENTER", () =>
        $"prefab={DescribeClerk(__0)}");

    [HarmonyPostfix, HarmonyPatch(typeof(EmployeeGenerator), nameof(EmployeeGenerator.SpawnRestocker))]
    public static void AfterGenerator(Clerk __result) => Record("EmployeeGenerator.SpawnRestocker EXIT", () =>
        $"result={DescribeClerk(__result)}");

    [HarmonyPrefix, HarmonyPatch(typeof(EmployeeManager), nameof(EmployeeManager.SpawnRestockerNetwork))]
    public static void BeforeRegister(EmployeeManager __instance, Clerk __0) => Record("SpawnRestockerNetwork ENTER", () =>
        $"clerk={DescribeClerk(__0)}; {DescribeManager(__instance)}");

    [HarmonyPostfix, HarmonyPatch(typeof(EmployeeManager), nameof(EmployeeManager.SpawnRestockerNetwork))]
    public static void AfterRegister(EmployeeManager __instance, Clerk __0) => Record("SpawnRestockerNetwork EXIT", () =>
        $"clerk={DescribeClerk(__0)}; {DescribeManager(__instance)}");

    [HarmonyPrefix, HarmonyPatch(typeof(NetworkEmployeeManager), nameof(NetworkEmployeeManager.HireRestocker_Request))]
    public static void BeforeNetworkRequest(int __0) => Record("Network Hire REQUEST", () => $"id={__0}");

    [HarmonyPrefix, HarmonyPatch(typeof(NetworkEmployeeManager), nameof(NetworkEmployeeManager.HireRestocker_Broadcast))]
    public static void BeforeNetworkBroadcast(int __0) => Record("Network Hire BROADCAST", () => $"id={__0}");

    [HarmonyPrefix, HarmonyPatch(typeof(NetworkEmployeeManager), nameof(NetworkEmployeeManager.HireRestocker_RPC))]
    public static void BeforeNetworkRpc(int __0) => Record("Network Hire RPC", () => $"id={__0}");

    [HarmonyPostfix, HarmonyPatch(typeof(Clerk), nameof(Clerk.Start))]
    public static void AfterClerkStart(Clerk __instance) => Record("Clerk.Start EXIT", () =>
        $"clerk={DescribeClerk(__instance)}; {DescribeManagers()}");

    [HarmonyPrefix, HarmonyPatch(typeof(RestockerManager), nameof(RestockerManager.SetRestockerManagementData))]
    public static void BeforeManagementData(RestockerManagementData __0) => Record("SetRestockerManagementData ENTER", () =>
        __0 == null ? "data=null" : $"id={__0.RestockerID}");

    public static void RecordManager(string stage, EmployeeManager manager) =>
        Record(stage, () => DescribeManager(manager));

    private static void Record(string stage, Func<string> snapshot)
    {
        if (!Plugin.Configuration.TraceHiring.Value)
            return;

        // A diagnostic failure must not interrupt the original hire method.
        try
        {
            Plugin.Logger.LogInfo($"[HireTrace {System.Threading.Interlocked.Increment(ref sequence)} frame={Time.frameCount}] {stage}: {snapshot()}");
        }
        catch (Exception exception)
        {
            Plugin.Logger.LogWarning($"[HireTrace] Could not read {stage}: {exception.Message}");
        }
    }

    private static string DescribeManager(EmployeeManager manager)
    {
        if (manager == null)
            return "manager=null";

        var saved = new List<int>();
        var active = new List<string>();

        if (manager.m_RestockersData != null)
        {
            foreach (var id in manager.m_RestockersData)
                saved.Add(id);
        }

        if (manager.m_ActiveRestockers != null)
        {
            foreach (var clerk in manager.m_ActiveRestockers)
                active.Add(DescribeClerk(clerk));
        }

        return $"manager={manager.GetInstanceID()}, object={manager.gameObject.name}, scene={manager.gameObject.scene.name}, " +
            $"enabled={manager.isActiveAndEnabled}, saved=[{string.Join(",", saved)}], active=[{string.Join("; ", active)}], " +
            $"spawnSlots={manager.m_RestockerSpawnPositions?.Count}, max={EmployeeManager.MAX_RESTOCKER_COUNT}";
    }

    private static string DescribeClerk(Clerk? clerk)
    {
        return clerk == null ? "null" :
            $"Clerk(id={clerk.EmployeeId}, type={clerk.Type}, name={clerk.gameObject.name}, instance={clerk.GetInstanceID()}, active={clerk.gameObject.activeInHierarchy})";
    }

    private static string DescribeManagers()
    {
        var objects = UnityEngine.Object.FindObjectsByType(Il2CppType.Of<EmployeeManager>(),
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var descriptions = new List<string>();

        foreach (var item in objects)
        {
            var manager = item.TryCast<EmployeeManager>();
            if (manager != null)
                descriptions.Add(DescribeManager(manager));
        }

        return $"networkSingletonPresent={NetworkEmployeeManager.HasInstance}; managers: {string.Join(" | ", descriptions)}";
    }
}
