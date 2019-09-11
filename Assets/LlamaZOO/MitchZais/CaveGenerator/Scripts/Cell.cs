namespace LlamaZOO.MitchZais.CaveGenerator
{
    public struct Cell
    {
        public CellType cellType;
        public int regionNum;
        public bool edgeCell;

        public const int DefaultRegionNum = 0;

        public Cell(CellType cellType, int regionNum = DefaultRegionNum, bool edgeCell = false)
        {
            this.cellType = cellType;
            this.regionNum = regionNum;
            this.edgeCell = edgeCell;
        }

        public static implicit operator CellType(Cell cell) => cell.cellType;
    }
}