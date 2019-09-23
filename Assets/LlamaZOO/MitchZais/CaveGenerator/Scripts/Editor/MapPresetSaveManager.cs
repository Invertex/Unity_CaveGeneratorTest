using System.IO;
using System.Collections.Generic;
using UnityEditor;
using LlamaZOO.MitchZais.CaveGenerator;

namespace LlamaZOO.MitchZais.CaveGeneratorEditor
{
    public static class MapPresetSaveManager
    {
        public static readonly string PresetSaveLocation = "Assets/LlamaZOO/MitchZais/CaveGenerator/PresetMaps/";

        /// <summary>
        /// Saves MapPreset from memory to an asset in StreamingAssets to be saved long terms and returns the asset reference.
        /// </summary>
        /// <param name="presetInstance"></param>
        /// <returns></returns>
        public static MapPresetSO SavePresetInstanceToAsset(MapPresetSO presetInstance, string saveName, bool overwriteIfConflict)
        {
            string saveLocation = SaveNameToPath(saveName);

            if (!PresetExistsAtPath(saveLocation) || overwriteIfConflict)
            {
                AssetDatabase.CreateAsset(presetInstance, saveLocation);
                AssetDatabase.SaveAssets();
            }

            return AssetDatabase.LoadAssetAtPath(saveLocation, typeof(MapPresetSO)) as MapPresetSO;
        }

        internal static string SaveNameToPath(string saveName) { return PresetSaveLocation + saveName + ".asset"; }

        internal static string GetUniqueSaveName(string saveName)
        {
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(SaveNameToPath(saveName));
            return Path.GetFileNameWithoutExtension(uniquePath);
        }

        internal static bool PresetNameExists(string saveName) => PresetExistsAtPath(SaveNameToPath(saveName));
        internal static bool PresetExistsAtPath(string path) => AssetDatabase.LoadMainAssetAtPath(path) != null;
        
    }
}