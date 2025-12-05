using System.Collections.Generic;
using UnityEngine;

public class PressurePlateTrigger : MonoBehaviour
{
    [Header("Doors (optional)")]
    public DoorRotator[] doors;

    [Header("Barriers (objects to hide)")]
    public GameObject[] barriers;

    [Header("Activation Settings")]
    public string playerTag = "Player";
    public bool useMassCheck = true;
    public float minimumMass = 1f;

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
        if (other.CompareTag(playerTag))
            return true;

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return false;

        if (useMassCheck)
            return rb.mass >= minimumMass;

        return true;
    }

    private void UpdatePlateState()
    {
        bool plateActive = _thingsOnPlate.Count > 0;

        // Doors
        foreach (var door in doors)
        {
            if (door == null) continue;
            if (plateActive)
                door.OpenDoor();
            else
                door.CloseDoor();
        }

        // Barriers: hide/show children instead of disabling the whole GameObject
        foreach (var barrier in barriers)
        {
            if (barrier == null) continue;

            foreach (Transform child in barrier.transform)
            {
                child.gameObject.SetActive(!plateActive); // pressed = hide, released = show
            }

            // Also disable colliders on the barrier itself
            var colliders = barrier.GetComponents<Collider>();
            foreach (var col in colliders)
            {
                if (col != null)
                    col.enabled = !plateActive;
            }
        }
    }
}
