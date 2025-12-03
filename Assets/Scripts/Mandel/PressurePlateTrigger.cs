using System.Collections.Generic;
using UnityEngine;

public class PressurePlateTrigger : MonoBehaviour
{
    [Header("Door")]
    public DoorRotator door;

    [Header("Activation Settings")]
    public string playerTag = "Player";
    public bool useMassCheck = true;
    public float minimumMass = 1f;  // how heavy an object must be to trigger

    // Track everything currently on the plate
    private readonly HashSet<Collider> _thingsOnPlate = new HashSet<Collider>();

    private void OnTriggerEnter(Collider other)
    {
        if (!IsValidActivator(other)) return;

        _thingsOnPlate.Add(other);
        UpdatePlateState();
    }

    private void OnTriggerExit(Collider other)
    {
        if (_thingsOnPlate.Contains(other))
        {
            _thingsOnPlate.Remove(other);
            UpdatePlateState();
        }
    }

    private bool IsValidActivator(Collider other)
    {
        // 1) Player always allowed if tagged
        if (other.CompareTag(playerTag))
            return true;

        // 2) Objects: must have a Rigidbody
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return false;

        // 3) Optional mass check
        if (useMassCheck)
        {
            return rb.mass >= minimumMass;
        }

        return true; // any rigidbody object if mass check is off
    }

    private void UpdatePlateState()
    {
        if (_thingsOnPlate.Count > 0)
        {
            // Something is on the plate → open
            door.OpenDoor();
        }
        else
        {
            // Plate is empty → close
            door.CloseDoor();
        }
    }
}
