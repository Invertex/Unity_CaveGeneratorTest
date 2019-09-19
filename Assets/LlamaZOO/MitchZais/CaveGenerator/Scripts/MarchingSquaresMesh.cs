using System.Collections.Generic;
using UnityEngine;

namespace LlamaZOO.MitchZais.CaveGenerator
{
//Adapted from Sebastian Lague's procedural generation tutorial
    internal class MarchingSquaresMesh
    {
        public Mesh Mesh { get; private set; }
        public List<List<Vector3>> Outlines { get; private set; }

        private List<Vector3> vertices;
        private List<int> triangles;
        private Dictionary<int, List<Triangle>> trianglesAtIndex = new Dictionary<int, List<Triangle>>();
        private HashSet<int> checkedVertices = new HashSet<int>();

        internal void GenerateMesh(MapPattern map, float height)
        {
            trianglesAtIndex.Clear();
            checkedVertices.Clear();
            vertices = new List<Vector3>(map.Width * map.Height);
            triangles = new List<int>((map.Width * map.Height) * 2);

            Mesh = GeneratePatternMesh(map, height);

            CalculateMeshOutlines();
        }

        private Mesh GeneratePatternMesh(MapPattern map, float height)
        {
            vertices = new List<Vector3>();
            triangles = new List<int>();

            MarchingGrid grid = new MarchingGrid(map, height);

            for (int y = 0; y < grid.squares.GetLength(0); y++)
            {
                for (int x = 0; x < grid.squares.GetLength(1); x++)
                {
                    TriangulateSquare(grid.squares[y, x]);
                }
            }

            Mesh mesh = new Mesh();

            mesh.indexFormat = (vertices.Count > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.Simplify();
           
            return mesh;
        }

        private void TriangulateSquare(Square square)
        {
            switch (square.configuration)
            {
                case 0:
                    break;

                // 1 points:
                case 1:
                    MeshFromPoints(square.centreLeft, square.centreBottom, square.bottomLeft);
                    break;
                case 2:
                    MeshFromPoints(square.bottomRight, square.centreBottom, square.centreRight);
                    break;
                case 4:
                    MeshFromPoints(square.topRight, square.centreRight, square.centreTop);
                    break;
                case 8:
                    MeshFromPoints(square.topLeft, square.centreTop, square.centreLeft);
                    break;

                // 2 points:
                case 3:
                    MeshFromPoints(square.centreRight, square.bottomRight, square.bottomLeft, square.centreLeft);
                    break;
                case 6:
                    MeshFromPoints(square.centreTop, square.topRight, square.bottomRight, square.centreBottom);
                    break;
                case 9:
                    MeshFromPoints(square.topLeft, square.centreTop, square.centreBottom, square.bottomLeft);
                    break;
                case 12:
                    MeshFromPoints(square.topLeft, square.topRight, square.centreRight, square.centreLeft);
                    break;
                case 5:
                    MeshFromPoints(square.centreTop, square.topRight, square.centreRight, square.centreBottom, square.bottomLeft, square.centreLeft);
                    break;
                case 10:
                    MeshFromPoints(square.topLeft, square.centreTop, square.centreRight, square.bottomRight, square.centreBottom, square.centreLeft);
                    break;

                // 3 point:
                case 7:
                    MeshFromPoints(square.centreTop, square.topRight, square.bottomRight, square.bottomLeft, square.centreLeft);
                    break;
                case 11:
                    MeshFromPoints(square.topLeft, square.centreTop, square.centreRight, square.bottomRight, square.bottomLeft);
                    break;
                case 13:
                    MeshFromPoints(square.topLeft, square.topRight, square.centreRight, square.centreBottom, square.bottomLeft);
                    break;
                case 14:
                    MeshFromPoints(square.topLeft, square.topRight, square.bottomRight, square.centreBottom, square.centreLeft);
                    break;

                // 4 point:
                case 15:
                    MeshFromPoints(square.topLeft, square.topRight, square.bottomRight, square.bottomLeft);
                    checkedVertices.Add(square.topLeft.vertexIndex);
                    checkedVertices.Add(square.topRight.vertexIndex);
                    checkedVertices.Add(square.bottomRight.vertexIndex);
                    checkedVertices.Add(square.bottomLeft.vertexIndex);
                    break;
            }

        }

        private void MeshFromPoints(params Node[] points)
        {
            AssignVertices(points);

            if (points.Length >= 3)
                CreateTriangle(points[0], points[1], points[2]);
            if (points.Length >= 4)
                CreateTriangle(points[0], points[2], points[3]);
            if (points.Length >= 5)
                CreateTriangle(points[0], points[3], points[4]);
            if (points.Length >= 6)
                CreateTriangle(points[0], points[4], points[5]);

        }

        private void AssignVertices(Node[] points)
        {
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].vertexIndex == -1)
                {
                    points[i].vertexIndex = vertices.Count;
                    vertices.Add(points[i].position);
                }
            }
        }

        private void CreateTriangle(Node a, Node b, Node c)
        {
            triangles.Add(a.vertexIndex);
            triangles.Add(b.vertexIndex);
            triangles.Add(c.vertexIndex);

            Triangle triangle = new Triangle(a.vertexIndex, b.vertexIndex, c.vertexIndex);
            AddTriangleToDictionary(triangle.vertexIndexA, triangle);
            AddTriangleToDictionary(triangle.vertexIndexB, triangle);
            AddTriangleToDictionary(triangle.vertexIndexC, triangle);
        }

        private void AddTriangleToDictionary(int vertexIndexKey, Triangle triangle)
        {
            if (trianglesAtIndex.ContainsKey(vertexIndexKey))
            {
                trianglesAtIndex[vertexIndexKey].Add(triangle);
            }
            else
            {
                List<Triangle> triangleList = new List<Triangle>();
                triangleList.Add(triangle);
                trianglesAtIndex.Add(vertexIndexKey, triangleList);
            }
        }

        private void CalculateMeshOutlines()
        {
            Outlines = new List<List<Vector3>>();

            for (int vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++)
            {
                if (!checkedVertices.Contains(vertexIndex))
                {
                    int newOutlineVertex = GetConnectedOutlineVertex(vertexIndex);
                    if (newOutlineVertex != -1)
                    {
                        checkedVertices.Add(vertexIndex);

                        List<Vector3> newOutline = new List<Vector3>();
                        newOutline.Add(vertices[vertexIndex]);
                        Outlines.Add(newOutline);
                        FollowOutline(newOutlineVertex, Outlines.Count - 1);
                        Outlines[Outlines.Count - 1].Add(vertices[vertexIndex]);
                    }
                }
            }

            SimplifyMeshOutlines();
        }

        private void SimplifyMeshOutlines()
        {
            for (int outlineIndex = 0; outlineIndex < Outlines.Count; outlineIndex++)
            {
                List<Vector3> simplifiedOutline = new List<Vector3>();
                Vector3 dirOld = Vector3.zero;
                for (int i = 0; i < Outlines[outlineIndex].Count; i++)
                {
                    Vector3 p1 = Outlines[outlineIndex][i];
                    Vector3 p2 = Outlines[outlineIndex][(i + 1) % Outlines[outlineIndex].Count];
                    Vector3 dir = p1 - p2;
                    if (dir != dirOld)
                    {
                        dirOld = dir;
                        simplifiedOutline.Add(Outlines[outlineIndex][i]);
                    }
                }
                Outlines[outlineIndex] = simplifiedOutline;
            }
        }

        private void FollowOutline(int vertexIndex, int outlineIndex)
        {
            Outlines[outlineIndex].Add(vertices[vertexIndex]);
            checkedVertices.Add(vertexIndex);
            int nextVertexIndex = GetConnectedOutlineVertex(vertexIndex);

            if (nextVertexIndex != -1)
            {
                FollowOutline(nextVertexIndex, outlineIndex);
            }
        }

        private int GetConnectedOutlineVertex(int vertexIndex)
        {
            List<Triangle> trianglesContainingVertex = trianglesAtIndex[vertexIndex];

            for (int i = 0; i < trianglesContainingVertex.Count; i++)
            {
                Triangle triangle = trianglesContainingVertex[i];

                for (int j = 0; j < 3; j++)
                {
                    int vertexB = triangle[j];
                    if (vertexB != vertexIndex && !checkedVertices.Contains(vertexB))
                    {
                        if (IsOutlineEdge(vertexIndex, vertexB))
                        {
                            return vertexB;
                        }
                    }
                }
            }

            return -1;
        }

        private bool IsOutlineEdge(int vertexA, int vertexB)
        {
            List<Triangle> trianglesContainingVertexA = trianglesAtIndex[vertexA];
            int sharedTriangleCount = 0;

            for (int i = 0; i < trianglesContainingVertexA.Count; i++)
            {
                if (trianglesContainingVertexA[i].Contains(vertexB))
                {
                    sharedTriangleCount++;
                    if (sharedTriangleCount > 1)
                    {
                        break;
                    }
                }
            }
            return sharedTriangleCount == 1;
        }

        private struct Triangle
        {
            internal int vertexIndexA;
            internal int vertexIndexB;
            internal int vertexIndexC;
            private int[] vertices;

            internal Triangle(int a, int b, int c)
            {
                vertexIndexA = a;
                vertexIndexB = b;
                vertexIndexC = c;

                vertices = new int[3];
                vertices[0] = a;
                vertices[1] = b;
                vertices[2] = c;
            }

            internal int this[int i]
            {
                get
                {
                    return vertices[i];
                }
            }


            internal bool Contains(int vertexIndex)
            {
                return vertexIndex == vertexIndexA || vertexIndex == vertexIndexB || vertexIndex == vertexIndexC;
            }
        }

        private  class MarchingGrid
        {
            internal Square[,] squares;

            internal MarchingGrid(MapPattern map, float height)
            {
                int nodeCountX = map.Width;
                int nodeCountY = map.Height;
                float halfTexelOffset =  map.SubdivPositionMultiplier * 0.5f;

                ControlNode[,] controlNodes = new ControlNode[nodeCountY, nodeCountX];

                for (int y = 0; y < nodeCountY; y++)
                {
                    for (int x = 0; x < nodeCountX; x++)
                    {
                        var cellCoord = new Vector2Int(x, y);
                        Vector3 pos = map.CoordToPos(cellCoord) + new Vector3(halfTexelOffset, height, halfTexelOffset);
                        controlNodes[y, x] = new ControlNode(pos, !map.IsOutsideMap(cellCoord) && map.Cells[cellCoord.y, cellCoord.x] == CellType.Wall, halfTexelOffset);
                    }
                }

                squares = new Square[nodeCountY - 1, nodeCountX - 1];
                for (int y = 0; y < nodeCountY - 1; y++)
                {
                    for (int x = 0; x < nodeCountX - 1; x++)
                    {
                        squares[y, x] = new Square(controlNodes[y + 1, x], controlNodes[y + 1, x + 1], controlNodes[y, x + 1], controlNodes[y, x]);
                    }
                }
            }
        }
        private  class Square
        {

            internal ControlNode topLeft, topRight, bottomRight, bottomLeft;
            internal Node centreTop, centreRight, centreBottom, centreLeft;
            internal int configuration;

            internal Square(ControlNode _topLeft, ControlNode _topRight, ControlNode _bottomRight, ControlNode _bottomLeft)
            {
                topLeft = _topLeft;
                topRight = _topRight;
                bottomRight = _bottomRight;
                bottomLeft = _bottomLeft;

                centreTop = topLeft.right;
                centreRight = bottomRight.up;
                centreBottom = bottomLeft.right;
                centreLeft = bottomLeft.up;

                if (topLeft.active)
                    configuration += 8;
                if (topRight.active)
                    configuration += 4;
                if (bottomRight.active)
                    configuration += 2;
                if (bottomLeft.active)
                    configuration += 1;
            }
        }

        private class Node
        {
            internal Vector3 position;
            internal int vertexIndex = -1;

            internal Node(Vector3 position)
            {
                this.position = position;
            }
        }

        private class ControlNode : Node
        {
            internal bool active;
            internal Node up, right;

            internal ControlNode(Vector3 _pos, bool _active, float halfSizeOffset) : base(_pos)
            {
                active = _active;
                up = new Node(position + Vector3.forward * halfSizeOffset);
                right = new Node(position + Vector3.right * halfSizeOffset);
            }
        }

        internal MarchingSquaresMesh(MapPattern map, float height) => GenerateMesh(map, height);
    }
}
