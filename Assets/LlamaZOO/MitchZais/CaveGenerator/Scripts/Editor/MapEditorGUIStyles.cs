using UnityEngine;

namespace LlamaZOO.MitchZais.CaveGeneratorEditor
{
    public static class MapEditorGUIStyles
    {
        public static readonly GUIStyle LabelUpperCenter;
        public static readonly GUIStyle LabelLowerCenter;
        public static readonly GUIStyle ButtonCentered;
        public static readonly GUIStyle ThinLineHorizontal;
        public static readonly GUIStyle PatternTexture;
        static MapEditorGUIStyles()
        {
            LabelUpperCenter = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter };
            LabelLowerCenter = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, padding = new RectOffset(0, 0, 0, -8)  };
            ButtonCentered = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.UpperCenter, padding = new RectOffset(0, 0, 0, 0) };

            ThinLineHorizontal = new GUIStyle(GUI.skin.horizontalSlider)
            {
                alignment = TextAnchor.UpperCenter,
                clipping = TextClipping.Clip,
                border = new RectOffset(-9999, -9999, 0, 0)
            };

            PatternTexture = new GUIStyle(GUI.skin.box)
            {

         
 
               stretchWidth = true, stretchHeight = true
               
            };
        }
    }
}