using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LlamaZOO.MitchZais.CaveGenerator
{
    [System.Serializable]
    public class MapParams
    {
        public int seed;
        public int width, height;
        public int refinementSteps;
        public float density;
        
        public void GenerateNewSeed()
        {
            this.seed = Random.Range(int.MinValue, int.MaxValue);
        }

        public MapParams(int seed, int width, int height, float density, int refinementSteps)
        {
            this.seed = seed;
            this.width = width;
            this.height = height;
            this.density = density;
            this.refinementSteps = refinementSteps;
        }

        public MapParams()
        {
            this.seed = System.Environment.TickCount; //Random.Range can't be called from MonoBehaviour constructor so this is used as a default
            this.width = 128;
            this.height = 128;
            this.density = 0.58f;
            this.refinementSteps = 3;
        }
    }
}