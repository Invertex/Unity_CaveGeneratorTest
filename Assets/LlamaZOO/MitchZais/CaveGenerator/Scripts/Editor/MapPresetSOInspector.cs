using System.Linq;
using UnityEngine;
using UnityEditor;
using LlamaZOO.MitchZais.CaveGenerator;

namespace LlamaZOO.MitchZais.CaveGeneratorEditor
{
    [ CustomEditor(typeof(MapPresetSO))]
    public class MapPresetSOInspector : Editor
    {
        [SerializeField] private bool showRefinementSteps = true;
        public MapPresetSO MapPreset { get{ return (target as MapPresetSO); } }

        public override void OnInspectorGUI()
        {
            DrawUserEditableInterface();
            DrawMiniMapPreview();
        }

        public void DrawUserEditableInterface()
        {
            Undo.RecordObject(MapPreset, "Modified Map Preset");
            DrawMapParams(MapPreset.MapParams);
            DrawMapInfo();
            EditorUtility.SetDirty(MapPreset);
        }

        private void DrawMapParams(MapParams mapParams)
        {
            EditorGUILayout.LabelField("", MapEditorGUIStyles.ThinLineHorizontal);
            EditorGUILayout.LabelField(".:Generation Parameters:.", MapEditorGUIStyles.LabelUpperCenter);

            EditorGUILayout.BeginHorizontal();
                mapParams.seed = EditorGUILayout.IntField("Seed: ", mapParams.seed);
                if (GUILayout.Button("Randomize", MapEditorGUIStyles.ButtonCentered)) { mapParams.seed = mapParams.GenerateNewSeed(); }
            EditorGUILayout.EndHorizontal();

            mapParams.width = Mathf.Max(EditorGUILayout.IntField("Width:", mapParams.width), 16);
            mapParams.height = Mathf.Max(EditorGUILayout.IntField("Height: ", mapParams.height), 16);
            mapParams.wallHeight = EditorGUILayout.Slider("Wall Height: ", mapParams.wallHeight, 0.05f, 30f);
            mapParams.fillDensity = EditorGUILayout.Slider("Fill Density: ", mapParams.fillDensity, 0.3f, 1.0f);

            int maxArea = mapParams.SubdividedHeight * mapParams.SubdividedWidth / 2;
            mapParams.minRoomArea = Mathf.Clamp(EditorGUILayout.IntField("Min Room Area: ", mapParams.minRoomArea), 9, maxArea);
            mapParams.minWallArea = Mathf.Clamp(EditorGUILayout.IntField("Min Wall Area: ", mapParams.minWallArea), 0, maxArea);
            
            DrawRefinementSteps(mapParams);
        }

        private void DrawRefinementSteps(MapParams mapParams)
        {
            var refineSteps = mapParams.refinementSteps;
            if(refineSteps.Length == 0){ refineSteps = new MapParams.RefinementStep[1]; }

            EditorGUILayout.Space();
            showRefinementSteps = EditorGUILayout.Foldout(showRefinementSteps, "Refinement Steps:");

            if(!showRefinementSteps) { return; }
            
            EditorGUI.indentLevel++;

            for(int i = 0; i < mapParams.refinementSteps.Length; i++)
            {
                EditorGUILayout.LabelField("", MapEditorGUIStyles.ThinLineHorizontal);
                refineSteps[i].iterations = EditorGUILayout.IntSlider(new GUIContent("Iterations: "), refineSteps[i].iterations, 0, 20);
                refineSteps[i].cellLiveThreshold = EditorGUILayout.IntSlider(new GUIContent("Cell Live Threshold: ", "A cell lives if its alive neighbor count is above this value."), refineSteps[i].cellLiveThreshold, 1, 7);
                refineSteps[i].cellDeathThreshold = EditorGUILayout.IntSlider(new GUIContent("Cell Death Threshold: ", "A cell dies if its alive neighbors are below this value."), refineSteps[i].cellDeathThreshold, 1, 7);
                
                EditorGUILayout.BeginHorizontal();
                    refineSteps[i].subdivideFirst = EditorGUILayout.Toggle(new GUIContent("Subdivide cells first "), refineSteps[i].subdivideFirst);
                
                    if (refineSteps.Length > 1 && GUILayout.Button("Remove"))
                    {
                        var modifiedCollection = refineSteps.ToList();
                        modifiedCollection.RemoveAt(i);
                        refineSteps = modifiedCollection.ToArray();
                        i += 1;
                    }
                EditorGUILayout.EndHorizontal();
            }
   
            EditorGUI.indentLevel--;

            if (GUILayout.Button("Add Refinement Step"))
            {
                var increasedSteps = new MapParams.RefinementStep[refineSteps.Length + 1];
                refineSteps.CopyTo(increasedSteps, 0);
                refineSteps = increasedSteps;
                refineSteps[refineSteps.Length - 1] = new MapParams.RefinementStep(4);
            }
            
            mapParams.refinementSteps = refineSteps;
        }

        private void DrawMapInfo()
        {
            EditorGUILayout.LabelField("", MapEditorGUIStyles.ThinLineHorizontal);
            EditorGUILayout.LabelField(".:Map Info:.", MapEditorGUIStyles.LabelUpperCenter);

            MapPreset.mapName = EditorGUILayout.TextField("Map Name: ", MapPreset.mapName);
            MapPreset.groundMaterial = EditorGUILayout.ObjectField(label: "Ground Material", obj: MapPreset.groundMaterial, objType: typeof(Material), allowSceneObjects: false) as Material;
            MapPreset.wallMaterial = EditorGUILayout.ObjectField(label: "Wall Material", obj: MapPreset.wallMaterial, objType: typeof(Material), allowSceneObjects: false) as Material;
            MapPreset.capMaterial = EditorGUILayout.ObjectField(label: "Cap Material", obj: MapPreset.capMaterial, objType: typeof(Material), allowSceneObjects: false) as Material;
        }

        private void DrawMiniMapPreview()
        {
            

            EditorGUILayout.LabelField("", MapEditorGUIStyles.ThinLineHorizontal);
            EditorGUILayout.LabelField(".:Minimap Preview:.", MapEditorGUIStyles.LabelUpperCenter);
           if (MapPreset.minimap == null) { return; }

            Rect drawArea = GUILayoutUtility.GetRect(32, 600, 32, 600);
            EditorGUI.DrawPreviewTexture(drawArea, MapPreset.minimap, null, ScaleMode.ScaleToFit);
            EditorGUILayout.Space();
        }
    }
}