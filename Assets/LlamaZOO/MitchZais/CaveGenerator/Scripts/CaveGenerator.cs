using UnityEngine;

namespace LlamaZOO.MitchZais.CaveGenerator
{
    public class CaveGenerator : MonoBehaviour
    {
        public Material groundMat;
        public Material wallMat;
        public MapMeshGenerator meshGenerator;

        [SerializeField] private bool generateCave = false;
        [SerializeField] private bool generateNewSeedEachTime = false;

        [Header("Debug")]
        [SerializeField] private bool previewPatternOnFloor = false;
        [SerializeField] private bool colorCodedRooms = false;

        [Header("Map Generation Parameters")]
        [SerializeField] private MapParams mapParams;
        public MapPattern map;

        private Transform renderPlane;
        private Texture2D mapTex;
        private Material mapMaterial;

        private void Reset()
        {
            mapParams = new MapParams();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            //Processing MonoBehaviour types immedietly in OnValidate will throw warnings and potential errors
            //This delays it until Inspector updating is finished.
            UnityEditor.EditorApplication.delayCall += ProcessUIChange;
        }
#endif   
        private void ProcessUIChange()
        {
            if (mapParams == null) { mapParams = new MapParams(); }

            if (generateCave)
            {
                generateCave = false;
                if (generateNewSeedEachTime) { mapParams.GenerateNewSeed(); }
                ClearChildrenImmediate();

                MapPattern map = GenerateCave(mapParams);
                map.ApplyMapToTexture2D(ref mapTex, colorCodedRooms);

                MapMeshGenerator meshGen = new MapMeshGenerator(map, 5);
                if (previewPatternOnFloor) { GeneratePreviewPlane(mapParams.width, mapParams.height); }
                else { InstantiateMesh(meshGen.GroundMesh, groundMat, true, "Ground"); }
                InstantiateMesh(meshGen.WallMeshExternal, wallMat, false, "WallExternal");
                InstantiateMesh(meshGen.PatternMesh, wallMat, false, "MapPattern");
                foreach(var wall in meshGen.WallMeshesInternal) { InstantiateMesh(wall, wallMat, true, "WallInternal"); }
            }
        }


        private void InstantiateMesh(Mesh mesh, Material mat, bool addMeshCollider, string objName)
        {
            GameObject obj = new GameObject(objName, new System.Type[] { typeof(MeshFilter), typeof(MeshRenderer) });
            obj.transform.SetParent(this.transform, false);
            obj.isStatic = true;
            MeshFilter mf = obj.GetComponent<MeshFilter>();
            mf.mesh = mesh;
            var rend = obj.GetComponent<Renderer>();
            rend.material = mat;

            if(addMeshCollider){ obj.AddComponent<MeshCollider>().sharedMesh = mesh; }
        }

        private void GeneratePreviewPlane(int width, int height)
        {
            if (!mapMaterial) { mapMaterial = new Material(Shader.Find("Unlit/Texture")); }
            if (!mapTex)
            {
                mapTex = new Texture2D(width, height) { filterMode = FilterMode.Point };
            }

            mapMaterial.mainTexture = mapTex;

            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            renderPlane = plane.transform;
            renderPlane.name = "MapPreviewPlane";
            renderPlane.SetParent(this.transform, false);
            renderPlane.localScale = new Vector3(width, height, 1);
            renderPlane.localPosition = new Vector3(width /2, 0, height / 2);
            renderPlane.localEulerAngles = new Vector3(90, 0, 0);
            renderPlane.GetComponent<Renderer>().material = mapMaterial;
        }
        private void ClearChildrenImmediate()
        {
            for (int c = transform.childCount - 1; c >= 0; c--)
            {
                if (Application.isPlaying) { Destroy(transform.GetChild(c).gameObject); }
                else { DestroyImmediate(transform.GetChild(c).gameObject); }
            }
        }

        public MapPattern GenerateCave(MapParams mapParams)
        {
            map = new MapPattern(mapParams);
            map.Generate();

            return map;
        }
    }
}