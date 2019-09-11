using System.Collections.Generic;
using UnityEngine;

namespace LlamaZOO.MitchZais.CaveGenerator
{
[System.Serializable]
    public class MapRegion
    {
        public int Area { get { return CellCoords.Count; } }
        public int RegionNum { get; private set; }
        public CellType CellType { get; private set; }
        public List<Vector2Int> CellCoords { get; private set; }
        public List<Vector2Int> PerimeterCoords { get; private set; }
        public HashSet<MapRegion> linkedRegions { get; private set; }

        internal void AddRegion(Cell[,] cells, MapRegion region, bool updatePerimeter = true)
        {
            if (region == null) { return; }
            region.UpdateRegionCells(cells, CellType, RegionNum, false);
            CellCoords.AddRange(region.CellCoords);
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

        internal List<MapRegion> FindTouchingRegions(MapPattern map, int maxReturnedRegions = 4)
        {
            List<MapRegion> touchingRegions = new List<MapRegion>(maxReturnedRegions);
            HashSet<(CellType cellType, int regionNum)> alreadyFound = new HashSet<(CellType cellType, int regionNum)>();
            int foundRegionCnt = 0;

            foreach(var coord in PerimeterCoords)
            {
                Cell thisCell = map.Map[coord.y, coord.x];

                for (int x = coord.x - 1; x < coord.x + 1; x++)
                {
                    for (int y = coord.y - 1; y < coord.y + 1; y++)
                    {
                        if (x != coord.x && y != coord.y && !map.IsOutsideMap(map.Map, new Vector2Int(x, y)))
                        {
                            Cell sampledCell = map.Map[y, x];

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

        internal void LinkRegion(MapRegion region)
        {
            linkedRegions.Add(region);
            region.linkedRegions.Add(this);
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

        internal void UpdatePerimeterValues(Cell[,] map)
        {
            foreach (Vector2Int coord in PerimeterCoords) { map[coord.y, coord.x].edgeCell = false; }

            PerimeterCoords = new List<Vector2Int>(CellCoords.Count / 10);

            int mapWidth = map.GetLength(1);
            int mapHeight = map.GetLength(0);

            foreach (Vector2Int coord in CellCoords)
            {
                bool isPerim = false;

                for (int x = coord.x - 1; x <= coord.x + 1; x++)
                {
                    if(isPerim){ break; }

                    for (int y = coord.y - 1; y <= coord.y + 1; y++)
                    {
                        if (x < 0 || y < 0 || x >= mapWidth || y >= mapHeight) { continue; }
                        if (x == coord.x || y == coord.y)
                        {
                            if (map[y, x].regionNum != RegionNum || map[y, x].cellType != CellType)
                            {
                                PerimeterCoords.Add(coord);
                                isPerim = true;
                                break;
                            }
                        }
                    }
                }

                map[coord.y, coord.x].edgeCell = isPerim;
            }
        }

        internal MapRegion(Cell[,] cells, List<Vector2Int> cellCoords, CellType cellType, int regionNum)
        {
            RegionNum = regionNum;
            CellType = cellType;
            CellCoords = cellCoords;
            PerimeterCoords = new List<Vector2Int>(0);
            UpdatePerimeterValues(cells);
        }
    }
}
