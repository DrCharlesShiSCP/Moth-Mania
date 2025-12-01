using UnityEngine;

public class PressurePlateTrigger : MonoBehaviour
{
    public DoorRotator door;   // Drag your door here in Inspector
    public string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            door.OpenDoor();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            door.CloseDoor();
        }
    }
}
