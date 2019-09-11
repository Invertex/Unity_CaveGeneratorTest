using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace LlamaZOO.MitchZais.CaveGenerator
{
    [System.Serializable]
    public class MapPattern
    {
        public Cell[,] Map { get; private set; }
        public MapParams MapParams { get; private set; }
        public Dictionary<CellType, List<MapRegion>> Regions { get; private set; }

        public int Width { get { return (Map != null) ? Map.GetLength(1) : 0; } }
        public int Height { get { return (Map != null) ? Map.GetLength(0) : 0; } }

        public void Generate()
        {
            Random.InitState(MapParams.seed);
            Map = new Cell[MapParams.height, MapParams.width]; //y,x order for proper contigious mapping to 2D space

            RandomFill(Map, MapParams.fillDensity);
            Map = RefineWalls(Map, MapParams.refinementSteps);
            GenerateRegionLists();
            RemoveUndersizedRegions(Map, MapParams);
        }

        private void RandomFill(Cell[,] map, float density)
        {
            //Start from 1 and stop before edge so our map defaults with a filled wall border
            for(int y = 1; y < Height - 1; y++)
            {
                for (int x = 1; x < Width - 1; x++)
                {
                    map[y, x] = (Random.value < density) ? new Cell(CellType.Floor) : new Cell(CellType.Wall);
                }
            }
        }

        private Cell[,] RefineWalls(Cell[,] map, MapParams.RefinementStep[] steps)
        {
            foreach (var step in steps)
            {
                if (step.subdivideFirst) { map = SubdivideMap(map, 1); }
                for (int i = 0; i < step.iterations; i++)
                {
                    int mapHeight = map.GetLength(0);
                    int mapWidth = map.GetLength(1);
                    
                    Cell[,] newMap = new Cell[mapHeight, mapWidth];

                    for (int y = 1; y < mapHeight - 1; y++)
                    {
                        for (int x = 1; x < mapWidth - 1; x++)
                        {
                            Cell targetCell = map[y, x];
                            int neighborFloorCells = GetSurroundingCellCount(map, new Vector2Int(x, y), 1, CellType.Floor);

                            if (targetCell != CellType.Wall && neighborFloorCells < step.roomDeathWeight)
                            {
                                targetCell.cellType = CellType.Wall;
                            }
                            else if (neighborFloorCells > step.roomLifeWeight)
                            {
                                targetCell.cellType = CellType.Floor;
                            }

                            newMap[y, x] = targetCell;
                        }
                    }
                    map = newMap;
                }
            }

            return map;
        }

        private void GenerateRegionLists()
        {
            int foundFloorRegions = 1;
            int foundWallRegions = 1;

            List<MapRegion> rooms = new List<MapRegion>(64);
            List<MapRegion> wallRegions = new List<MapRegion>(64);
            Regions = new Dictionary<CellType, List<MapRegion>>();
            Regions.Add(CellType.Floor, rooms);
            Regions.Add(CellType.Wall, wallRegions);

            for (int y = 1; y < Height - 1; y++)
            {
                for(int x = 1; x < Width - 1; x++)
                {
                    Cell curCell = Map[y, x];

                    if (curCell.regionNum == Cell.DefaultRegionNum)
                    {
                        if (curCell == CellType.Floor)
                        {
                            rooms.Add( new MapRegion(Map, GetFloodFillRegion(Map, y, x, foundFloorRegions), CellType.Floor, foundFloorRegions) );
                            foundFloorRegions++;
                        }
                        else if (curCell == CellType.Wall)
                        {
                            wallRegions.Add( new MapRegion(Map, GetFloodFillRegion(Map, y, x, foundWallRegions), CellType.Wall, foundWallRegions) );
                            foundWallRegions++;
                        }
                    }
                }
            }
        }

        private (List<MapRegion> remainingRegions, List<MapRegion> removedRegions) GetRegionsUnderSize(CellType regionType, int area)
        {
            List<MapRegion> removeRegions = new List<MapRegion>();
            List<MapRegion> keptRegions = new List<MapRegion>();

            if (Regions.TryGetValue(regionType, out List<MapRegion> regionList))
            {
                if (regionList.Count == 0) { return (keptRegions, removeRegions); }

                List<MapRegion> areaSortedRegions = new List<MapRegion>(regionList);
                areaSortedRegions.Sort((a, b) => a.Area.CompareTo(b.Area));

                //Count - 1 so that we always at least leave the biggest region
                for (int i = 0; i < areaSortedRegions.Count - 1; i++)
                {
                    MapRegion region = areaSortedRegions[i];

                    if (region.Area < area) { removeRegions.Add(region); }
                    else { keptRegions.Add(region); }
                }
                keptRegions.Add(areaSortedRegions[areaSortedRegions.Count - 1]);
            }
          
            return (keptRegions, removeRegions);
        }

        private void RemoveUndersizedRegions(Cell[,] map, MapParams mapParams)
        {
            var undersizedRoomRegions = GetRegionsUnderSize(CellType.Floor, SubdividedSize(mapParams.smallestRoomArea, mapParams.TotalSubdivisions));
            var undersizedWallRegions = GetRegionsUnderSize(CellType.Wall, SubdividedSize(mapParams.smallestWallArea, mapParams.TotalSubdivisions));

            var pendingRegionMerges = new Dictionary<MapRegion, List<MapRegion>>();

            ConstructRegionMerges(undersizedWallRegions.removedRegions);
            ConstructRegionMerges(undersizedRoomRegions.removedRegions);

                void ConstructRegionMerges(IEnumerable<MapRegion> regions)
                {
                    foreach (MapRegion region in regions)
                    {
                        var touchingRegion = region.FindTouchingRegions(this, 1);

                        if (touchingRegion.Count > 0)
                        {
                            if (pendingRegionMerges.TryGetValue(touchingRegion[0], out List<MapRegion> touchingRegionPendingMerges))
                            {
                                touchingRegionPendingMerges.Add(region);
                            }
                            else { pendingRegionMerges.Add(touchingRegion[0], new List<MapRegion>() { region }); }
                        }
                    }
                }
            
            List<MapRegion> wallRegions = Regions[CellType.Wall];
            List<MapRegion> floorRegions = Regions[CellType.Floor];
            wallRegions.Clear();
            wallRegions.AddRange(undersizedWallRegions.remainingRegions);
            floorRegions.Clear();
            floorRegions.AddRange(undersizedRoomRegions.remainingRegions);

            UpdateCellRegionNums(wallRegions);
            UpdateCellRegionNums(floorRegions);

                void UpdateCellRegionNums(List<MapRegion> regions)
                {
                    for(int i = 0; i < regions.Count; i++)
                    {
                        regions[i].UpdateRegionNumber(map, i + 1);
                    }
                }

            foreach (var regionMerge in pendingRegionMerges)
            {
                regionMerge.Key.AddRegions(map, regionMerge.Value, false);
            }
            //We don't want to calculate new perimeters until all the existing cells have been merged otherwise perimeters can end up incorrect from old cell data
            floorRegions.ForEach((region) => region.UpdatePerimeterValues(map));
            wallRegions.ForEach((region) => region.UpdatePerimeterValues(map));
        }

        private List<Vector2Int> GetFloodFillRegion(Cell[,] map, int y, int x, int newCellVal)
        {
            Stack<Vector2Int> cellsToCheck = new Stack<Vector2Int>();
            List<Vector2Int> alteredCells = new List<Vector2Int>(Width * Height);

            int targetRegionNum = map[y,x].regionNum;
            CellType targetRegionType = map[y,x];

            cellsToCheck.Push(new Vector2Int(x,y));

            while(cellsToCheck.Count > 0)
            {
                var cellCoord = cellsToCheck.Pop();

                if(IsOutsideMap(map, cellCoord)) { continue; }
                Cell sampledCell = map[cellCoord.y, cellCoord.x];

                if (sampledCell == targetRegionType && sampledCell.regionNum == targetRegionNum)
                {
                    map[cellCoord.y, cellCoord.x].regionNum = newCellVal;
                    alteredCells.Add(cellCoord);

                    cellsToCheck.Push(new Vector2Int(cellCoord.x - 1, cellCoord.y - 1));
                    cellsToCheck.Push(new Vector2Int(cellCoord.x, cellCoord.y - 1));
                    cellsToCheck.Push(new Vector2Int(cellCoord.x + 1, cellCoord.y - 1));

                    cellsToCheck.Push(new Vector2Int(cellCoord.x - 1, cellCoord.y));
                    cellsToCheck.Push(new Vector2Int(cellCoord.x + 1, cellCoord.y));


                    cellsToCheck.Push(new Vector2Int(cellCoord.x - 1, cellCoord.y + 1));
                    cellsToCheck.Push(new Vector2Int(cellCoord.x, cellCoord.y + 1));
                    cellsToCheck.Push(new Vector2Int(cellCoord.x + 1, cellCoord.y + 1));
                }
            }

            return alteredCells;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetSurroundingCellCount(Cell[,] map, Vector2Int coord, int radius, CellType matchCellType, bool outOfBoundsMatch = false) 
        {
            int found = 0;
            
            for(int y = coord.y - radius; y <= coord.y + radius; y++)
            {
                for (int x = coord.x - radius; x <= coord.x + radius; x++)
                {
                    if (x == coord.x && y == coord.y) { continue; }

                    if(IsOutsideMap(map, new Vector2Int(x,y)))
                    {
                        if (outOfBoundsMatch) { found++; }
                        continue;
                    }

                    if(map[y,x] == matchCellType) { found++; }
                }
            }
            return found;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsOutsideMap(Cell[,] map, Vector2Int coord)
        {
            return (coord.y < 0 || coord.x < 0 || coord.y >= map.GetLength(0) || coord.x >= map.GetLength(1));
        }

        /// <summary>
        /// Multiplies value by 2 for each division
        /// </summary>
        /// <param name="val"></param>
        /// <param name="divisions"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SubdividedSize(int val, int divisions)
        {
            for (int i = 0; i < divisions; i++)
            {
                val *= 2;
            }
            return val;
        }

        private Cell[,] SubdivideMap(Cell[,] map, int subdivisions)
        {
            int height = map.GetLength(0); int width = map.GetLength(1);
            int scaledWidth = SubdividedSize(width, subdivisions);
            int scaledHeight = SubdividedSize(height, subdivisions);
            int scaledIdxMult = SubdividedSize(1, subdivisions);

            Cell[,] scaledMap = new Cell[scaledHeight, scaledWidth];

            for(int y = 0; y < height; y++)
            {
                for(int x = 0; x < width; x++)
                {
                    Cell cell = map[y, x];

                    int scaledYOffset = y * scaledIdxMult;

                    for (int scaledY = scaledYOffset; scaledY < scaledYOffset + scaledIdxMult; scaledY ++)
                    {
                        int scaledXOffset = x * scaledIdxMult;

                        for (int scaledX = scaledXOffset; scaledX < scaledXOffset + scaledIdxMult; scaledX++)
                        {
                            scaledMap[scaledY, scaledX] = cell;
                        }
                    }
                }
            }

            return scaledMap;
        }

        private void MergeScrapWallRegions()
        {
            if (Regions.TryGetValue(CellType.Wall, out var wallRegions))
            {
                int keptWallRegionCnt = 0;
                List<MapRegion> keptWallRegions = new List<MapRegion>(wallRegions.Count);

                for (int i = 0; i < wallRegions.Count; i++)
                {
                    MapRegion wallRegion = wallRegions[i];
                    MapRegion closestRegion = wallRegions[i].FindTouchingRegions(this,1)[0];

                    if (closestRegion == null || closestRegion.CellType != wallRegion.CellType)
                    {
                        keptWallRegionCnt += 1;
                        wallRegion.UpdateRegionNumber(Map, keptWallRegionCnt);
                        keptWallRegions.Add(wallRegion);
                    }
                    else { closestRegion.AddRegion(Map, wallRegion); }
                }
                Regions[CellType.Wall] = keptWallRegions;
            }
        }

        public void ApplyMapToTexture2D(ref Texture2D tex, bool colorCodedRooms = false)
        {
            int yLen = Height;
            int xLen = Width;
            float roomCount = (Regions != null) ? Regions[CellType.Floor].Count : 0;

            if (tex == null) { tex = new Texture2D(xLen, yLen); }
            else { tex.Resize(xLen, yLen); }

            var textureBuffer = tex.GetRawTextureData<Color32>();
            int pixelIdx = 0;

            for (int y = 0; y < yLen; y++)
            {
                for (int x = 0; x < xLen; x++)
                {
                    Cell cell = Map[y, x];
                    Color pixelColor = (cell == CellType.Wall) ? Color.black : Color.white;

                    if (colorCodedRooms)
                    {
                        if (cell.edgeCell && cell == CellType.Wall)
                        {
                            pixelColor = new Color(0.2f, 0.2f, 0.2f);
                        }
                        else if (cell != CellType.Wall)
                        {
                            pixelColor = Color.HSVToRGB((float)cell.regionNum / roomCount, 1, cell.edgeCell ? 1f : 1, false);
                        }
                    }
                    textureBuffer[pixelIdx++] = pixelColor;
                }
            }

            tex.Apply();
        }

        public MapPattern(MapParams saveData) { this.MapParams = saveData; }
        public MapPattern(int width, int height, int seed = -1, float density = 0.5f, int refinementSteps = 3, int subdivisions = 2)
        {
            if (seed == -1) { seed = Random.Range(int.MinValue, int.MaxValue); }
            MapParams = new MapParams(seed, width, height, density, refinementSteps, subdivisions);
        }
    }
}