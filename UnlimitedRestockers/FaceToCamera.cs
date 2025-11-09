using UnityEngine;

namespace UnlimitedRestockers;

public class FaceToCamera : MonoBehaviour
{
    private Camera? targetCamera;

    public void Configure(Camera? targetCamera = null)
    { 
        this.targetCamera = targetCamera;
    }

    public void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    public void LateUpdate()
    {
        if (!targetCamera || targetCamera is null) 
            return;

        transform.forward = new Vector3
        (
            targetCamera.transform.forward.x, 
            0, 
            targetCamera.transform.forward.z
        );
    }
}
