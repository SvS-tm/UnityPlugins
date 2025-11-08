using UnityEngine;

namespace UnlimitedRestockers;

public class FaceToCamera : MonoBehaviour
{
    public bool UseMainCamera = true;
    public Camera? TargetCamera;

    public void Start()
    {
        if (UseMainCamera && TargetCamera == null)
            TargetCamera = Camera.main;
    }

    public void LateUpdate()
    {
        if (!TargetCamera || TargetCamera is null) 
            return;

        // Face the camera while staying upright
        transform.forward = new Vector3(TargetCamera.transform.forward.x, 0, TargetCamera.transform.forward.z);
    }
}
