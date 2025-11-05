using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(BoxCollider))]
[ExecuteAlways] // Allows grid snapping in edit mode
public class LadderClimb3D : MonoBehaviour
{
    [Header("Ladder Settings")]
    [Tooltip("How fast the player moves up/down the ladder.")]
    public float climbSpeed = 3f;

    [Header("Grid Settings")]
    [Tooltip("Tilemap Grid this ladder should snap to.")]
    public Grid grid;

    [Header("Player Settings")]
    [Tooltip("Tag used to identify the player.")]
    public string playerTag = "Player";

    private void Reset()
    {
        // Make sure the collider is a trigger
        BoxCollider col = GetComponent<BoxCollider>();
        col.isTrigger = true;
    }

    private void Update()
    {
        SnapToGrid();
    }

    private void SnapToGrid()
    {
        if (grid == null) return;

        // Convert world position to cell position, then back to world position
        Vector3Int cellPosition = grid.WorldToCell(transform.position);
        Vector3 snappedPosition = grid.GetCellCenterWorld(cellPosition);

        // Snap the ladder object to the grid
        transform.position = snappedPosition;
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb == null) return;

            float vertical = Input.GetAxisRaw("Vertical"); // W/S or Up/Down

            if (Mathf.Abs(vertical) > 0.1f)
            {
                // Disable gravity while climbing
                rb.useGravity = false;

                // Move the player along the Y-axis only
                Vector3 velocity = rb.linearVelocity;
                velocity.y = vertical * climbSpeed;
                rb.linearVelocity = velocity;
            }
            else
            {
                // Stop vertical movement while idle on ladder
                Vector3 velocity = rb.linearVelocity;
                velocity.y = 0f;
                rb.linearVelocity = velocity;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Restore gravity when leaving ladder
                rb.useGravity = true;
            }
        }
    }
}
