using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LlamaZOO.MitchZais.CaveGenerator
{
    public class CaveGenerator : MonoBehaviour
    {  
        [SerializeField] private bool generateCave = false;
        [SerializeField] private bool generateNewSeedEachTime = false;

        [Header("Map Generation Parameters")]
        [SerializeField] private MapParams mapParams;

        private Transform renderPlane;
        private Texture2D mapTex;
        private Material mapMaterial;

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
                map.ApplyMapToTexture2D(ref mapTex);
            }
        }

        private void GeneratePreviewPlane(int width, int height)
        {
            if (transform.childCount > 0)
            {
                renderPlane = transform.GetChild(0);
            }
            else
            {
                GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
                renderPlane = plane.transform;
                
                renderPlane.name = "MapPreviewPlane";
                renderPlane.SetParent(this.transform, false);
                renderPlane.transform.eulerAngles = new Vector3(90, 0, 0);
            }
            if(!mapMaterial) { mapMaterial = new Material(Shader.Find("Unlit/Texture")); }
            if(!mapTex)
            {
                mapTex = new Texture2D(width, height);
                mapTex.filterMode = FilterMode.Point;
            }

            mapMaterial.mainTexture = mapTex;
            renderPlane.GetComponent<Renderer>().material = mapMaterial;
            renderPlane.localScale = new Vector3(width, height, 1);
        }

        public MapPattern GenerateCave(MapParams mapParams)
        {
            MapPattern map = new MapPattern(mapParams.width, mapParams.height, mapParams.seed, mapParams.density, mapParams.refinementSteps, mapParams.subdivs);
            map.Generate();

            return map;
        }
    }
}