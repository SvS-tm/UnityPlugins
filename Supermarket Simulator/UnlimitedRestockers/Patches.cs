using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Photon.Pun;
using UnityEngine;
using Utilities;

namespace UnlimitedRestockers;

public class Patches
{
    // v1.6.0(223) compares against the literal 6, not MAX_RESTOCKER_COUNT.
    public const int OriginalRestockerLimit = 6;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EmployeeManager), nameof(EmployeeManager.HandleCorruptEmployeeData))]
    public static void ProtectExtraSavedRestockers(EmployeeManager __instance,
        out Il2CppSystem.Collections.Generic.List<int>? __state)
    {
        __state = null;
        var saved = __instance.m_RestockersData;
        if (saved == null)
            return;

        var originalIds = new Il2CppSystem.Collections.Generic.List<int>();
        var hasExtraIds = false;
        foreach (var id in saved)
        {
            if (id > OriginalRestockerLimit)
                hasExtraIds = true;
            else
                originalIds.Add(id);
        }

        if (!hasExtraIds)
            return;

        // This native cleanup also hard-codes ID > 6 as corrupt. Give only
        // this method a vanilla-ID view, leaving SaveManager's actual list and
        // management records untouched. Other employee cleanup still runs.
        __state = saved;
        __instance.m_RestockersData = originalIds;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(EmployeeManager), nameof(EmployeeManager.HandleCorruptEmployeeData))]
    public static void RestoreExtraSavedRestockers(EmployeeManager __instance,
        Il2CppSystem.Collections.Generic.List<int>? __state)
    {
        // Restore the exact saved-list reference even if native cleanup throws.
        // LoadData subsequently enumerates it through SpawnRestocker as usual.
        if (__state != null)
            __instance.m_RestockersData = __state;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EmployeeManager), nameof(EmployeeManager.SpawnRestocker))]
    public static bool SpawnExtraRestocker(EmployeeManager __instance, int restockerID)
    {
        if (restockerID <= OriginalRestockerLimit)
            return true;

        try
        {
            if (PhotonNetwork.IsConnected)
                throw new InvalidOperationException("Extra restockers currently support single-player only.");

            // Also prepare extra IDs when the game spawns workers from a save.
            EnsureRestockerCapacity(__instance, restockerID);
            if (__instance.GetRestockerByID(restockerID) != null)
                return false;

            var generator = EmployeeGenerator.Instance;
            if (generator == null)
                throw new InvalidOperationException("EmployeeGenerator is not available.");

            var setup = IDManager.Instance.RestockerSO(restockerID);
            var position = __instance.m_RestockerSpawnPositions[restockerID - 1];

            // Match the native OFFLINE branch after its hard-coded ID guard:
            // pool a modern Clerk, assign its ID, register it, then notify once.
            // HireRestocker owns payment and saved IDs. Clerk's initialization
            // loads/creates management data; do not reset it in a hire postfix.
            var clerk = generator.SpawnRestocker(setup.RestockerPrefab, position.position, position.rotation);
            if (clerk == null)
                throw new InvalidOperationException("EmployeeGenerator returned no Clerk.");

            clerk.EmployeeId = restockerID;
            __instance.m_ActiveRestockers.Add(clerk);
            Plugin.Logger.LogInfo($"Spawned extra restocker ID {restockerID} as Clerk ({clerk.gameObject.name}).");
            __instance.onRestockerHired?.Invoke();
        }
        catch (Exception exception)
        {
            Plugin.Logger.LogError($"Extra restocker spawn failed for ID {restockerID}: {exception}");
        }

        // Never fall through to the native no-op for IDs above six.
        return false;
    }

    public static void EnsureRestockerCapacity(EmployeeManager manager, int restockerID)
    {
        if (restockerID <= OriginalRestockerLimit)
            return;

        var idManager = IDManager.Instance;
        if (idManager == null || idManager.m_Restockers == null)
            throw new InvalidOperationException("Restocker definitions are not available.");

        var positions = manager.m_RestockerSpawnPositions;
        if (positions == null || positions.Count == 0 || positions[positions.Count - 1] == null)
            throw new InvalidOperationException("No valid restocker spawn position is available to extend.");

        var originalSetups = new List<RestockerSO>();
        var existingIds = new HashSet<int>();
        foreach (var setup in idManager.m_Restockers)
        {
            if (setup == null)
                continue;

            existingIds.Add(setup.ID);
            if (setup.ID >= 1 && setup.ID <= OriginalRestockerLimit && setup.RestockerPrefab != null)
                originalSetups.Add(setup);
        }

        if (originalSetups.Count == 0)
            throw new InvalidOperationException("No official restocker Clerk prefab is available.");

        if (positions.Count < restockerID)
        {
            var newArray = new Il2CppReferenceArray<Transform>(restockerID);
            positions.Il2CppCopyTo(newArray);
            for (var index = positions.Count; index < restockerID; ++index)
            {
                var previous = newArray[index - 1];
                var position = new GameObject($"Patched_Restocker_Position_{index + 1}").transform;
                position.SetParent(previous.parent, false);
                position.position = previous.position + Vector3.right;
                position.rotation = previous.rotation;
                newArray[index] = position;
            }

            manager.m_RestockerSpawnPositions = newArray;
        }

        for (var id = OriginalRestockerLimit + 1; id <= restockerID; ++id)
        {
            if (existingIds.Contains(id))
                continue;

            // The six official RestockerSO assets reference Clerk model variants.
            // Clone a random setup with its prefab intact, preserving the model,
            // animator, and components. No obsolete Restocker class or guessed
            // prefab paths are used here.
            var template = originalSetups[UnityEngine.Random.Range(0, originalSetups.Count)];
            var newSetup = UnityEngine.Object.Instantiate(template);
            newSetup.ID = id;
            newSetup.DailyWage = Plugin.Configuration.DailyWage.Value;
            idManager.m_Restockers.Add(newSetup);
            Plugin.Logger.LogInfo($"Added SO: {id}, prefab: {newSetup.RestockerPrefab.gameObject.name}");
        }

        var requestedSetup = idManager.RestockerSO(restockerID);
        if (requestedSetup == null || requestedSetup.RestockerPrefab == null ||
            manager.m_RestockerSpawnPositions[restockerID - 1] == null)
            throw new InvalidOperationException($"Restocker {restockerID} has incomplete spawn data.");
    }
}
