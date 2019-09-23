using UnityEngine;

namespace LlamaZOO.MitchZais.CaveGenerator
{
    public class Cave : MonoBehaviour
    {
        public MapPresetSO mapPreset;
        
        [SerializeField] private Transform mapMeshGroup;
        public Transform MapMeshGroup { get { return mapMeshGroup; } }

        private void Awake()
        {
            if(mapMeshGroup == null) { GenerateCave(); }
        }

        public void GenerateCave()
        {
            if (mapPreset != null) { GenerateCave(mapPreset); }
        }

        public MapPattern GenerateCave(MapPresetSO mapPreset)
        {
            if (mapPreset == null) { return null; }

            var map = new MapPattern(mapPreset.MapParams);

            map.Generate();
            mapPreset.minimap = GeneratePatternTexture(mapPreset, map, true);
            GenerateMeshes(map);

            return map;
        }

        private void GenerateMeshes(MapPattern mapPattern)
        {   
            ClearGeneratedChildren();

            MapMeshGenerator meshGen = new MapMeshGenerator(mapPattern, mapPreset.MapParams.wallHeight);

            mapMeshGroup = new GameObject("MapMeshGroup").transform;
            mapMeshGroup.SetParent(this.transform, false);

            InstantiateMesh(meshGen.GroundMesh, mapPreset.groundMaterial, true, "Ground");
            InstantiateMesh(meshGen.WallMeshExternal, mapPreset.wallMaterial, false, "WallExternal");
            InstantiateMesh(meshGen.PatternMesh, mapPreset.capMaterial, false, "MapPattern");
            foreach (var wall in meshGen.WallMeshesInternal) { InstantiateMesh(wall, mapPreset.wallMaterial, true, "WallInternal"); }
        }

        public Texture2D GeneratePatternTextureOnly(MapPresetSO mapPreset, bool colorCodedRooms)
        {
            var mapPattern = new MapPattern(mapPreset.MapParams);
            mapPattern.Generate();

            return GeneratePatternTexture(mapPreset, mapPattern, colorCodedRooms);
        }

        public Texture2D GeneratePatternTexture(MapPresetSO mapPreset, MapPattern mapPattern, bool colorCodedRooms)
        {
            if (mapPreset.minimap == null)
            {
                mapPreset.minimap = new Texture2D(mapPreset.MapParams.width, mapPreset.MapParams.height) { filterMode = FilterMode.Point };
            }

            mapPattern.ApplyMapToTexture2D(ref mapPreset.minimap, colorCodedRooms);
           
            return mapPreset.minimap;
        }

        private void ClearGeneratedChildren()
        {
            if(mapMeshGroup == null) { return; }

            for (int c = transform.childCount - 1; c >= 0; c--)
            {
                if (Application.isPlaying) { Destroy(transform.GetChild(c).gameObject); }
                else { DestroyImmediate(transform.GetChild(c).gameObject); }
            }
        }

        private void InstantiateMesh(Mesh mesh, Material mat, bool addMeshCollider, string objName)
        {
            GameObject obj = new GameObject(objName, new System.Type[] { typeof(MeshFilter), typeof(MeshRenderer) });
            obj.transform.SetParent(mapMeshGroup, false);
            obj.isStatic = true;
            MeshFilter mf = obj.GetComponent<MeshFilter>();
            mf.mesh = mesh;
            var rend = obj.GetComponent<Renderer>();
            rend.material = mat;

            if (addMeshCollider) { obj.AddComponent<MeshCollider>().sharedMesh = mesh; }
        }
    }
}
