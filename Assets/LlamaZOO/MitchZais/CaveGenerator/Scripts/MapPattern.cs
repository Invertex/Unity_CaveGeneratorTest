using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using System.Linq;
using System.Diagnostics;

namespace LlamaZOO.MitchZais.CaveGenerator
{
    [System.Serializable]
    public class MapPattern
    {
        public Cell[,] Cells { get; private set; }
        public MapParams MapParams { get; private set; }
        public Dictionary<CellType, List<MapRegion>> Regions { get; private set; }
        public List<MapRegion> Rooms
        { 
            get
            {
                List<MapRegion> rooms;
                if (!Regions.TryGetValue(CellType.Floor, out rooms)) { Rooms = rooms = new List<MapRegion>(); }
            
                return rooms;
            }
            set
            {
                if (!Regions.ContainsKey(CellType.Floor)) { Regions.Add(CellType.Floor, value); }
                else { Regions[CellType.Floor] = value; }
            }
        }

        public MapRegion SpawnRoom { get; private set; }

        private float subdivPositionMultiplier = 1;
        public int Width { get { return  Cells.GetLength(1); } }
        public int Height { get { return  Cells.GetLength(0); } }

        public void Generate()
        {
            UnityEngine.Random.InitState(MapParams.seed);
            Cells = new Cell[MapParams.height, MapParams.width]; //y,x order for proper contigious mapping to 2D space

            RandomFill(Cells, MapParams.fillDensity);
            Cells = RefineWalls(Cells, MapParams.refinementSteps);
            GenerateRegionLists();
            RemoveUndersizedRegions(Cells, MapParams);
            BuildClosestPointsData();
            ConnectRoomsToClosestRoom();
            SpawnRoom = Rooms[Random.Range(0, Rooms.Count)];
            EnsureAllRegionsReachable();
        }

        private void RandomFill(Cell[,] map, float density)
        {
            //Start from 1 and stop before edge so our map defaults with a filled wall border
            for(int y = 1; y < Height - 1; y++)
            {
                for (int x = 1; x < Width - 1; x++)
                {
                    map[y, x] = (UnityEngine.Random.value < density) ? new Cell(CellType.Floor) : new Cell(CellType.Wall);
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
                            int neighborFloorCells = map.GetSurroundingCellCount(new Vector2Int(x, y), 1, CellType.Floor);

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
                    Cell curCell = Cells[y, x];

                    if (curCell.regionNum == Cell.DefaultRegionNum)
                    {
                        if (curCell == CellType.Floor)
                        {
                            rooms.Add( new MapRegion(Cells, GetFloodFillRegion(Cells, y, x, foundFloorRegions), CellType.Floor, foundFloorRegions) );
                            foundFloorRegions++;
                        }
                        else if (curCell == CellType.Wall)
                        {
                            wallRegions.Add( new MapRegion(Cells, GetFloodFillRegion(Cells, y, x, foundWallRegions), CellType.Wall, foundWallRegions) );
                            foundWallRegions++;
                        }
                    }
                }
            }
        }

        private HashSet<Vector2Int> GetFloodFillRegion(Cell[,] map, int y, int x, int newCellVal)
        {
            Stack<Vector2Int> cellsToCheck = new Stack<Vector2Int>();
            HashSet<Vector2Int> alteredCells = new HashSet<Vector2Int>();

            int targetRegionNum = map[y, x].regionNum;
            CellType targetRegionType = map[y, x];

            cellsToCheck.Push(new Vector2Int(x, y));

            while (cellsToCheck.Count > 0)
            {
                var cellCoord = cellsToCheck.Pop();

                if (map.IsOutsideMap(cellCoord)) { continue; }
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
            Dictionary<MapRegion, HashSet<MapRegion>> ConstructRegionMerges(IEnumerable<MapRegion> regions, bool removeEnclosedRegions = false)
            {
                var merges = new Dictionary<MapRegion, HashSet<MapRegion>>();
                
                foreach (MapRegion undersizedRegion in regions)
                {
                    var touchingRegions = undersizedRegion.FindTouchingRegions(this, removeEnclosedRegions ? 20 : 3);

                    if (touchingRegions.Count > 0)
                    {
                        touchingRegions.Sort((a, b) => a.PerimeterCoords.Count.CompareTo(b.PerimeterCoords.Count));

                        MapRegion largestTouchingRegion = touchingRegions[touchingRegions.Count - 1];

                        if (!merges.TryGetValue(largestTouchingRegion, out HashSet<MapRegion> regionsToMerge))
                        {
                            merges.Add(largestTouchingRegion, regionsToMerge = new HashSet<MapRegion>());
                        }
                        regionsToMerge.Add(undersizedRegion);

                        if (removeEnclosedRegions)
                        {
                            for (int i = 0; i < touchingRegions.Count - 1; i++)
                            {
                                //If the touching region has a bigger area and is also a different type, can't possibly be enclosed region
                                if( touchingRegions[i].CellType != undersizedRegion.CellType 
                                    && touchingRegions[i].Area < undersizedRegion.Area)
                                {
                                    regionsToMerge.Add(touchingRegions[i]);
                                }
                            }
                        }
                    }
                }
                return merges;
            }

            void UpdateCellRegionNums(List<MapRegion> regions)
            {
                for (int i = 0; i < regions.Count; i++)
                {
                    regions[i].UpdateRegionNumber(map, i + 1);
                }
            }

            void ProcessMerges<T>(Dictionary<MapRegion, T> merges) where T : ICollection<MapRegion>
            {
                foreach (var regionMerge in merges)
                {
                    regionMerge.Key.AddRegions(map, regionMerge.Value, false);
                }
            }
        
            //Merge wall regions first as it may affect the area size of rooms 
            var undersizedWallRegions = GetRegionsUnderSize(CellType.Wall, SubdividedSize(mapParams.smallestWallArea, mapParams.TotalSubdivisions));
            var pendingWallMerges = ConstructRegionMerges(undersizedWallRegions.removedRegions);
            List<MapRegion> wallRegions = Regions[CellType.Wall];
            wallRegions.Clear();
            wallRegions.AddRange(undersizedWallRegions.remainingRegions);
            UpdateCellRegionNums(wallRegions);
            ProcessMerges(pendingWallMerges);
        
            //Merges room regions
            var undersizedFloorRegions = GetRegionsUnderSize(CellType.Floor, SubdividedSize(mapParams.smallestRoomArea, mapParams.TotalSubdivisions));
            var pendingFloorMerges = ConstructRegionMerges(undersizedFloorRegions.removedRegions, true); 
            List<MapRegion> floorRegions = Regions[CellType.Floor];
            floorRegions.Clear();
            floorRegions.AddRange(undersizedFloorRegions.remainingRegions);
            UpdateCellRegionNums(floorRegions);
            ProcessMerges(pendingFloorMerges);

            //We don't want to calculate new perimeters until all the existing cells have been merged otherwise perimeters can end up incorrect from old cell data
            wallRegions.ForEach((region) => region.UpdatePerimeterValues(map));
            floorRegions.ForEach((region) => region.UpdatePerimeterValues(map));
        }

        /// <summary>
        /// Doubles value for each division
        /// </summary>
        /// <param name="val"></param>
        /// <param name="divisions"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SubdividedSize(int val, int divisions)
        {
            for (int i = 0; i < divisions; i++)
            {
                val += val;
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

            subdivPositionMultiplier = (float)MapParams.width / scaledMap.GetLength(1);
            return scaledMap;
        }

        private void BuildClosestPointsData()
        {
            int roomCnt = Rooms.Count;

            for(int a = 0; a < roomCnt - 1; a++)
            {
                for (int b = a + 1; b < roomCnt; b++)
                {
                    var closestPoints = Rooms[a].FindClosestPointsBetweenRegions(Rooms[b]);
                    Rooms[a].AddClosestCoordToRegion(Rooms[b], closestPoints.region1Coord, closestPoints.dist);
                    Rooms[b].AddClosestCoordToRegion(Rooms[a], closestPoints.region2Coord, closestPoints.dist);
                }
            }
        }

        private void ConnectRoomsToClosestRoom()
        {
            foreach(var room in Rooms)
            {
                if(room.LinkedRegions.Count == 0)
                {
                    var closestRoom = room.GetClosestRegion();
                    var closestCoordOnOtherRoom = closestRoom.region.ClosestCoordToRegion[room].coord;
                    int pathRadius = Random.Range(1, 2);
                    CreatePathBetweenRegions(room, closestRoom.region, closestRoom.closestCoordTo, closestCoordOnOtherRoom, pathRadius);
                }
            }
        }

        private void EnsureAllRegionsReachable()
        {
            foreach(var room in Rooms)
            {
                MapRegion searchFromRegion = room;
                int loopCnt = 0;
                HashSet<MapRegion> ignoredRegions = new HashSet<MapRegion>();

                while (!searchFromRegion.IsReachableFromRegion(SpawnRoom) && loopCnt < 20)
                {
                    loopCnt++;

                    var closestLink = searchFromRegion.GetUnlinkedRegionWithShortestDistanceBetween(SpawnRoom, ignoredRegions);
                    searchFromRegion = closestLink.toRegion;

                    if (closestLink.fromRegion != SpawnRoom)
                    {
                        Connect(closestLink.fromRegion, closestLink.toRegion);
                    }
                   
                    void Connect(MapRegion fromRegion, MapRegion toRegion)
                    {
                        var fromCoord = fromRegion.ClosestCoordToRegion[toRegion].coord;
                        var toCoord = toRegion.ClosestCoordToRegion[fromRegion].coord;

                        CreatePathBetweenRegions(fromRegion, toRegion, fromCoord, toCoord, Random.Range(1, 2));
                    }
                }
            }
        }

        void CreatePathBetweenRegions(MapRegion region1, MapRegion region2, Vector2Int startCoord, Vector2Int endCoord, int radius)
        {
            radius *= MapParams.TotalSubdivisions;
            region1.LinkRegion(region2);

            List<Vector2Int> path = GetPathPoints(startCoord, endCoord);

            int pointCount = path.Count;
            int midPoint = Mathf.CeilToInt(pointCount / 2);

            foreach (var coord in path.GetRange(0, midPoint))
            {
                region1.AddCellsInRadius(this, coord, radius);
            }
            
            if(pointCount < 2){ return; } //Only one point to draw so can't split in two

            //Split the pathway cell additions halfway, adding to other region so our rooms region data meet halfway
            foreach (var coord in path.GetRange(midPoint, pointCount - midPoint))
            {
                region2.AddCellsInRadius(this, coord, radius);
            }
        }

        List<Vector2Int> GetPathPoints(Vector2Int from, Vector2Int to)
        {
            List<Vector2Int> points = new List<Vector2Int>();

            Vector2Int point = from;
            Vector2Int delta = to - from;

            int longest = Mathf.Abs(delta.x);
            int shortest = Mathf.Abs(delta.y);

            delta.x = System.Math.Sign(delta.x);
            delta.y = System.Math.Sign(delta.y);

            Vector2Int step = new Vector2Int(delta.x, 0);
            Vector2Int gradStep = new Vector2Int(0, delta.y);

            if (longest < shortest)
            {
                int swap = longest;
                longest = shortest;
                shortest = swap;

                Vector2Int swapDelta = step;
                step = gradStep;
                gradStep = swapDelta;
            }

            int gradAccumulation = longest / 2;

            for (int i = 0; i < longest; i++)
            {
                points.Add(point);

                point += step;
                gradAccumulation += shortest;

                if (gradAccumulation >= longest)
                {
                    point += gradStep;
                    gradAccumulation -= longest;
                }
            }

            return points;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /// <summary>
        /// Converts the Vector2Int Cell[,] coordinate to an object space Vector3 coordinate, taking map subdivisions into account.
        /// </summary>
        /// <param name="coord">Cell[,] coordinate</param>
        /// <returns></returns>
        internal Vector3 CoordToPos(Vector2Int coord) => new Vector3(coord.x * subdivPositionMultiplier, 0, coord.y * subdivPositionMultiplier);

        public MapPattern(MapParams saveData) { this.MapParams = saveData; }
        public MapPattern(int width, int height, int seed = -1, float density = 0.5f, int refinementSteps = 3, int subdivisions = 2)
        {
            if (seed == -1) { seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue); }
            MapParams = new MapParams(seed, width, height, density, refinementSteps, subdivisions);
        }
    }
}