using System.Collections.Generic;
using UnityEngine;

namespace SEGSRuntime
{
    public class ModelNodeMods : MonoBehaviour
    {
        [SerializeField] public ModelModifiers ModelMod;
        [SerializeField] public List<TextureWrapper> TexWrappers;
        [SerializeField] public GeometryModifiersData GeomTricks;
    }
}