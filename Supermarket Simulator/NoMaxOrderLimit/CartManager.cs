using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace NoMaxOrderLimit;

public class EnvironmentOverloadTracker(IntPtr ptr) : MonoBehaviour(ptr)
{
    public void OnEnable()
    {
        SceneManager.sceneLoaded += (UnityAction<Scene, LoadSceneMode>)OnSceneLoaded;
    }

    public void OnDisable()
    {
        SceneManager.sceneLoaded -= (UnityAction<Scene, LoadSceneMode>)OnSceneLoaded;
    }

    public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Main Scene")
        {
            foreach (var furnitureBox in FindObjectsOfType<FurnitureBox>())
            {
                if (furnitureBox.transform.position.y > 8f)
                {
                    var x = furnitureBox.transform.position.x;
                    var z = furnitureBox.transform.position.z;

                    furnitureBox.transform.position = new Vector3(x, 2f, z);
                }
            }

            foreach (var box in FindObjectsOfType<Box>().Where(box => !box.Racked && box.Full))
            {
                if (box.transform.position.y > 8f)
                {
                    var x = box.transform.position.x;
                    var z = box.transform.position.z;

                    box.transform.position = new Vector3(x, 2f, z);
                }
            }
        }
    }
}
