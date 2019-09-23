using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LlamaZOO.MitchZais.CaveGenerator
{
    [CreateAssetMenu(fileName = "Cave Preset", menuName = "LLamaZoo/LlamaZoo/MitchZais/Create Cave Generation Preset", order = 100)]
    public class MapPresetSO : ScriptableObject
    {
        public string mapName = "New Map";
        [SerializeField] public long creationDateUTC;
        [SerializeField] private MapParams mapParams;
        
        public MapParams MapParams { get { return mapParams; } private set { mapParams = value; } }

        public Texture2D minimap;
        public Material groundMaterial;
        public Material wallMaterial;
        public Material capMaterial;

        public MapPresetSO()
        {
            creationDateUTC = System.DateTime.UtcNow.ToFileTimeUtc();
        }
    }
}