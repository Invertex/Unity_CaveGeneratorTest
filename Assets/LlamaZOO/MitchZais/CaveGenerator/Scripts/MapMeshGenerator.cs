using System.Collections.Generic;
using UnityEngine;

namespace LlamaZOO.MitchZais.CaveGenerator
{
    public class MapMeshGenerator
    {
        public Mesh GroundMesh { get; private set; }
        public Mesh WallMeshExternal { get; private set; }
        public Mesh[] WallMeshesInternal { get; private set; }
        public Mesh PatternMesh { get; private set; }

        private MapPattern map;

        internal void GenerateMapMesh(MapPattern map, float height)
        {
            this.map = map;
            GroundMesh = GenerateGroundMesh();
            WallMeshExternal = GenerateWallMesh(GroundMesh.vertices, height, true, true);
            MarchingSquaresMesh marchedPatternData = new MarchingSquaresMesh(map, height);
            PatternMesh = marchedPatternData.Mesh;
            GeneratePlanarUVs(PatternMesh);
            WallMeshesInternal = WallMeshesFromOutlines(marchedPatternData.Outlines, -height);
        }

        private Mesh GenerateGroundMesh()
        {
            Mesh plane = new Mesh();
            plane.name = "CaveGroundPlane";
            float halfTexelOffset = map.SubdivPositionMultiplier * 0.5f;
            Vector3[] verts = new Vector3[4];

            (int vertIdx, Vector2Int coord) bottomLeft, bottomRight, topLeft, topRight;
            topLeft =       (0, new Vector2Int(0, map.Height));
            bottomLeft =    (1, new Vector2Int(0, 0));
            bottomRight =   (2, new Vector2Int(map.Width, 0));
            topRight =      (3, new Vector2Int(map.Width, map.Height));

            verts[topLeft.vertIdx] = map.CoordToPos(topLeft.coord) + new Vector3(halfTexelOffset, 0, -halfTexelOffset);
            verts[bottomLeft.vertIdx] = map.CoordToPos(bottomLeft.coord) + new Vector3(halfTexelOffset, 0, halfTexelOffset);
            verts[bottomRight.vertIdx] = map.CoordToPos(bottomRight.coord) + new Vector3(-halfTexelOffset, 0, halfTexelOffset);
            verts[topRight.vertIdx] = map.CoordToPos(topRight.coord) + new Vector3(-halfTexelOffset, 0, -halfTexelOffset);

            int[] triangles = new int[6];

            triangles[0] = topLeft.vertIdx;
            triangles[1] = bottomRight.vertIdx;
            triangles[2] = bottomLeft.vertIdx;

            triangles[3] = topLeft.vertIdx;
            triangles[4] = topRight.vertIdx;
            triangles[5] = bottomRight.vertIdx;

            plane.vertices = verts;
            plane.triangles = triangles;

            Vector2[] uvs = new Vector2[] { new Vector2(verts[0].x, verts[0].z), 
                                       new Vector2(verts[1].x, verts[1].z),
                                       new Vector2(verts[2].x, verts[2].z),
                                       new Vector2(verts[3].x, verts[3].z) };
            plane.uv = uvs;
            var lightmapUVs = Generate01SpaceUV(plane.vertices);
            plane.uv2 = lightmapUVs;
            plane.uv3 = lightmapUVs;
            plane.RecalculateNormals();

            return plane;
        }

        private void GeneratePlanarUVs(Mesh mesh)
        {
            var verts = mesh.vertices;
            var uv = new Vector2[verts.Length];
            var uv2 = new Vector2[verts.Length];

            for(int i = 0; i < verts.Length; i++)
            {
                uv[i] = new Vector2(verts[i].x, verts[i].z);
                uv2[i] = map.PosToUV01(verts[i]);
            }

            mesh.uv = uv;
            mesh.uv2 = uv2;
            mesh.uv3 = uv2;
        }

        private Vector2[] Generate01SpaceUV(IList<Vector3> verts)
        {
            var uvs = new Vector2[verts.Count];

            for(int i = 0; i < verts.Count; i++)
            {
                uvs[i] = map.PosToUV01(verts[i]);
            }

            return uvs;
        }

        private Mesh[] WallMeshesFromOutlines(List<List<Vector3>> outlines, float height)
        {
            var meshes = new List<Mesh>(outlines.Count);

            foreach(var outline in outlines)
            {
                Mesh wallMesh = GenerateWallMesh(outline, height, loopingOutline: true, hardenEdges: false);
                if(wallMesh != null) { meshes.Add(wallMesh); }
            }

            return meshes.ToArray();
        }

        private Mesh GenerateWallMesh(IList<Vector3> outline, float height, bool loopingOutline = false, bool hardenEdges = false)
        {
            int outlineCnt = outline.Count;
            if (outlineCnt < 4) { return null; }

            if(hardenEdges)
            {
                outline = DuplicateVertsForHardEdges(outline);
                outlineCnt = outline.Count;
            }

            List<Vector3> wallVertices = new List<Vector3>(outline.Count * 2);
            List<int> wallTriangles = new List<int>();

            Vector3 thickness = Vector3.up * height;

            int triangleOffset = 0;
            int localVertOffset = wallVertices.Count;

            if (outline[outlineCnt - 1] == outline[0])
            {
                loopingOutline = true;
                outlineCnt -= 1;
            }

            //Set first vertical edge so we can loop easily without duplicating verts
            wallVertices.Add(outline[0]);
            wallVertices.Add(outline[0] + thickness);

            for (int i = 1; i < outlineCnt; i++)
            {
                triangleOffset = wallVertices.Count;
                wallVertices.Add(outline[i]); // right
                wallVertices.Add(outline[i] + thickness); // bottom right

                wallTriangles.Add(triangleOffset - 1); //bottom left
                wallTriangles.Add(triangleOffset + 1); //bottom right
                wallTriangles.Add(triangleOffset - 2); //top left

                wallTriangles.Add(triangleOffset); //bottom left
                wallTriangles.Add(triangleOffset - 2);
                wallTriangles.Add(triangleOffset + 1); // bottom right
            }

            if (loopingOutline)
            {
                //Link back with starting verts
                wallTriangles.Add(triangleOffset + 1);
                wallTriangles.Add(localVertOffset + 1);
                wallTriangles.Add(localVertOffset);

                wallTriangles.Add(localVertOffset);
                wallTriangles.Add(triangleOffset);
                wallTriangles.Add(triangleOffset + 1);
            }
            
            Mesh wallMesh = new Mesh();
            wallMesh.vertices = wallVertices.ToArray();
            wallMesh.triangles = wallTriangles.ToArray();
            wallMesh.RecalculateNormals();

            return wallMesh;
        }

        internal static IList<Vector3> DuplicateVertsForHardEdges(IList<Vector3> verts)
        {
            if(verts.Count == 0){ return verts; }
            var dupedVerts = new Vector3[verts.Count * 2];

            for (int i = 0; i < verts.Count; i++)
            {
                int offset = i * 2;
                dupedVerts[offset] = verts[i];
                dupedVerts[offset + 1] = verts[i];
            }

            return dupedVerts;
        }

        public MapMeshGenerator(MapPattern map, float height = 5f) => GenerateMapMesh(map, height);
    }
}