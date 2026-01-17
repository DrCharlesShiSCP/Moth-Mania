using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class LadderClimb3D : MonoBehaviour
{
    [Header("Ladder Settings")]
    public float climbSpeed = 3f;

    [Header("Player Settings")]
    public string playerTag = "Player";

    BoxCollider _col;

    void Reset()
    {
        _col = GetComponent<BoxCollider>();
        _col.isTrigger = true;
    }

    void Awake()
    {
        _col = GetComponent<BoxCollider>();
        _col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (other.TryGetComponent(out OurCharacterController c))
            c.RegisterLadder(this);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (other.TryGetComponent(out OurCharacterController c))
            c.UnregisterLadder(this);
    }
}
