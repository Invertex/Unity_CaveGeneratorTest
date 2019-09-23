using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using LlamaZOO.MitchZais.CaveGenerator;

namespace LlamaZOO.MitchZais.CaveGeneratorEditor
{
    [CustomEditor(typeof(Cave))]
    public class CaveInspector : Editor
    {
        SerializedProperty mapPreset;
        [SerializeField] private bool showPresetDetails = false;
        private void OnEnable()
        {  
            mapPreset = serializedObject.FindProperty("mapPreset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
                EditorGUILayout.ObjectField(mapPreset, label: new GUIContent("Map Preset"), objType: typeof(MapPresetSO));
            if(EditorGUI.EndChangeCheck()) { serializedObject.ApplyModifiedProperties(); }
            
            if(mapPreset.objectReferenceValue == null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Cave Preset not assigned, assign one or generate with editor.");
            }
            else
            {
                showPresetDetails = EditorGUILayout.Foldout(showPresetDetails, new GUIContent("Preset Details"));
                if (showPresetDetails)
                {
                    var preset = mapPreset.objectReferenceValue as MapPresetSO;
                    var paramsEditor = Editor.CreateEditor(preset);
                    EditorGUI.BeginDisabledGroup(true);
                    paramsEditor.OnInspectorGUI();
                    EditorGUI.EndDisabledGroup();
                }
            }

            if (GUILayout.Button("Open Cave Editor"))
            {
                CaveEditorWindow.ShowWindow(target as Cave);
            }
        }
    }
}