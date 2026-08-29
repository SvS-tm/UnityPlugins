using BepInEx.Unity.IL2CPP.Utils.Collections;
using MyBox;
using System.Collections;
using Clerk = SupermarketSimulator.Clerk.Clerk;
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

	private void HireRestocker()
	{
		var manager = GetEmployeeManager();
		
		if (manager == null)
			return;

		using (var manipulator = restockersIdsPool.Manipulate())
		{
			var id = manipulator.Reserve();

			manager.HireRestocker(id, Plugin.Configuration.HireCost.Value);
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

				var activeClerk = employeeManager.GetRestockerByID(id);
				var restockerSO = idManager.RestockerSO(id);

				return $"{activeClerk.EmployeeId} ({restockerSO.DailyWage}$)";
			}
		);
	}

    private static IDManager GetIDManager()
    {
        return Singleton<IDManager>.Instance;
    }

    private static EmployeeManager GetEmployeeManager()
	{
		return Singleton<EmployeeManager>.Instance;
	}


}
