using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MyBox;
using UnityEngine;
using UnityEngine.InputSystem;
using Utilities;

namespace UnlimitedRestockers;

public class RestockersManager(IntPtr ptr) : MonoBehaviour(ptr)
{
    private readonly IdPool restockersIdsPool = new(6);
	private InputAction hireRestockerAction = default!;
	private InputAction fireRestockerAction = default!;

	private readonly List<IDisposable> resources = new(2);

	public void Awake()
	{
		StartCoroutine(Init().WrapToIl2Cpp());
	}

	public void OnDestroy()
	{
		foreach (var resource in resources)
		{
			resource.Dispose();
		}
	}

	private IEnumerator Init()
	{
		EmployeeManager manager;

		while ((manager = GetEmployeeManager()) == null)
			yield return null;

		hireRestockerAction = InputHelpers.ParseAction("hireRestockerAction", Plugin.Configuration.HireRestockerBinding.Value);
		fireRestockerAction = InputHelpers.ParseAction("fireRestockerAction", Plugin.Configuration.FireRestockerBinding.Value);

		resources.Add
		(
			hireRestockerAction.WithCooldownCallback
			(
				HireRestocker, 
				() => Plugin.Configuration.HireCooldown.Value
			)
		);

		resources.Add
		(
			fireRestockerAction.WithCooldownCallback
			(
				FireRestocker, 
				() => Plugin.Configuration.HireCooldown.Value
			)
		);

        Plugin.Configuration.HireRestockerBinding.SettingChanged += (_, _) =>
        {
            hireRestockerAction.Rebind(Plugin.Configuration.HireRestockerBinding.Value);
        };

        Plugin.Configuration.FireRestockerBinding.SettingChanged += (_, _) =>
        {
            fireRestockerAction.Rebind(Plugin.Configuration.FireRestockerBinding.Value);
        };

        var originalOnRestockerFired = manager.onRestockerFired;
		var originalOnRestockerHired = manager.onRestockerHired;

		manager.onRestockerFired = new Action<Restocker>
		(
			(restocker) =>
			{
				originalOnRestockerFired?.Invoke(restocker);

				using (var manipulator = restockersIdsPool.Manipulate())
				{ 
					manipulator.Release(restocker.RestockerID);

					Plugin.Logger.LogInfo($"Restocker fired: {restocker.RestockerID}, [{string.Join(",", manipulator.GetReservedIds())}]");
				}
			}
		);

		manager.onRestockerHired = new Action
		(
			() =>
			{
				originalOnRestockerHired?.Invoke();

				var manager = GetEmployeeManager();

				using (var manipulator = restockersIdsPool.Manipulate())
				{
					Plugin.Logger.LogInfo($"Restocker hired, [{string.Join(",", manipulator.GetReservedIds())}]");

					foreach (var restocker in manager.m_ActiveRestockers)
					{
						manipulator.Reserve(restocker.RestockerID);

                        AttachBoardToRestocker(restocker);
					}
				}
            }
		);

		Plugin.Logger.LogInfo("Manager is Initialized!");
	}

    private void HireRestocker()
    {
        var manager = GetEmployeeManager();
        var idManager = GetIDManager();

        if (manager == null || idManager == null)
            return;

        using (var manipulator = restockersIdsPool.Manipulate())
        {
            var id = manipulator.Reserve();

            Plugin.Logger.LogMessage($"Reserved id: {id}");

            if (manager.m_RestockerSpawnPositions.Count < id)
            {
                Plugin.Logger.LogInfo($"Trying to add new positions: {id}");

                var newArray = new Il2CppReferenceArray<Transform>(id);

                manager.m_RestockerSpawnPositions.Il2CppCopyTo(newArray);

                for (var index = manager.m_RestockerSpawnPositions.Count; index < id; ++index)
                {
                    Plugin.Logger.LogInfo($"Getting last pos: {index - 1} of {newArray.Count}");

                    var lastRestockerPosition = newArray[index - 1];

                    if (lastRestockerPosition == null)
                        break;

                    Plugin.Logger.LogInfo($"Success: {lastRestockerPosition.transform.position}");

                    var positionWrapper = new GameObject("Patched_Restocker_Position");

                    Plugin.Logger.LogInfo($"Setting parent: {lastRestockerPosition.transform.parent}");

                    positionWrapper.transform.SetParent(lastRestockerPosition.transform.parent, false);

                    Plugin.Logger.LogInfo($"Changing coordinates: {lastRestockerPosition.transform.position}");

                    positionWrapper.transform.position = new Vector3(lastRestockerPosition.position.x + 1, lastRestockerPosition.position.y, lastRestockerPosition.position.z);

                    Plugin.Logger.LogInfo($"Setting new pos: {index} of {newArray.Count}");

                    newArray[index] = positionWrapper.transform;

                    Plugin.Logger.LogInfo($"Added: {positionWrapper.transform.position}");
                }

                manager.m_RestockerSpawnPositions = newArray;
            }

            if (idManager.m_Restockers.Count < id)
            {
                Plugin.Logger.LogInfo("Trying to add new SOs...");

                for (var index = idManager.m_Restockers.Count; index < id; ++index)
                {
                    var newSO = Instantiate(idManager.m_Restockers[^1]);

                    newSO.ID = index + 1;

                    idManager.m_Restockers.Add(newSO);

                    Plugin.Logger.LogInfo($"Added SO: {newSO.ID}");
                }
            }

            manager.HireRestocker(id, Plugin.Configuration.HireCost.Value);

			var restockerSO = idManager.RestockerSO(id);

			if (restockerSO != null)
			{
				restockerSO.DailyWage = Plugin.Configuration.DailyWage.Value;
			}
        }
    }

    private void FireRestocker()
    {
        var manager = GetEmployeeManager();

        if (manager == null)
            return;

        using (var manipulator = restockersIdsPool.Manipulate())
        {
            var id = manipulator.PickToRelease();

            if (id != -1)
                manager.FireRestocker(id);
            else if (manager.m_ActiveRestockers.Count > 0)
                manager.FireRestocker(manager.m_ActiveRestockers[0].RestockerID);
        }
    }

    private static void AttachBoardToRestocker(Restocker restocker)
	{
		if (!(restocker.gameObject is GameObject gameObject && gameObject != null))
			return;

		var components = gameObject.transform.IL2CppGetComponentsInChildren<UiLabel>(true);

		if (components is not [UiLabel label])
		{
            var holder = new GameObject("NameLabel_UI");

            holder.transform.SetParent(gameObject.transform, false);

            label = holder.Il2CppAddComponent<UiLabel>();
        }

        label.Configure(restocker.RestockerID.ToString());
	}

    private static EmployeeManager GetEmployeeManager()
	{
		return Singleton<EmployeeManager>.Instance;
	}

	private static IDManager GetIDManager()
	{
		return Singleton<IDManager>.Instance;
	}
}
