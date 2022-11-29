using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TileColliderManager : MonoBehaviour
{
    public Tilemap tilemap;
    TileBase[] tiles;

    private void Awake()
    {
        transform.position = Vector3.zero;
    }

    // Start is called before the first frame update
    void Start()
    {
        //List<Vector3> tilePositions = new List<Vector3>();
        foreach (Vector3Int position in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(position))
            {
                //tilePositions.Add(tilemap.CellToWorld(position));
                Vector3 worldPosition = tilemap.CellToWorld(position);
                gameObject.AddComponent<BoxCollider>().center = worldPosition + new Vector3(0.5f, 0.5f, 0);
            }
        }
    }
}
