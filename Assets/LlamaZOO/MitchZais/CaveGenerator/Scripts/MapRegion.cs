using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LlamaZOO.MitchZais.CaveGenerator
{
[System.Serializable]
    public class MapRegion
    {
        public int RegionNum { get; private set; }
        public int Area { get { return CellCoords.Count; } }
        public CellType CellType { get; private set; }
        public HashSet<Vector2Int> CellCoords { get; private set; }
        public HashSet<Vector2Int> PerimeterCoords { get; private set; }
        public HashSet<MapRegion> LinkedRegions { get; private set; }

        internal Dictionary<MapRegion, (Vector2Int coord, float dist)> ClosestCoordToRegion { get; private set; }

        internal void AddRegion(Cell[,] cells, MapRegion region, bool updatePerimeter = true)
        {
            if (region == null) { return; }
            region.UpdateRegionCells(cells, CellType, RegionNum, false);
            CellCoords.UnionWith(region.CellCoords);
           // CellCoords.AddRange(region.CellCoords);
            region.CellCoords.Clear();
            region.UpdatePerimeterValues(cells);
            if (updatePerimeter) { UpdatePerimeterValues(cells); }
        }

        internal void AddRegions(Cell[,] cells, ICollection<MapRegion> regions, bool updatePerimeter = true)
        {
            if(regions == null || regions.Count == 0){ return; }

            foreach(var region in regions) { AddRegion(cells, region, false); }
            if (updatePerimeter) { UpdatePerimeterValues(cells); }
        }

        /// <summary>
        /// Adds cells and ensures they're removed from other Region's collections.
        /// Should only be used during path creation as affect on performance would be noticeable for large amounts of pixels.
        /// </summary>
        internal void AddCellsInRadius(MapPattern map, Vector2Int coord, int radius)
        {
            int radSqr = radius * radius;
            HashSet<Vector2Int> addedCoords = new HashSet<Vector2Int>();

            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x*x + y*y <= radSqr)
                    {
                        Vector2Int drawCoord = new Vector2Int(coord.x + x, coord.y + y);

                        if (!map.IsOutsideMap(drawCoord))
                        {
                            addedCoords.Add(drawCoord);
                            Cell existingCell = map.Cells[drawCoord.y, drawCoord.x];
                            MapRegion existingRegion = map.GetRegion(existingCell, existingCell.regionNum);
                            map.Cells[drawCoord.y, drawCoord.x] = new Cell(CellType, RegionNum, false);
                            existingRegion.RemoveCellFromRegion(drawCoord);
                        }
                    }
                }
            }

            PerimeterCoords.ExceptWith(addedCoords);
            CellCoords.UnionWith(addedCoords);
            PerimeterCoords.UnionWith(CalculatePerimeterCoordsForCellGroup(map.Cells, addedCoords, true));
        }

        internal void AddClosestCoordToRegion(MapRegion region, Vector2Int coord, float dist)
        {
            if(!ClosestCoordToRegion.ContainsKey(region))
            {
                ClosestCoordToRegion.Add(region, (coord, dist));
            } else { ClosestCoordToRegion[region] = (coord, dist); }
        }

        internal (MapRegion region, Vector2Int closestCoordTo, float dist) GetClosestRegion()
        {
            float bestDist = int.MaxValue;
            MapRegion closestRegion = null;
            Vector2Int closestCoord = Vector2Int.zero;

            foreach(var region in ClosestCoordToRegion)
            {
                if(region.Value.dist < bestDist)
                {
                    bestDist = region.Value.dist;
                    closestRegion = region.Key;
                    closestCoord = region.Value.coord;
                }
            }

            return (closestRegion, closestCoord, bestDist);
        }

        internal List<MapRegion> FindTouchingRegions(MapPattern map, int maxReturnedRegions = 4)
        {
            List<MapRegion> touchingRegions = new List<MapRegion>(maxReturnedRegions);
            HashSet<(CellType cellType, int regionNum)> alreadyFound = new HashSet<(CellType cellType, int regionNum)>();
            int foundRegionCnt = 0;

            foreach(var coord in PerimeterCoords)
            {
                Cell thisCell = map.Cells[coord.y, coord.x];

                for (int x = coord.x - 1; x < coord.x + 1; x++)
                {
                    for (int y = coord.y - 1; y < coord.y + 1; y++)
                    {
                        if (x != coord.x && y != coord.y && !map.Cells.IsOutsideMap(new Vector2Int(x, y)))
                        {
                            Cell sampledCell = map.Cells[y, x];

                            if(alreadyFound.Contains((sampledCell.cellType, sampledCell.regionNum))){ continue; }
                            int regionIndex = sampledCell.regionNum - 1;

                            if (sampledCell.cellType != thisCell.cellType || sampledCell.regionNum != thisCell.regionNum)
                            {
                                if (regionIndex >= 0 && map.Regions.TryGetValue(sampledCell.cellType, out var sampledRegionsList) && regionIndex < sampledRegionsList.Count)
                                {
                                    touchingRegions.Add(sampledRegionsList[regionIndex]);
                                    foundRegionCnt += 1;
                                    alreadyFound.Add((sampledCell.cellType, sampledCell.regionNum));

                                    if(foundRegionCnt >= maxReturnedRegions) { return touchingRegions; }
                                }
                            }
                        }
                    }
                }
            }
            return touchingRegions;
        }

        internal bool IsReachableFromRegion(MapRegion targetRegion)
        {
            if(this == targetRegion) { return true; }

            var checkedRegions = new HashSet<MapRegion>();
            checkedRegions.Add(this);

            foreach (var linkedRegion in LinkedRegions)
            {
                if (IsReachableRecursion(linkedRegion)) { return true; }
            }

            bool IsReachableRecursion(MapRegion checkRegion)
            {
                if (checkRegion == targetRegion) { return true; }

                checkedRegions.Add(checkRegion);

                foreach (var linkedRegion in checkRegion.LinkedRegions)
                {
                    if (checkedRegions.Contains(linkedRegion)) { continue; }
                    if (IsReachableRecursion(linkedRegion)) { return true; }
                }

                return false;
            }

            return false;
        }

        internal (MapRegion fromRegion, MapRegion toRegion) GetUnlinkedRegionWithShortestDistanceBetween(MapRegion defaultClosestRegion, HashSet<MapRegion> ignoredRegions)
        {
            (MapRegion fromRegion, MapRegion toRegion) bestMatch = (this, GetClosestUnlinkedRegion());
            float bestDist = ClosestCoordToRegion[bestMatch.toRegion].dist;

            ignoredRegions.Add(this);
            ignoredRegions.Add(defaultClosestRegion);

            foreach (var linkedRegion in LinkedRegions)
            {
                var result = GetShortestUnlinkedRecursively(linkedRegion, defaultClosestRegion, ignoredRegions);
                if(result.dist < bestDist)
                {
                    bestDist = result.dist; 
                    bestMatch.fromRegion = result.fromRegion;
                    bestMatch.toRegion = result.toRegion;
                    if(bestDist == 0){ return (defaultClosestRegion, defaultClosestRegion); } //if 0 we were able to traverse to the target so just return
                }
            }
            return bestMatch;
        }

        private (float dist, MapRegion fromRegion, MapRegion toRegion) GetShortestUnlinkedRecursively(MapRegion nextRegion, MapRegion targetRegion, HashSet<MapRegion> ignoreRegions)
        {
            if (nextRegion == targetRegion) { return (0, targetRegion, targetRegion); }

            var closestRegion = nextRegion.GetClosestUnlinkedRegion();
            (float dist, MapRegion closestRegion, MapRegion toRegion) bestLink = (nextRegion.ClosestCoordToRegion[closestRegion].dist, nextRegion, closestRegion);
            
            HashSet<MapRegion> checkRegions = new HashSet<MapRegion>(nextRegion.LinkedRegions);
            ignoreRegions.Add(nextRegion);
            checkRegions.ExceptWith(ignoreRegions);

            foreach (var checkRegion in checkRegions)
            {
                var checkResult = GetShortestUnlinkedRecursively(checkRegion, targetRegion, ignoreRegions);
                if(checkResult.dist < bestLink.dist)
                {
                    bestLink = checkResult;
                }
            }

            return bestLink;
        }
        
        internal MapRegion GetClosestUnlinkedRegion()
        {
            float bestDist = float.MaxValue;
            MapRegion closestRegion = null;

            foreach(var kvp in ClosestCoordToRegion)
            {
                if(LinkedRegions.Contains(kvp.Key)){ continue; }
                if(kvp.Value.dist < bestDist)
                {
                    bestDist = kvp.Value.dist;
                    closestRegion = kvp.Key;
                }
            }

            return closestRegion;
        }

        internal void LinkRegion(MapRegion region2)
        {
            LinkedRegions.Add(region2);
            region2.LinkedRegions.Add(this);
        }

        internal (Vector2Int region1Coord, Vector2Int region2Coord, float dist) FindClosestPointsBetweenRegions(MapRegion region2)
        {
            float bestDist = float.MaxValue;
            Vector2Int region1BestCoord, region2BestCoord;
            region1BestCoord = region2BestCoord = Vector2Int.zero;

            var region1Perim = PerimeterCoords;
            var region2Perim = region2.PerimeterCoords;

            foreach (var region1Coord in region1Perim)
            {
                foreach (var region2Coord in region2Perim)
                {
                    float dist = CoordDist(region1Coord, region2Coord);

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        region1BestCoord = region1Coord;
                        region2BestCoord = region2Coord;
                    }
                }
            }

            return (region1BestCoord, region2BestCoord, bestDist);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal float CoordDist(Vector2Int coord1, Vector2Int coord2)
        {   
            return Vector2Int.Distance(coord1, coord2);
        }

        internal void RemoveCellFromRegion(Vector2Int coord)
        {
            CellCoords.Remove(coord);
            PerimeterCoords.Remove(coord);
        }

        internal void UpdateRegionNumber(Cell[,] map, int regionNum)
        {
            RegionNum = regionNum;

            foreach (Vector2Int coord in CellCoords)
            {
                map[coord.y, coord.x].regionNum = regionNum;
            }
        }

        internal void UpdateRegionCells(Cell[,] map, CellType cellType, int regionNum, bool edgeCell = false)
        {
            RegionNum = regionNum;
            CellType = cellType;

            foreach (Vector2Int coord in CellCoords)
            {
                map[coord.y, coord.x] = new Cell(cellType: CellType, regionNum: regionNum, edgeCell: edgeCell);
            }
        }

        HashSet<Vector2Int> CalculatePerimeterCoordsForCellGroup(Cell[,] map, IEnumerable<Vector2Int> addedCells, bool ignoreSameType)
        {
            HashSet<Vector2Int> perimCoords = new HashSet<Vector2Int>();
            int mapWidth = map.GetLength(1);
            int mapHeight = map.GetLength(0);

            foreach (Vector2Int coord in addedCells)
            {
                bool isPerim = false;

                for (int x = coord.x - 1; x <= coord.x + 1; x++)
                {
                    if (isPerim) { break; }

                    for (int y = coord.y - 1; y <= coord.y + 1; y++)
                    {
                        if (x < 0 || y < 0 || x >= mapWidth || y >= mapHeight) { continue; }
                        if (x == coord.x || y == coord.y)
                        {
                            Cell targetCell = map[y, x];

                            if (ignoreSameType && targetCell.cellType == CellType) { continue; }
                            else if (targetCell.cellType != CellType || targetCell.regionNum != RegionNum)
                            {
                                perimCoords.Add(coord);
                                isPerim = true;
                                break;
                            }
                        }
                    }
                }

                map[coord.y, coord.x].edgeCell = isPerim;
            }

            return perimCoords;
        }

        internal void UpdatePerimeterValues(Cell[,] map)
        {
            foreach (Vector2Int coord in PerimeterCoords) { map[coord.y, coord.x].edgeCell = false; }
            PerimeterCoords = CalculatePerimeterCoordsForCellGroup(map, CellCoords, false);
        }

        public override string ToString() => CellType + ":" + RegionNum;
        
        private void Initialize()
        {
            PerimeterCoords = new HashSet<Vector2Int>();
            ClosestCoordToRegion = new Dictionary<MapRegion, (Vector2Int, float)>();
            LinkedRegions = new HashSet<MapRegion>();
        }

        internal MapRegion(Cell[,] cells, HashSet<Vector2Int> cellCoords, CellType cellType, int regionNum)
        {
            RegionNum = regionNum;
            CellType = cellType;
            CellCoords = cellCoords;
            Initialize();
            UpdatePerimeterValues(cells);
        }

        internal MapRegion()
        {
            Initialize();
        }
    }
}
