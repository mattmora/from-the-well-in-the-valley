using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TileColliderManager : MonoBehaviour
{
    public Tilemap blockTilemap;
    public Tilemap platformTilemap;
    public GameObject platformPrefab;
    public Tilemap waterTilemap;
    public GameObject waterParent;

    private void Awake()
    {
        transform.position = Vector3.zero;
    }

    // Start is called before the first frame update
    void Start()
    {
        if (blockTilemap != null)
        {
            List<List<Vector3>> blockColumns = ReduceTiles(blockTilemap);
            if (blockColumns.Count > 0)
            {
                Vector3 bottomLeft = blockColumns[0][0];
                int width = 0;
                int height = blockColumns[0].Count;
                List<Vector3> last = blockColumns[0];
                foreach (List<Vector3> current in blockColumns)
                {
                    if (current.Count != last.Count || current[0].x > last[0].x + blockTilemap.cellSize.x || current[0].y != last[0].y)
                    {
                        CreateBlock(bottomLeft, width, height);
                        bottomLeft = current[0];
                        width = 0;
                        height = current.Count;
                    }
                    width++;
                    last = current;
                }
                CreateBlock(bottomLeft, width, last.Count);
            }
        }

        if (platformTilemap != null)
        {
            List<List<Vector3>> platformColumns = ReduceTiles(platformTilemap);
            if (platformColumns.Count > 0)
            {
                Vector3 bottomLeft = platformColumns[0][0];
                int width = 0;
                int height = platformColumns[0].Count;
                List<Vector3> last = platformColumns[0];
                foreach (List<Vector3> current in platformColumns)
                {
                    if (current.Count != last.Count || current[0].x > last[0].x + platformTilemap.cellSize.x || current[0].y != last[0].y)
                    {
                        CreatePlatform(bottomLeft, width, height);
                        bottomLeft = current[0];
                        width = 0;
                        height = current.Count;
                    }
                    width++;
                    last = current;
                }
                CreatePlatform(bottomLeft, width, last.Count);
            }
        }

        if (waterTilemap != null)
        {
            List<List<Vector3>> waterColumns = ReduceTiles(waterTilemap);
            if (waterColumns.Count > 0)
            {
                Vector3 bottomLeft = waterColumns[0][0];
                int width = 0;
                int height = waterColumns[0].Count;
                List<Vector3> last = waterColumns[0];
                foreach (List<Vector3> current in waterColumns)
                {
                    if (current.Count != last.Count || current[0].x > last[0].x + blockTilemap.cellSize.x || current[0].y != last[0].y)
                    {
                        CreateBlock(bottomLeft, width, height, waterParent).isTrigger = true;
                        bottomLeft = current[0];
                        width = 0;
                        height = current.Count;
                    }
                    width++;
                    last = current;
                }
                CreateBlock(bottomLeft, width, last.Count, waterParent).isTrigger = true;
            }
        } 
    }

    private List<List<Vector3>> ReduceTiles(Tilemap tilemap)
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

        return columns;
    }

    private BoxCollider CreateBlock(Vector3 bottomLeft, int width, int height, GameObject parent = null)
    {
        if (parent == null) parent = gameObject;

        float x = blockTilemap.cellSize.x;
        float y = blockTilemap.cellSize.y;
        float w = x * width;
        float h = y * height;

        BoxCollider coll = parent.AddComponent<BoxCollider>();
        coll.center = bottomLeft + new Vector3(w / 2f - x * 0.5f, h / 2f, 0f);
        coll.size = new Vector3(w, h, 1f);

        return coll;
    }

    private void CreatePlatform(Vector3 bottomLeft, int width, int height)
    {
        float x = blockTilemap.cellSize.x;
        float y = blockTilemap.cellSize.y;
        float w = x * width;
        float h = y * height;

        GameObject platform = Instantiate(platformPrefab, bottomLeft + new Vector3(w / 2f - x * 0.5f, h, 0f), Quaternion.Euler(90f, 0f, 0f), transform);
        platform.transform.localScale = new Vector3(w, 1f, 1f);
    }
}
