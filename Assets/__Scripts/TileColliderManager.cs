using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TileColliderManager : MonoBehaviour
{
    public Tilemap blockTilemap;
    public Tilemap platformTilemap;

    private void Awake()
    {
        transform.position = Vector3.zero;
    }

    // Start is called before the first frame update
    void Start()
    {
        Vector3 size = blockTilemap.cellSize;

        List<Vector3> tilePositions = new List<Vector3>();
        // Get all tile positions
        foreach (Vector3Int position in blockTilemap.cellBounds.allPositionsWithin)
        {
            if (blockTilemap.HasTile(position))
            {
                tilePositions.Add(blockTilemap.CellToWorld(position) + new Vector3(size.x * 0.5f, 0f, 0f));
            }
        }

        // Sort by x then y position
        tilePositions.Sort((a, b) =>
        {
            if (a.x < b.x) return -1;
            if (a.x > b.x) return 1;

            if (a.y < b.y) return -1;
            if (a.y > b.y) return 1;

            return 0;
        });

        // Reduce to continuous columns then create box colliders

        List<List<Vector3>> columns = new List<List<Vector3>>();
        List<Vector3> column = new List<Vector3>();
        foreach (Vector3 current in tilePositions)
        {
            // In a column
            if (column.Count > 0)
            {
                Vector3 previous = column[column.Count - 1];
                // End of column, add to columns, get ready for a new one
                if (current.y > previous.y + size.y || current.x != previous.x)
                {
                    columns.Add(column);
                    column = new List<Vector3>();
                }
            }
            column.Add(current);
        }
        if (column.Count > 0) columns.Add(column);

        // Sort by start y then start x
        columns.Sort((a, b) =>
        {
            if (a[0].y < b[0].y) return -1;
            if (a[0].y > b[0].y) return 1;

            if (a[0].x < b[0].x) return -1;
            if (a[0].x > b[0].x) return 1;

            return 0;
        });

        Vector3 origin = columns[0][0];
        int width = 0;
        int height = columns[0].Count;
        List<Vector3> last = columns[0];
        foreach (List<Vector3> current in columns)
        {
            if (current.Count != last.Count || current[0].x > last[0].x + size.x || current[0].y != last[0].y)
            {
                CreateBlock(origin, width, height);
                origin = current[0];
                width = 0;
                height = current.Count;
            }
            width++;
            last = current;
        }
        CreateBlock(origin, width, last.Count);
    }

    private void CreateBlock(Vector3 origin, int width, int height)
    {
        float x = blockTilemap.cellSize.x;
        float y = blockTilemap.cellSize.y;
        float w = x * width;
        float h = y * height;

        BoxCollider coll = gameObject.AddComponent<BoxCollider>();
        coll.center = origin + new Vector3(w / 2f - x * 0.5f, h / 2f, 0f);
        coll.size = new Vector3(w, h, 1f);
    }
}
