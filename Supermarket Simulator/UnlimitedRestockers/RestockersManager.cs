using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.Attributes;
using Photon.Pun;
using System.Collections;
using Clerk = SupermarketSimulator.Clerk.Clerk;
using UnityEngine;
using UnityEngine.InputSystem;
using Utilities;

namespace UnlimitedRestockers;

public class RestockersManager(IntPtr ptr) : MonoBehaviour(ptr)
{
	private static EmployeeManager? employeeManager;
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

	[HideFromIl2Cpp]
	private IEnumerator Init()
	{
		EmployeeManager? manager;
		
		while ((manager = GetEmployeeManager()) == null)
			yield return null;

		HiringDiagnostics.RecordManager("Plugin selected EmployeeManager", manager);

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

		var originalOnClerkFired = manager.onClerkFired;
		var originalOnRestockerHired = manager.onRestockerHired;

		manager.onClerkFired = new Action<Clerk>
		(
			(clerk) =>
			{
				originalOnClerkFired?.Invoke(clerk);

				using (var manipulator = restockersIdsPool.Manipulate())
				{
					manipulator.Release(clerk.EmployeeId);

					Plugin.Logger.LogInfo($"Restocker fired: {clerk.EmployeeId}, [{string.Join(",", manipulator.GetReservedIds())}]");
				}
			}
		);

		manager.onRestockerHired = new Action
		(
			() =>
			{
				originalOnRestockerHired?.Invoke();

	                var manager = GetEmployeeManager();

	                if (manager == null)
	                    return;

                using var manipulator = restockersIdsPool.Manipulate();

                Plugin.Logger.LogInfo($"Restocker hired, [{string.Join(",", manipulator.GetReservedIds())}]");

                foreach (var clerk in manager.m_ActiveRestockers)
                {
                    manipulator.Reserve(clerk.EmployeeId);

                    AttachBoardToRestocker(clerk);
                }
            }
		);

		Plugin.Logger.LogInfo("Manager is Initialized!");
	}

	[HideFromIl2Cpp]
	public static void HireRestocker()
	{
		var manager = GetEmployeeManager();
		if (manager == null)
			return;

		// Extra-ID definitions are local; do not attempt an unsynchronized hire
		// in multiplayer. This patch follows the verified single-player path.
		if (PhotonNetwork.IsConnected)
		{
			Plugin.Logger.LogWarning("Plugin hiring currently supports single-player only.");
			return;
		}

		var hiringCost = Plugin.Configuration.HireCost.Value;
		if (!float.IsFinite(hiringCost) || hiringCost < 0)
		{
			Plugin.Logger.LogError("HiringCost must be a finite, non-negative value.");
			return;
		}

		// Avoid IDs that are still in the save but have not spawned yet, as well
		// as active IDs. Failed attempts must not reserve an ID in our pool.
		var occupiedIds = new HashSet<int>();
		foreach (var clerk in manager.m_ActiveRestockers)
			occupiedIds.Add(clerk.EmployeeId);
		foreach (var savedId in manager.m_RestockersData)
			occupiedIds.Add(savedId);

		var id = 1;
		while (occupiedIds.Contains(id))
			id++;

		try
		{
			var moneyManager = MoneyManager.Instance;
			if (moneyManager == null)
				throw new InvalidOperationException("MoneyManager is not available.");

			if (!moneyManager.HasMoney(hiringCost))
			{
				Plugin.Logger.LogWarning($"Cannot hire restocker {id}: not enough money for {hiringCost}.");
				return;
			}

			// Validate before the game's HireRestocker debits money. The spawn
			// prefix repeats capacity preparation so loading saves works too.
			Patches.EnsureRestockerCapacity(manager, id);
			if (EmployeeGenerator.Instance == null)
				throw new InvalidOperationException("EmployeeGenerator is not available.");

			Plugin.Logger.LogInfo($"Requesting restocker hire: {id}, cost: {hiringCost}");
			try
			{
				manager.HireRestocker(id, hiringCost);
			}
			catch (Exception exception)
			{
				Plugin.Logger.LogError($"HireRestocker failed for ID {id}: {exception}");
			}

			if (manager.GetRestockerByID(id) != null)
				return;

			// Native HireRestocker charges BEFORE adding this previously unused
			// saved ID. If spawning failed, undo that purchase, not an existing hire.
			if (manager.m_RestockersData.Remove(id))
			{
				moneyManager.MoneyTransition(hiringCost, MoneyManager.TransitionType.STAFF, true);
				Plugin.Logger.LogWarning($"Restocker {id} did not spawn; removed its new saved ID and refunded {hiringCost}.");
			}
			else
			{
				Plugin.Logger.LogWarning($"Restocker {id} was not hired; no saved ID was added.");
			}
		}
		catch (Exception exception)
		{
			Plugin.Logger.LogError($"Cannot hire restocker {id}: {exception}");
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
				manager.FireRestocker(manager.m_ActiveRestockers[0].EmployeeId);
		}
	}

	private static void AttachBoardToRestocker(Clerk clerk)
	{
		if (!(clerk.gameObject is GameObject gameObject && gameObject != null))
			return;

		var components = gameObject.transform.IL2CppGetComponentsInChildren<UiLabel>(true);

		if (components is not [UiLabel label])
		{
			var holder = new GameObject("NameLabel_UI");

			holder.transform.SetParent(gameObject.transform, false);

			label = holder.Il2CppAddComponent<UiLabel>();
		}

		var id = clerk.EmployeeId;

		label.Configure
		(
			() =>
			{
					var employeeManager = GetEmployeeManager();
					var idManager = GetIDManager();

					if (employeeManager == null)
						return id.ToString();

					var activeClerk = employeeManager.GetRestockerByID(id);
					var restockerSO = idManager.RestockerSO(id);

					return activeClerk == null
						? id.ToString()
						: $"{activeClerk.EmployeeId} ({restockerSO.DailyWage}$)";
			}
		);
	}

    private static IDManager GetIDManager()
    {
        return IDManager.Instance;
    }

	private static EmployeeManager? GetEmployeeManager()
	{
		if (employeeManager != null)
			return employeeManager;

		employeeManager = Il2CppUnityExtensions.Il2CppFindFirstObjectByType<EmployeeManager>
		(
			FindObjectsInactive.Include
		);

		return employeeManager;
	}


}
