using System;
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using MyBox;
using UnityEngine;
using Utilities;

namespace UnlimitedRestockers;

public class RestockersManager(IntPtr ptr) : MonoBehaviour(ptr)
{
	private static DateTime lastActionTime;

	[NonSerialized]
	public Setting Setting = default!;

	private static bool shiftDown;

	private static bool ctrlDown;

	private static bool altDown;

	public void Awake()
	{
		StartCoroutine(Init().WrapToIl2Cpp());
	}

	private IEnumerator Init()
	{
		EmployeeManager manager;

		while ((manager = GetEmployeeManager()) == null)
			yield return null;

		var originalOnRestockerFired = manager.onRestockerFired;
		var originalOnRestockerHired = manager.onRestockerHired;

		manager.onRestockerFired = new Action<Restocker>
		(
			(restocker) =>
			{
				UnlimitedRestockersPlugin.Logger.LogInfo($"Restocker fired: {restocker.RestockerID}, {restocker.name}");

				originalOnRestockerFired?.Invoke(restocker);
			}
		);

		manager.onRestockerHired = new Action
		(
			() =>
			{
				UnlimitedRestockersPlugin.Logger.LogInfo($"Restocker hired");

				originalOnRestockerHired?.Invoke();
			}
		);

		UnlimitedRestockersPlugin.Logger.LogInfo("Manager is Initialized!");
	}

	public void Update()
	{
        if (Input.GetKeyDown(Setting.KEY_HIRE))
		{
			if ((!Setting.KEY_HIRE_SHIFT || shiftDown) && (!Setting.KEY_HIRE_CTRL || ctrlDown) && (!Setting.KEY_HIRE_ALT || altDown))
			{
				HireRestocker();
			}
		}
		else if (Input.GetKeyDown(Setting.KEY_FIRE))
		{
			if ((!Setting.KEY_FIRE_SHIFT || shiftDown) && (!Setting.KEY_FIRE_CTRL || ctrlDown) && (!Setting.KEY_FIRE_ALT || altDown))
			{
				FireRestocker();
			}
		}
		else if (Input.GetKeyDown(KeyCode.LeftShift))
		{
			shiftDown = true;
		}
		else if (Input.GetKeyUp(KeyCode.LeftShift))
		{
			shiftDown = false;
		}
		else if (Input.GetKeyDown(KeyCode.LeftControl))
		{
			ctrlDown = true;
		}
		else if (Input.GetKeyUp(KeyCode.LeftControl))
		{
			ctrlDown = false;
		}
		else if (Input.GetKeyDown(KeyCode.LeftAlt))
		{
			altDown = true;
		}
		else if (Input.GetKeyUp(KeyCode.LeftAlt))
		{
			altDown = false;
		}

		var manager = GetEmployeeManager();

		if (manager != null)
		{
			foreach (var restocker in manager.m_ActiveRestockers)
			{
                AttachBoardToRestocker(restocker);

				var delta = Input.mouseScrollDelta.y;

                if (delta != 0)
                {
                    var labels = restocker.gameObject.transform.IL2CppGetComponentsInChildren<UiLabel>(true);

                    if (labels != null && labels is [UiLabel label])
                    {
						var ctrl = Input.GetKey(KeyCode.LeftControl);

						if (ctrl)
						{
							delta *= 0.01f;
                        }
						else
						{
                            delta *= 0.05f;
                            label.Configure(offset: label.Offset + new Vector3(0, delta));
						}

                        UnlimitedRestockersPlugin.Logger.LogInfo($"[{restocker.RestockerID}]POS: {label.transform.position}{restocker.gameObject.transform.position}");
                        UnlimitedRestockersPlugin.Logger.LogInfo($"[{restocker.RestockerID}]SCALE: {label.transform.localScale}");
                    }
                    else
                    {
                        UnlimitedRestockersPlugin.Logger.LogInfo($"Label not found {restocker.RestockerID}");
                    }
                }
            }
		}
	}

	private void HireRestocker()
	{
		var now = DateTime.Now;
		
		if (now - lastActionTime >= TimeSpan.FromSeconds(Setting.COOLDOWN_HIRE.Value))
		{
			lastActionTime = now;

			GetEmployeeManager().HireRestocker(Setting.RESTOCKER_ID.Value, Setting.COST_HIRE.Value);
		}
	}

	private void FireRestocker()
	{
		GetEmployeeManager().FireRestocker(Setting.RESTOCKER_ID.Value);
	}

	private static void AttachBoardToRestocker(Restocker restocker)
	{
		if (!(restocker.gameObject is GameObject gameObject && gameObject != null))
			return;

		var components = gameObject.transform.IL2CppGetComponentsInChildren<UiLabel>(true);

		if (components != null && components.Length > 0)
		{
			return;
		}

		UnlimitedRestockersPlugin.Logger.LogInfo($"Attaching board: {restocker.RestockerID}, {gameObject.name}");

		var holder = new GameObject("NameLabel_UI");

		holder.transform.SetParent(gameObject.transform, false);

		var label = holder.Il2CppAddComponent<UiLabel>();

		label?.Configure(restocker.RestockerID.ToString());
	}

    private static EmployeeManager GetEmployeeManager()
	{
		return Singleton<EmployeeManager>.Instance;
	}
}
