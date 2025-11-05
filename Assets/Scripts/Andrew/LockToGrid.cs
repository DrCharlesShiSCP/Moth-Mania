using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteInEditMode] // Runs only in the Unity Editor
public class LockToGridEditorOnly : MonoBehaviour
{
    [Tooltip("The Grid this object should snap to.")]
    public Grid grid;

    private void OnValidate()
    {
        SnapToGrid();
    }

    private void OnDrawGizmos()
    {
        // Also keep snapping visually while moving in Scene View
        SnapToGrid();
    }

    private void SnapToGrid()
    {
        if (Application.isPlaying || grid == null)
            return;

        // Convert world position to cell position and back to cell center
        Vector3Int cellPosition = grid.WorldToCell(transform.position);
        Vector3 snappedPosition = grid.GetCellCenterWorld(cellPosition);

        // Snap the object’s position to the grid
        transform.position = snappedPosition;
    }
}
