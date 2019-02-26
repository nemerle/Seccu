using System;
using UnityEngine;

namespace SEGSRuntime
{
    [Flags]
    public enum TrickFlags : uint
    {
        Additive    =           0x1,
        ScrollST0   =           0x2,
        FrontFace   =           0x4,
        CameraFace  =           0x8,
        DistAlpha   =          0x10,
        ColorOnly   =          0x20,
        DoubleSided =          0x40,
        NoZTest     =          0x80,
        ReflectTex1 =         0x100,
        ScrollST1   =         0x200,
        NightLight  =         0x400,
        NoZWrite    =         0x800,
        Wireframe   =        0x1000,
        NoDraw      =        0x2000,
        STAnimate   =        0x4000,
        ParticleSys =        0x8000,
        NoColl      =       0x10000,
        SetColor    =       0x20000,
        VertexAlpha =       0x40000,
        NoFog       =       0x80000,
        FogHasStartAndEnd= 0x100000,
        EditorVisible =    0x200000,
        CastShadow  =      0x400000,
        LightFace   =      0x800000,
        ReflectTex0 =     0x1000000,
        AlphaRef    =     0x2000000,
        SimpleAlphaSort = 0x4000000,
        TexBias     =     0x8000000,
        NightGlow   =    0x10000000,
        SelectOnly  =    0x20000000,
        STSScale    =    0x40000000,
        NotSelectable =  0x80000000,
    };
    public class ModelModifiers
    {
        [SerializeField] public Vector2 ScrollST0;
        [SerializeField] public Vector2 ScrollST1;
        [SerializeField] public Vector2 tex_scale;
        [SerializeField] public Color32 TintColor0;
        [SerializeField] public Color32 TintColor1;
        [SerializeField] public TrickFlags _TrickFlags=0;
        [SerializeField] public float SortBias=0;
        public GeometryModifiersData info=null;
        public ModelModifiers clone()
        {
            return (ModelModifiers)this.MemberwiseClone();
        }
    };
}