using System.Collections.Generic;
using UnityEngine;

namespace SEGSRuntime
{
    public enum eBlendMode : int
    {
        MULTIPLY = 0,
        MULTIPLY_REG = 1,
        COLORBLEND_DUAL = 2,
        ADDGLOW = 3,
        ALPHADETAIL = 4,
        BUMPMAP_MULTIPLY = 5,
        BUMPMAP_COLORBLEND_DUAL = 6,
        INVALID = ~0 // all bits set, should be properly sized to underlying type
    };

    [System.Serializable]
    public class BoneAnimTrack
    {
        public Quaternion[] rot_keys;
        public Vector3[] pos_keys;
        public ushort rotation_ticks;
        public ushort position_ticks;
        public byte tgtBoneOrTexId;
    };

    [System.Serializable]
    public class TextureAnim_Data2
    {
        public BoneAnimTrack animtrack1;
        public BoneAnimTrack animtrack2;
        public string scrollType;
        public float speed;
        public float stScale;
        public int flags;
    }
    internal class TextureAnim_Dat : TextureAnim_Data2
    {
        public bool loadFrom(BinStore s)
        {
            bool ok = true;
            s.prepare();
            ok &= s.read(out speed);
            ok &= s.read(out stScale);
            ok &= s.read(out scrollType);
            ok &= s.read(out flags);
            ok &= s.prepare_nested(); // will update the file size left
            Debug.Assert(s.end_encountered());
            return ok;
        }
    };

    internal struct ColorList
    {
        //public Color32 field_0[16];
        public int count;
        public float scale;
    };

    [System.Serializable]
    public class GeometryModifiersData
    {
        public string src_name;
        public string name;
        public ModelModifiers node = new ModelModifiers();
        public int GfxFlags;
        public uint ObjFlags;
        public uint GroupFlags;
        public eBlendMode blend_mode;
        public float LodNear;
        public float LodFar;
        public float LodNearFade;
        public float LodFarFade;
        public List<TextureAnim_Data2> StAnim = new List<TextureAnim_Data2>();
        public Vector2 FogDist;
        public float ShadowDist;
        public float AlphaRef;
        public float ObjTexBias;
        public Vector2 NightGlow;
        public float Sway;

        public float Sway_Rotate;

        //public ColorList clists_1;
        //public ColorList clists_2;
        public float LodScale;
    }
    internal class GeometryModifiers : GeometryModifiersData
    {

        public bool loadFrom(BinStore s)
        {
            bool ok = true;
            s.prepare();
            ok &= s.read(out name);
            ok &= s.read(out src_name);
            ok &= s.read(out LodFar);
            ok &= s.read(out LodFarFade);
            ok &= s.read(out LodNear);
            ok &= s.read(out LodNearFade);
            uint tflag;
            ok &= s.read(out tflag);
            node._TrickFlags = (TrickFlags) tflag;
            ok &= s.read(out ObjFlags);
            ok &= s.read(out GfxFlags);
            ok &= s.read(out GroupFlags);
            ok &= s.read(out Sway);
            ok &= s.read(out Sway_Rotate);
            ok &= s.read(out AlphaRef);
            ok &= s.read(out FogDist);
            ok &= s.read(out node.SortBias);
            ok &= s.read(out node.ScrollST0);
            ok &= s.read(out node.ScrollST1);
            ok &= s.read(out ShadowDist);
            ok &= s.read(out NightGlow);
            ok &= s.read(out node.TintColor0);
            ok &= s.read(out node.TintColor1);
            ok &= s.read(out ObjTexBias);
            ok &= s.prepare_nested(); // will update the file size left
            if (s.end_encountered())
                return ok;
            string _name;
            while (s.nesting_name(out _name))
            {
                s.nest_in();
                if ("StAnim" == _name)
                {
                    TextureAnim_Dat entry = new TextureAnim_Dat();
                    ok &= entry.loadFrom(s);
                    StAnim.Add(entry);
                }
                else
                    Debug.Assert(false, "unknown field referenced.");

                s.nest_out();
            }

            Debug.Assert(s.end_encountered());
            return ok;
        }
    };
}
