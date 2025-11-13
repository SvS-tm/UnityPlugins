using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MyBox;
using UnityEngine;
using Utilities;

namespace UnlimitedRestockers;

public class Patches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(EmployeeManager), nameof(EmployeeManager.SpawnRestocker))]
    public static bool Prefix(EmployeeManager __instance, int restockerID)
    {
        if (__instance.m_RestockerSpawnPositions.Count < restockerID)
        {
            Plugin.Logger.LogInfo($"Trying to add new positions: {restockerID}");

            var newArray = new Il2CppReferenceArray<Transform>(restockerID);

            __instance.m_RestockerSpawnPositions.Il2CppCopyTo(newArray);

            for (var index = __instance.m_RestockerSpawnPositions.Count; index < restockerID; ++index)
            {
                Plugin.Logger.LogInfo($"Getting last pos: {index - 1} of {newArray.Count}");

                var lastRestockerPosition = newArray[index - 1];

                var positionWrapper = new GameObject("Patched_Restocker_Position");

                if (lastRestockerPosition != null)
                {
                    Plugin.Logger.LogInfo($"Success: {lastRestockerPosition.transform.position}");

                    positionWrapper.transform.SetParent(lastRestockerPosition.transform.parent, false);

                    Plugin.Logger.LogInfo($"Changing coordinates: {lastRestockerPosition.transform.position}");

                    positionWrapper.transform.position = new Vector3(lastRestockerPosition.position.x + 1, lastRestockerPosition.position.y, lastRestockerPosition.position.z);

                    Plugin.Logger.LogInfo($"Setting new pos: {index} of {newArray.Count}");
                }

                newArray[index] = positionWrapper.transform;

                Plugin.Logger.LogInfo($"Added: {positionWrapper.transform.position}");
            }

            __instance.m_RestockerSpawnPositions = newArray;
        }

        var idManager = Singleton<IDManager>.Instance;

        if (idManager != null && idManager.m_Restockers.Count < restockerID)
        {
            Plugin.Logger.LogInfo("Trying to add new SOs...");

            for (var index = idManager.m_Restockers.Count; index < restockerID; ++index)
            {
                var newSO = Object.Instantiate(idManager.m_Restockers[^1]);

                newSO.ID = index + 1;
                newSO.DailyWage = Plugin.Configuration.DailyWage.Value;

                idManager.m_Restockers.Add(newSO);

                Plugin.Logger.LogInfo($"Added SO: {newSO.ID}");
            }
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EmployeeManager), nameof(EmployeeManager.HireRestocker))]
    public static void Postfix(EmployeeManager __instance, int restockerID, float hiringCost)
    {
        var restockerManager = Singleton<RestockerManager>.Instance;

        if (restockerManager != null)
        {
            Plugin.Logger.LogInfo($"Setting new management data: {restockerID}");

            restockerManager.SetRestockerManagementData(new RestockerManagementData(restockerID, false));
        }
    }
}
