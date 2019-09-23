# 3D Cave Generator
3D procedural cave generation system based on cellular automata, done for a LlamaZOO test.
-Ensures connectivity of all rooms and culling/mergin of rooms and walls under a user-defined area size.
-Generates a mesh comprisong of a ceiling, floor, pattern-cap and wall meshes segmented into regions for collision efficiency.
-Generates texture of the pattern for Mini-Map usage.

# Usage:

-Add "Cave" component to a GameObject in the scene.

-This component can be slotted with a preset ScriptableObject that contains user customized generation parameters.

-Whether a preset is assign or not, an editor can be launched by clicking "Open Cave Editor" on the inspector of this component.

-If no preset is present, an instance of one will be generated for you.

-Customize parameters as you like and click "Generate Map".<br/>
  --The mesh will appear generated in the scene and a texture of the map will be shown at the bottom of the editor window.

-The preset can be saved to an asset file to preserve it outside of the Cave component and be slotted into any other Cave component to produce that cave when regenerated.<br/>
  --(Saved preset will also display its minimap texture to let the user know what it looked like)
