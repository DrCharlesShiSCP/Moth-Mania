using UnityEngine;
using UnityEngine.SceneManagement;

public class EndDoor : MonoBehaviour
{
    [Header("Scene Loading")]
    [Tooltip("Name of the scene to load when activated.")]
    public string sceneName;
    [Header("Moth Requirement")]
    [Tooltip("I want to kms lol it's 2am")]
    public bool EnoughMoths;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EnoughMoths = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            if (EnoughMoths)
            {
                Debug.Log("Level Complete!");
                // Here you can add code to load the next level or show a completion screen
                LoadNextScene();
            }
            else
            {
                Debug.Log("Not enough moths to exit!");
            }
        }
    }
    public void LoadNextScene()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[EndDoor] Scene name is empty. Assign one in the Inspector.");
            return;
        }

        // Check if scene is valid before trying to load
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.Log($"[EndDoor] Loading scene: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError($"[EndDoor] Scene '{sceneName}' cannot be found or is not added to Build Settings.");
        }
    }
}
