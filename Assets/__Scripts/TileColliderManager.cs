using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TileColliderManager : MonoBehaviour
{
    public Tilemap tilemap;

    private void Awake()
    {
        transform.position = Vector3.zero;
    }

    // Start is called before the first frame update
    void Start()
    {
        Vector3 size = tilemap.cellSize;

        List<Vector3> tilePositions = new List<Vector3>();
        // Get all tile positions
        foreach (Vector3Int position in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(position))
            {
                tilePositions.Add(tilemap.CellToWorld(position) + new Vector3(size.x * 0.5f, 0f, 0f));
            }
        }

        // Sort by x then y position;
        tilePositions.Sort((a, b) =>
        {
            if (a.x < b.x) return -1;
            if (a.x > b.x) return 1;

            if (a.y < b.y) return -1;
            if (a.y > b.y) return 1;

            return 0;
        });

        // Reduce to continuous columns then create box colliders
        List<Vector3> column = new List<Vector3>();
        foreach (Vector3 current in tilePositions)
        {
            // In a column
            if (column.Count > 0)
            {
                Vector3 previous = column[column.Count - 1];
                // End of column, make a collider, get ready for a new one
                if (current.y > previous.y + size.y || current.x != previous.x)
                {
                    CreateColumn(column);
                    column.Clear();
                }
            }
            column.Add(current);
        }
        // Last column
        CreateColumn(column);

        // Could do another pass and combine columns with the same start/end ys
    }

    private void CreateColumn(List<Vector3> positions)
    {
        Vector3 start = positions[0];
        Vector3 end = positions[positions.Count - 1];

        Debug.Assert(start.x == end.x, "not right");

        float height = tilemap.cellSize.y + (end.y - start.y);
        Vector3 center = new Vector3(start.x, start.y + height * 0.5f, 0f);

        BoxCollider coll = gameObject.AddComponent<BoxCollider>();
        coll.center = center;
        coll.size = new Vector3(tilemap.cellSize.x, height, 1f);
    }
}
