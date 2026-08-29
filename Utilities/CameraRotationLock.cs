using UnityEngine;

namespace Utilities;

/// <summary>
/// Temporarily disables the local first-person camera's look input. Locks are
/// reference-counted so multiple plugin menus can be open without restoring
/// camera rotation until the last menu closes.
/// </summary>
public sealed class CameraRotationLock : IDisposable
{
    private static int activeLockCount;
    private static FirstPersonController? controller;
    private static bool previousCameraInteraction;

    private bool isDisposed;

    private CameraRotationLock()
    {
    }

    public static CameraRotationLock Acquire()
    {
        activeLockCount++;
        var result = new CameraRotationLock();
        result.Maintain();
        return result;
    }

    public void Maintain()
    {
        if (isDisposed)
            return;

        if (controller == null)
        {
            controller = Il2CppUnityExtensions.Il2CppFindFirstObjectByType<FirstPersonController>
            (
                FindObjectsInactive.Include
            );

            if (controller == null)
                return;

            previousCameraInteraction = controller.CameraInteraction;
        }

        controller.CameraInteraction = false;
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        activeLockCount = Math.Max(0, activeLockCount - 1);

        if (activeLockCount > 0)
            return;

        if (controller != null)
            controller.CameraInteraction = previousCameraInteraction;

        controller = null;
    }
}
