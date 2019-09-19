using System;
using System.Collections.Generic;
using UnityEngine;

namespace LlamaZOO.MitchZais.CaveGenerator
{
//Mesh simplify code taken from the Unity3D wiki http://wiki.unity3d.com/index.php/Mesh_simplification_(for_MeshCollider,_lossless)
    public static class MeshUtils
    {
        public static void Simplify(this Mesh mesh)
        {
            var verts = mesh.vertices;
            var origNumVerts = verts.Length;

            var workingSet = new List<Vertice>(origNumVerts);
            for (int i = 0; i < origNumVerts; i++)
            {
                var r = new Vertice();
                r.position = verts[i];
                workingSet.Add(r);
            }

            var tris = mesh.triangles;
            var triLength = tris.Length;
            for (int i = 0; i < triLength; i += 3)
                Face.AddFace(workingSet, tris, i);

            for (int i = 0; i < origNumVerts; i++)
                workingSet[i].AssignLinearPosition();

            /*********************************
             *                               *
             *         Simplify mesh!        *
             *                               *
             ********************************/

            HashSet<Vertice> candidates;
            HashSet<Vertice> nextCandidates = new HashSet<Vertice>();

            foreach (Vertice v in workingSet) if (!v.IsStatic)
                    nextCandidates.Add(v);

            while (nextCandidates.Count != 0)
            {
                candidates = nextCandidates;
                nextCandidates = new HashSet<Vertice>();
                foreach (Vertice a in candidates)
                {
                    if (a.edges != null) foreach (Edge ac in a.edges)
                        {
                            Vertice c;
                            if (a.CanFollow(ac, out c))
                            {
                                foreach (Face f in ac.faces)
                                    Edge.Collapse(f.GetOpposite(c), f.GetOpposite(a), f);
                                foreach (Edge edge_of_a in a.edges)
                                {
                                    if (edge_of_a != ac)
                                    {
                                        var o = edge_of_a.GetOpposite(a);
                                        if (!o.IsStatic) nextCandidates.Add(o);
                                        edge_of_a.Reconnect(a, c);
                                    }
                                }
                                if (!c.IsStatic) nextCandidates.Add(c);

                                c.DisconnectFrom(ac);
                                a.Disconnect();
                                ac.DisconnectIncludingFaces();
                                break;
                            }
                        }
                }
            }

            var simplifiedVerts = new List<Vertice>();
            foreach (Vertice v in workingSet) if (v.edges != null)
                    simplifiedVerts.Add(v);

            var simplifiedNumVerts = simplifiedVerts.Count;
            var newPositions = new Vector3[simplifiedNumVerts];
            //var resultColors = new Color[simplifiedNumVerts];
            for (int i = 0; i < simplifiedNumVerts; i++)
            {
                simplifiedVerts[i].finalIndex = i;
                newPositions[i] = simplifiedVerts[i].position;
            }

            var resultTris = new List<int>();
            var triSet = new HashSet<Face>();
            foreach (Vertice v in simplifiedVerts)
            {
                foreach (Edge e in v.edges)
                {
                    foreach (Face f in e.faces) if (!triSet.Contains(f))
                        {
                            triSet.Add(f);
                            f.GetIndexes(resultTris);
                        }
                }
            }

            mesh.Clear();
            mesh.vertices = newPositions;
            mesh.triangles = resultTris.ToArray();

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            // Debug.LogFormat("Simplify vert count: {0} vs. {1}", simplifiedNumVerts, origNumVerts);
        }

        private class Vertice
        {
            public List<Edge> edges = new List<Edge>();
            public Vector3 position;

            /// <summary>A cache used for identifying a vertice during mesh reconstruction.
            public int finalIndex;

            /// <summary>
            ///  - Null if vertice is internal in a plane
            ///  - A non-zero vector if vertice is internal in a line
            ///  - Vector3.zero otherwise.
            /// </summary>
            public Vector3? linearPosition;

            public void AssignLinearPosition()
            {
                for (int i = 0; i < edges.Count; i++)
                {
                    var edge = edges[i];
                    if (!edge.HasEqualPlanes())
                    {
                        if (linearPosition == null)
                            linearPosition = edge.vertices[1].position - edge.vertices[0].position;
                        else if (!edge.IsParallel(linearPosition))
                        {
                            linearPosition = Vector3.zero;
                            break;
                        }
                    }
                }
            }

            public bool IsStatic
            {
                get
                {
                    return linearPosition == Vector3.zero;
                }
            }

            public Edge GetExistingConnectingEdge(Vertice v)
            {
                foreach (Edge e in edges)
                {
                    if (e.vertices[0] == this && e.vertices[1] == v) return e;
                    if (e.vertices[1] == this && e.vertices[0] == v) return e;
                }
                return null;
            }

            public Edge GetConnectingEdge(Vertice v)
            {
                Edge result = GetExistingConnectingEdge(v);
                if (result == null)
                {
                    result = new Edge(this, v);
                    edges.Add(result);
                    v.edges.Add(result);
                }
                return result;
            }

            /// <summary>When collapsing an edge a->c, the linear space must
            /// be respected, and all faces, not connected with a->c,
            /// must not flip.
            /// </summary>
            public bool CanFollow(Edge transportEdge, out Vertice opposite)
            {
                if (IsStatic) { opposite = default(Vertice); return false; }
                if (linearPosition != null && !transportEdge.IsParallel(linearPosition)) { opposite = default(Vertice); return false; }

                var localTris = new HashSet<Face>();
                foreach (Edge e in edges) foreach (Face f in e.faces)
                        localTris.Add(f);

                localTris.ExceptWith(transportEdge.faces);

                opposite = transportEdge.GetOpposite(this);
                var targetPos = opposite.position;
                var lTriEnum = localTris.GetEnumerator();
                try
                {
                    while (lTriEnum.MoveNext())
                        if (lTriEnum.Current.MoveWouldFlip(this, targetPos))
                            return false;
                }
                finally { lTriEnum.Dispose(); }
                return true;
            }

            public void DisconnectFrom(Edge e)
            {
                edges.Remove(e);
            }

            public void Disconnect()
            {
                edges.Clear();
                edges = null;
            }
        }

        private class Edge
        {
            public List<Vertice> vertices;
            public List<Face> faces;

            public static void Collapse(Edge moved, Edge target, Face f)
            {
                Face faceOutsideMoved;
                try { faceOutsideMoved = moved.GetOpposite(f); }
                catch (Exception e)
                {
                    throw new Exception(e.Message + "\n" + moved.vertices[0].position.ToString("F2") + " <--> " + moved.vertices[1].position.ToString("F2"));
                }
                faceOutsideMoved.Replace(moved, target);
                target.Replace(f, faceOutsideMoved);
                foreach (Vertice v in moved.vertices)
                {
                    v.edges.Remove(moved);
                }
            }

            public Edge(Vertice v0, Vertice v1)
            {
                vertices = new List<Vertice>(2);
                vertices.Add(v0);
                vertices.Add(v1);
                faces = new List<Face>(2);
            }

            public Vertice GetOpposite(Vertice v)
            {
                var v0 = vertices[0];
                return v != v0 ? v0 : vertices[1];
            }

            public Face GetOpposite(Face v)
            {
                if (faces.Count != 2) throw new Exception("Collapsing an edge with only 1 face into another. This is not supported.");
                var face0 = faces[0];
                return face0 == v ? faces[1] : face0;
            }

            public bool HasEqualPlanes()
            {
                if (faces.Count != 2) return false;
                var f0 = faces[0];
                var f0e0 = faces[0].edges[0];

                var f1 = faces[1];
                var f1e0 = faces[1].edges[0];

                var e0 = f0e0 != this ? f0e0 : f0.edges[1];
                var e1 = f1e0 != this ? f1e0 : f1.edges[1];

                var v0 = vertices[1].position - vertices[0].position;
                var v1 = e0.vertices[1].position - e0.vertices[0].position;
                var v2 = e1.vertices[1].position - e1.vertices[0].position;

                var n0 = Vector3.Cross(v0, v1);

                var dot = Vector3.Dot(n0, v2);
                return -5e-3 < dot && dot < 5e-3;
            }

            public void Replace(Face oldFace, Face newFace)
            {
                for (int j = 0; j < faces.Count; j++)
                {
                    if (faces[j] == oldFace)
                    {
                        faces[j] = newFace;
                        return;
                    }
                }
            }

            public void Reconnect(Vertice oldVertice, Vertice newVertice)
            {
                if (vertices[0] == oldVertice) vertices[0] = newVertice;
                else vertices[1] = newVertice;
                newVertice.edges.Add(this);
            }

            public bool Contains(Vertice v)
            {
                return v == vertices[0] || v == vertices[1];
            }

            public bool IsParallel(Vector3? nv)
            {
                var v0 = vertices[0].position;
                var v1 = vertices[1].position;
                float cross = Vector3.Cross(v1 - v0, nv.Value).sqrMagnitude;
                return -5e-6f < cross && cross < 5e-6f;
            }

            public void DisconnectIncludingFaces()
            {
                vertices.Clear();
                vertices = null;
                foreach (Face f in faces)
                    f.Disconnect();
                faces.Clear();
                faces = null;
            }
        }

        private class Face
        {
            public Edge[] edges;

            /*
             * <paramref name="e0">Edge between v0 and v1</paramref>
             * <paramref name="e1">Edge between v0 and v2</paramref>
             * <paramref name="e2">Edge between v1 and v2</paramref>
             */

            private Face(Edge e0, Edge e1, Edge e2)
            {
                edges = new Edge[] { e0, e1, e2 };
            }

            /*
             * <paramref name="allVertices">List of vertices, in the same order as 'verts' from the mesh.</paramref>
             * <paramref name="tris">The tris array from the mesh.</paramref>
             * <paramref name="triIndex">The index of the first vertex in this triangle.</paramref>
             */

            public static void AddFace(List<Vertice> allVertices, int[] tris, int triIndex)
            {
                var v0 = allVertices[tris[triIndex + 0]];
                var v1 = allVertices[tris[triIndex + 1]];
                var v2 = allVertices[tris[triIndex + 2]];

                var e0 = v0.GetConnectingEdge(v1);
                var e1 = v0.GetConnectingEdge(v2);
                var e2 = v1.GetConnectingEdge(v2);

                var face = new Face(e0, e1, e2);

                e0.faces.Add(face);
                e1.faces.Add(face);
                e2.faces.Add(face);
            }

            public Edge GetOpposite(Vertice v)
            {
                Edge e0, e1;
                if (!(e0 = edges[0]).Contains(v)) return e0;
                if (!(e1 = edges[1]).Contains(v)) return e1;
                return edges[2];
            }

            public Vertice GetOpposite(Edge o)
            {
                var o0 = o.vertices[0];
                var o1 = o.vertices[1];
                for (int i = 0; i < 3; i++)
                { // Will never reach 3 because there will be an unequal vertice before the third edge
                    Edge e = edges[i];
                    Vertice e0 = e.vertices[0];
                    if (e0 != o0 && e0 != o1) return e0;
                    Vertice e1 = e.vertices[1];
                    if (e1 != o0 && e1 != o1) return e1;
                }
                throw new Exception("A face seems to have three edges that all share a vertice with a given edge.");
            }

            public void Replace(Edge oldEdge, Edge newEdge)
            {
                if (edges[0] == oldEdge) edges[0] = newEdge;
                else if (edges[1] == oldEdge) edges[1] = newEdge;
                else edges[2] = newEdge;
            }

            public bool MoveWouldFlip(Vertice v, Vector3 p)
            {
                Edge oppositeEdge = GetOpposite(v);
                var ov0 = oppositeEdge.vertices[0].position;
                var ov = oppositeEdge.vertices[1].position - ov0;
                var ot = p - ov0;
                var ct = v.position - ov0;

                var cross0 = Vector3.Cross(ot, ov);
                var c0SqrMagnitude = cross0.sqrMagnitude;
                if (c0SqrMagnitude < 0.0001f) return true;

                var cross1 = Vector3.Cross(ct, ov);
                var c1SqrMagnitude = cross1.sqrMagnitude;
                if (c1SqrMagnitude < 0.0001f) return true;

                if (Mathf.Sign(Vector3.Dot(cross0, cross1)) < 0f) return true;

                return false;
            }

            /*
             * <summary>Adds the finalIndex for the verts in order: v0, v1, v2.</summary>
             */

            public void GetIndexes(List<int> results)
            {
                results.Add(GetOpposite(edges[2]).finalIndex);
                results.Add(GetOpposite(edges[1]).finalIndex);
                results.Add(GetOpposite(edges[0]).finalIndex);
            }

            public void Disconnect()
            {
                edges = null;
            }
        }
    }
}