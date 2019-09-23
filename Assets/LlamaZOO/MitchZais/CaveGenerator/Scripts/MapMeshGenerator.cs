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
            var marchedPatternData = new MarchingSquaresMesh(map, height);
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
            
            if(loopingOutline && outline[0] != outline[outlineCnt - 1])
            {
                outline = new List<Vector3>(outline);
                outline.Add(outline[0]);
                
                outlineCnt++;
            }

            List<int> triangles = new List<int>();
            List<Vector3> verts = new List<Vector3>(outline.Count * 2);
            List<Vector2> uvs = new List<Vector2>(outline.Count * 2);
            float uvDistAccum = 0;
            Vector3 thickness = Vector3.up * height;

            //Set first vertical edge so we can loop easily without duplicating verts
            verts.Add(outline[0]);
            verts.Add(outline[0] + thickness);
            int triangleOffset = 2;

            uvs.Add(new Vector2(0, uvDistAccum));
            uvs.Add(new Vector2(height, uvDistAccum));

            for (int i = 1; i < outlineCnt; i++)
            {
                Vector3 vert = outline[i];
                verts.Add(vert); 
                verts.Add(vert + thickness);

                uvDistAccum += Vector3.Distance(vert, outline[i - 1]);
                uvs.Add(new Vector2(0, uvDistAccum));
                uvs.Add(new Vector2(height, uvDistAccum));

                triangles.Add(triangleOffset - 1);
                triangles.Add(triangleOffset + 1);
                triangles.Add(triangleOffset - 2);

                triangles.Add(triangleOffset); 
                triangles.Add(triangleOffset - 2);
                triangles.Add(triangleOffset + 1); 

                triangleOffset += 2;
            }

            Mesh mesh = new Mesh();
            mesh.vertices = verts.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();

            var uv2 = new Vector2[uvs.Count];
            var uvScaleFactor = new Vector2(1 / height, uvs.Count / uvDistAccum);

            for(int i = 1; i < uvs.Count; i+= 2)
            {
                uv2[i] = uvs[i] * uvScaleFactor;
            }

            mesh.uv2 = uv2;
            mesh.uv3 = uv2;

            mesh.RecalculateNormals();
            //Average the normal across the UV seam
            var normals = mesh.normals;
            Vector3 avgSeamNormal = (normals[0] + normals[normals.Length - 1]) / 2f;
            normals[normals.Length - 2] = avgSeamNormal;
            normals[normals.Length - 1] = avgSeamNormal; 
            normals[0] = avgSeamNormal;
            normals[1] = avgSeamNormal;
            
            mesh.normals = normals;

            return mesh;
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