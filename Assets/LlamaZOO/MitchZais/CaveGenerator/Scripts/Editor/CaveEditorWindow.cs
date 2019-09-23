using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LlamaZOO.MitchZais.CaveGenerator;
using System.IO;

namespace LlamaZOO.MitchZais.CaveGeneratorEditor
{
[System.Serializable]
    public class CaveEditorWindow : EditorWindow
    {
        [SerializeField] private Cave cave;

        [SerializeField] private SerializedObject preset;
        [SerializeField] private Editor presetEditor;

        /** Map Editor Settings **/
        [SerializeField] private bool genPreviewTextureOnly;
        [SerializeField] private bool genPreviewTexture;
        [SerializeField] private bool colorCodedRooms;
        [SerializeField] private bool genRandomSeedEachTime;
        [SerializeField] private string saveName = "New Map";
        [SerializeField] private Texture2D previewTexture;

        private MapPresetSO MapPreset { get { return cave.mapPreset; } }
        private MapParams MapParams { get { return MapPreset.MapParams; } }

        [SerializeField] private Vector2 scroll;

        private void OnGUI()
        {
            DrawHeader();

            if(!CanDrawControls()){ return; }

            scroll = EditorGUILayout.BeginScrollView(scroll);

                if(DrawPresetSOInspector())
                {
                    preset.ApplyModifiedProperties();
                }

                DrawMapGenerationControls();
                DrawSaveControls();
                DrawPatternTexture(previewTexture);

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space();
            var obj = EditorGUILayout.ObjectField(obj: cave, objType: typeof(Cave), allowSceneObjects: true, label: new GUIContent("Editing Cave: "));
        
            if(obj == null || (obj as Cave) != cave)
            {
                SetCaveContext(obj as Cave);
            }
        }

        private bool DrawPresetSOInspector()
        {
            preset.Update();
            EditorGUI.BeginChangeCheck();
            presetEditor.OnInspectorGUI();
     
            return EditorGUI.EndChangeCheck();
        }

        private void DrawMapGenerationControls()
        {
            EditorGUILayout.LabelField("", MapEditorGUIStyles.ThinLineHorizontal);
            EditorGUILayout.LabelField(".:Map Generation:.", MapEditorGUIStyles.LabelUpperCenter);

            genPreviewTexture = EditorGUILayout.ToggleLeft("Generate Minimap", genPreviewTexture);
            EditorGUI.BeginDisabledGroup(!genPreviewTexture);
                colorCodedRooms = EditorGUILayout.ToggleLeft(new GUIContent("Color Coded Rooms", "Each separate room island that became connected will have a different color."), colorCodedRooms);
                genPreviewTextureOnly = EditorGUILayout.ToggleLeft(new GUIContent("Only Generate Minimap", "Skips meshing to speed up pattern generating."), genPreviewTextureOnly); 
            EditorGUI.EndDisabledGroup();

            genRandomSeedEachTime = EditorGUILayout.ToggleLeft("Generate Random Seed Each Time", genRandomSeedEachTime);

            EditorGUILayout.Space();
            
            if (GUILayout.Button("Generate Map"))
            {
                if(genRandomSeedEachTime){ MapParams.GenerateNewSeed(); }

                EditorUtility.DisplayProgressBar("Cave Generator", "Generating map...", 0);

                if(genPreviewTexture && genPreviewTextureOnly){ previewTexture = cave.GeneratePatternTextureOnly(MapPreset, colorCodedRooms); }
                else
                { 
                    var generatedMap = cave.GenerateCave(MapPreset);
                    if(genPreviewTexture) { generatedMap.ApplyMapToTexture2D(ref previewTexture, colorCodedRooms); }
                }
                
                EditorUtility.ClearProgressBar();
            }
        }

        private void DrawSaveControls()
        {
            if (IsPresetInstance())
            {
                saveName = EditorGUILayout.TextField("Save As File Name:", saveName);

                if (GUILayout.Button("Save Preset Instance To File"))
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    if (MapPresetSaveManager.PresetNameExists(saveName))
                    {
                        if (!EditorUtility.DisplayDialog("Save Map Preset", "Preset Map of this name already exists!", "Overwrite", "Cancel"))
                        {
                            return;
                        }
                    }

                    var mapPresetAsset = MapPresetSaveManager.SavePresetInstanceToAsset(MapPreset, saveName, true);
                    cave.mapPreset = mapPresetAsset;
                    MarkDirty();
                }
            }
            else
            {
                EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField("Currently editing Preset asset: ");

                    EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.ObjectField(obj:MapPreset, objType: typeof(MapPresetSO), allowSceneObjects: false);
                    EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Create New Preset Instance"))
                {
                    CreateNewPresetInstance();
                    MarkDirty();
                }
            }
        }
        
        private void DrawPatternTexture(Texture2D pattern)
        {
            EditorGUILayout.LabelField("", MapEditorGUIStyles.ThinLineHorizontal);
            EditorGUILayout.LabelField(".:Minimap Preview:.", MapEditorGUIStyles.LabelUpperCenter);
            if (pattern == null) { return; }

            Rect drawArea = GUILayoutUtility.GetRect(32, 600, 32, 600);
            EditorGUI.DrawPreviewTexture(drawArea, pattern, null, ScaleMode.ScaleToFit);
            EditorGUILayout.Space();
        }

        private bool CanDrawControls()
        {
            if(!IsCaveAssigned()) { return false; }
            EnsurePresetExists();
            
            return true;
        }

        private void CreateNewPresetInstance()
        {
            cave.mapPreset = CreateInstance<MapPresetSO>();
            EnsurePresetExists();
            saveName = MapPresetSaveManager.GetUniqueSaveName(saveName);
            MapPreset.mapName = saveName;
            preset.ApplyModifiedProperties();
        }

        private void CreateSerializedProps(MapPresetSO preset)
        {
            this.preset = new SerializedObject(preset);
            presetEditor = Editor.CreateEditor(preset);
        }

        private void EnsurePresetExists()
        {
            if (preset == null || preset.targetObject == null || preset.targetObject != cave.mapPreset)
            {
                if (cave.mapPreset == null) { CreateNewPresetInstance(); }
                CreateSerializedProps(cave.mapPreset);
            }
        }

        private bool IsCaveAssigned()
        {
            if (cave == null)
            {
                EditorGUILayout.LabelField("No Cave object assigned! Can't edit.", MapEditorGUIStyles.LabelUpperCenter);
                return false;
            }

            return true;
        }

        private bool IsPresetInstance() => !AssetDatabase.IsMainAsset(MapPreset);
        
        private void MarkDirty()
        {
            EditorUtility.SetDirty(cave);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        public void SetCaveContext(Cave cave)
        {
            this.cave = cave;
            EnsurePresetExists();
        }

        public static void ShowWindow(Cave cave)
        {
            var window = GetWindow<CaveEditorWindow>("LLamaZOO Cave Generator", true);
            window.SetCaveContext(cave);
            window.minSize = new Vector2(300, 500);
        }
    }
}
