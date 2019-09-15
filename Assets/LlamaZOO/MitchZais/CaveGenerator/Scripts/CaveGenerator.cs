using UnityEngine;

namespace LlamaZOO.MitchZais.CaveGenerator
{
    public class CaveGenerator : MonoBehaviour
    {  
        [SerializeField] private bool generateCave = false;
        [SerializeField] private bool generateNewSeedEachTime = false;
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
                GeneratePreviewPlane(mapParams.width, mapParams.height);

                MapPattern map = GenerateCave(mapParams);
                map.ApplyMapToTexture2D(ref mapTex, colorCodedRooms);
            }
        }

        private void GeneratePreviewPlane(int width, int height)
        {
            for(int c = transform.childCount - 1; c >= 0; c--)
            {
                DestroyImmediate(transform.GetChild(c).gameObject);
            }

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
            renderPlane.localPosition = new Vector3(width * 0.5f, 0, height * 0.5f);
            renderPlane.localEulerAngles = new Vector3(90, 0, 0);
            renderPlane.GetComponent<Renderer>().material = mapMaterial;
            plane.hideFlags = HideFlags.HideAndDontSave;
        }

        public MapPattern GenerateCave(MapParams mapParams)
        {
            map = new MapPattern(mapParams);
            map.Generate();

            return map;
        }
    }
}