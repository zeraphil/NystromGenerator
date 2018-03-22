using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;

public class NystromGenerator : MonoBehaviour {

    public static ReadOnlyCollection<Vector2Int> CardinalDirections =
    new List<Vector2Int> { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left }.AsReadOnly();

    enum TileType { None, Wall, RoomFloor, CorridorFloor, OpenDoor, ClosedDoor };

    public Grid grid;
    public Tilemap tilemap;
    public Tile WallTile;
    public Tile FloorTile;
    public Tile DoorTile;
    public Tile CorridorTile;

    public int dimensions = 51;

    public int numRoomTries = 0;
    public int extraConnectorChance = 10;

    public int extraRoomSize = 0;
    public int windingPercent = 50;

    public bool removeDeadEnds = false;

    public int seed = 0;

    private List<Rect> mRooms = new List<Rect>();

    private ReadOnlyCollection<int> Directions;


    Rect mBounds;
    LevelGrid<int> mRegions;
    LevelGrid<TileType> mLevelGrid; // store the tiles we'll use for later placement
    int mCurrentRegion = -1;


	// Use this for initialization
	void Start () {

        if (seed != 0)
            Random.InitState(seed);

        Generate(dimensions, dimensions);
    }

	void Generate(int width, int height)
    {

        if (width % 2 == 0 || height % 2 == 0)
        {
            throw new System.Exception("The stage must be odd-sized.");
        }

        mBounds = new Rect(0, 0, width, height);
        mLevelGrid = new LevelGrid<TileType>(width, height);
        mLevelGrid.Fill(TileType.Wall);

        mRegions = new LevelGrid<int>(width, height);
        mRegions.Fill(-1);

        AddRooms();

        // Fill in all of the empty space with mazes.
        for (int y = 1; y < mBounds.height; y += 2)
        {
            for (int x = 1; x < mBounds.width; x += 2)
            {
                var pos = new Vector2Int(x, y);
                if (mLevelGrid[pos] != TileType.Wall) continue;

                GrowMaze(pos);
            }
        }

        ConnectRegions();

        if ( removeDeadEnds)
            RemoveDeadEnds();

        StartCoroutine(SetAllTiles());
    }

    /*
    IEnumerator SeeRegions()
    {

        for (int x = 0; x < mRegions.Width; x++)
            for (int y = 0; y < mRegions.Height; y++)
            {
                {
                    var position = new Vector3Int(x, y, 0);

                    switch (mRegions[x, y])
                    {
                        case 0:
                            tilemap.SetColor(position, Color.black);
                            tilemap.SetTile(position, WallTile);
                            break;
                        case 1:
                            tilemap.SetTile(position, FloorTile);
                            break;
                        case 2:
                            tilemap.SetTile(position, CorridorTile);
                            break;
                        case 3:
                        case 4:
                            tilemap.SetTile(position, DoorTile);
                            break;
                        default:
                            //tilemap.SetTile(position, FloorTile);
                            break;
                    }

                }
            }
        yield return null;
    }
    */

    IEnumerator SetAllTiles()
    {

        for (int x = 0; x < mLevelGrid.Width; x++)
            for (int y = 0; y < mLevelGrid.Height; y++)
            {
                {
                    var position = new Vector3Int(x, y, 0);

                    switch(mLevelGrid[x,y])
                    {
                        case TileType.Wall:
                            tilemap.SetTile(position, WallTile);
                            break;
                        case TileType.RoomFloor:
                            tilemap.SetTile(position, FloorTile);
                            break;
                        case TileType.CorridorFloor:
                            tilemap.SetTile(position, FloorTile);
                            break;
                        case TileType.OpenDoor:
                        case TileType.ClosedDoor:
                            tilemap.SetTile(position, CorridorTile);
                            break;
                        default:
                            //tilemap.SetTile(position, FloorTile);
                            break;
                    }

                }
            }
        yield return null;
    }

    //Growing tree maze
    void GrowMaze(Vector2Int start)
    {
        var cells = new LinkedList<Vector2Int>();

        Vector2Int lastDir = Vector2Int.zero; //won't be a cardinal direction


        StartRegion();
        Carve(start, TileType.CorridorFloor);

        cells.AddFirst(start);

        while ( cells.Count != 0)
        {
            var cell = cells.Last.Value;

            //see which adjacent cells are open

            var unmadeCells =  new List<Vector2Int>();

            foreach ( var dir in CardinalDirections)
            {
                if (CanCarve(cell, dir)) unmadeCells.Add(dir);
            }

            if (unmadeCells.Count != 0)
            {
                Vector2Int dir;

                if (lastDir != Vector2Int.zero && unmadeCells.Contains(lastDir) && Random.Range(0, 100) > windingPercent)
                {
                    dir = lastDir;
                }
                else
                {
                    int idx = Random.Range(0, unmadeCells.Count);
                    dir = unmadeCells[idx];
                }


                Carve(cell + dir, TileType.CorridorFloor);
                Carve(cell + dir * 2, TileType.CorridorFloor);

                cells.AddLast(cell + dir * 2);
                lastDir = dir;
            }
            else
            {
                //no adjacent uncarved cells
                cells.RemoveLast();
                lastDir = Vector2Int.zero;
            }
        }

    }

    void AddRooms()
    {
        for (int i = 0; i < numRoomTries; i++)
        {
            int size = Random.Range(1, 3 + extraRoomSize) * 2 + 1;
            int rectangularity = Random.Range(0, 1 + size / 2) * 2;

            int width = size;
            int height = size;

            if (Random.Range(1, 2) == 1)
            {
                width += rectangularity;
            }
            else
            {
                height += rectangularity;
            }

            int x = Random.Range(0, (mLevelGrid.Width - width) / 2) * 2 + 1;
            int y = Random.Range(0, (mLevelGrid.Height - height) / 2) * 2 + 1;

            Rect room = new Rect(x, y, width, height);

            bool overlaps = false;

            foreach (var other in mRooms)
            {
                if (room.Overlaps(other))
                {
                    overlaps = true;
                    break;
                }
            }

            if (overlaps) continue;

            mRooms.Add(room);

            StartRegion();

            for (int m = x; m < x + width; m++)
            {
                for ( int n = y; n < y + height; n++)
                {
                    Carve(new Vector2Int(m,n));
                }

            }
        }
    }

    void ConnectRegions()
    {
        //find all the tiles that can connect two or more region

        Dictionary<Vector2Int, HashSet<int>> connectorRegions = new Dictionary<Vector2Int, HashSet<int>>();

        for (int y = 1; y < mBounds.height - 1; y++)
        {
                for (int x = 1; x < mBounds.width - 1; x++)
                {
                var pos = new Vector2Int(x, y);

                if (mLevelGrid[pos] != TileType.Wall) continue;

                var regions = new HashSet<int>();

                foreach( var dir in  CardinalDirections)
                {
                    var region = mRegions[pos + dir];
                    if ( region != -1) regions.Add(region);

                }

                if (regions.Count < 2) continue;

                connectorRegions[pos] = regions;

            }
        }

        var connectors = new HashSet<Vector2Int>(connectorRegions.Keys);
        // Keep track of which regions have been merged. This maps an original
        // region index to the one it has been merged to.
        var merged = new SortedDictionary<int, int>();
        var openRegions = new List<int>();
        for (int i = 0; i <= mCurrentRegion; i++)
        {
            merged.Add(i, i);
            openRegions.Add(i);
        }


        // Keep connecting regions until we're down to one.
        while (openRegions.Count > 0)
        {

            var idx = Random.Range(0, connectors.Count);
            Vector2Int connector = connectors.ElementAt(idx);

            // Carve the connection.
            AddJunction(connector);

            // Merge the connected regions. We'll pick one region (arbitrarily) and
            // map all of the other regions to its index.
            var regions = connectorRegions[connector].Select(region => merged[region]);

            var dest = regions.First();
            var sources = regions.Skip(1).ToList();

            // Merge all of the affected regions. We have to look at *all* of the
            // regions because other regions may have previously been merged with
            // some of the ones we're merging now.
            for (var i = 0; i <= mCurrentRegion; i++)
            {
                if (sources.Contains(merged[i]))
                {
                    merged[i] = dest;
                }
            }

            // The sources are no longer in use.
            openRegions.RemoveAll(sources.Contains);

            // Remove any connectors that aren't needed anymore.
            connectors.RemoveWhere(pos => {
                // Don't allow connectors right next to each other.
                if ((connector - pos).magnitude < 2) return true;

                // If the connector no long spans different regions, we don't need it.
                var local_regions = connectorRegions[pos].Select(region => merged[region]).ToList();

                if (local_regions.Count > 1) return false;

                // This connecter isn't needed, but connect it occasionally so that the
                // dungeon isn't singly-connected.

                if (OneIn(extraConnectorChance)) AddJunction(pos);

                return true;
            });
        }
    }

    void AddJunction(Vector2Int pos)
    {
        if (OneIn(4))
        {
            mLevelGrid[pos] = OneIn(3) ? TileType.OpenDoor : TileType.RoomFloor;
        }
        else
            mLevelGrid[pos] = TileType.ClosedDoor;
    }

    void RemoveDeadEnds()
    {
        bool done = false;

        while (!done)
        {
            done = true;

            for (int x = 1; x < mBounds.width-1; x++)
            {
                for (int y = 1; y < mBounds.height-1; y++)
                {
                    var pos = new Vector2Int(x, y);
                    if (mLevelGrid[pos] == TileType.Wall) continue;

                    int exits = 0;
                    foreach ( var dir in  CardinalDirections)
                    {
                        if (mLevelGrid[pos + dir] != TileType.Wall) exits++;
                    }

                    if (exits != 1) continue;

                    done = false;

                    mLevelGrid[pos] = TileType.Wall;
                }
            }
        }

    }

    /// Gets whether or not an opening can be carved from the given starting
    /// [Cell] at [pos] to the adjacent Cell facing [direction]. Returns `true`
    /// if the starting Cell is in bounds and the destination Cell is filled
    /// (or out of bounds).</returns>
    bool CanCarve(Vector2Int pos, Vector2Int direction )
    {
        if (!mBounds.Contains(pos + direction * 3)) return false;

        return mLevelGrid[pos + direction * 2] == TileType.Wall;
    }

    void StartRegion()
    {
        mCurrentRegion++;
    }

    void Carve(Vector2Int pos)
    {
        mLevelGrid[pos] = TileType.RoomFloor;
        mRegions[pos] = mCurrentRegion;
    }

    void Carve(Vector2Int pos, TileType type = TileType.None)
    {
        if (type == TileType.None) type = TileType.RoomFloor;

        mLevelGrid[pos] = type;
        mRegions[pos] = mCurrentRegion;

    }

    public bool OneIn(int value)
    {
        return (Random.Range(0, value) == 1);
    }


}
