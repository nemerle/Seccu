using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SEGSRuntime
{
    [Flags]
    enum TexOpt : uint
    {
        FADE = 0x0001,
        DUAL = 0x0010,
        REPLACEABLE = 0x0800,
        BUMPMAP = 0x1000,
    };

    [Flags]
    enum GroupFlags : uint
    {
        VisOutside = 0x1,
        VisBlocker = 0x2,
        VisAngleBlocker = 0x4,
        VisTray = 0x8,
        VisShell = 0x10,
        VisWindow = 0x20,
        VisDoorFrame = 0x40,
        RegionMarker = 0x80,
        VolumeTrigger = 0x100,
        WaterVolume = 0x200,
        LavaVolume = 0x400,
        DoorVolume = 0x800,
        ParentFade = 0x1000,
        KeyLight = 0x2000,
        SewerWaterVolume = 0x4000,
        RedWaterVolume = 0x8000, // only in I2
        MaterialVolume = 0x10000, // only in I2
    };

    internal class TextureModifiers
    {
        public string src_file;
        public string name;
        public string Blend;
        public string BumpMap;
        public Vector2 Fade = new Vector2(0, 0);
        public Vector2 ScaleST0 = new Vector2(0, 0);
        public Vector2 ScaleST1 = new Vector2(0, 0);

        public uint Flags;
        public uint BlendType;
        public int surfaceBitIdx;
        public string Surface; // Name of this surface  WOOD METAL etc.
        public float Gloss;

        public bool loadFrom(BinStore s)
        {
            bool ok = true;
            s.prepare();
            ok &= s.read(out name);
            ok &= s.read(out src_file);
            ok &= s.read(out Gloss);
            ok &= s.read(out Surface);
            ok &= s.read(out Fade);
            ok &= s.read(out ScaleST0);
            ok &= s.read(out ScaleST1);
            ok &= s.read(out Blend);
            ok &= s.read(out BumpMap);
            ok &= s.read(out BlendType);
            ok &= s.read(out Flags);
            ok &= s.prepare_nested(); // will update the file size left
            Debug.Assert(s.end_encountered());
            return ok;
        }
    };

    internal class SceneModifiers
    {
        List<TextureModifiers> texture_mods = new List<TextureModifiers>();

        List<GeometryModifiersData> geometry_mods = new List<GeometryModifiersData>();

        // for every directory in the texture's path we can have a modifier.
        public Dictionary<string, TextureModifiers> m_texture_path_to_mod = new Dictionary<string, TextureModifiers>();
        Dictionary<string, GeometryModifiersData> g_tricks_string_hash_tab = new Dictionary<string, GeometryModifiersData>();

        public GeometryModifiersData findGeomModifier(string modelname, string trick_path)
        {
            string[] parts = modelname.Split(new string[] {"__"}, StringSplitOptions.None);
            if (parts.Length < 2)
                return null;
            List<string> elems = new List<string>(parts);
            elems.RemoveAt(0);
            string bone_trick_name = string.Join("__", elems);
            GeometryModifiersData result = null;
            g_tricks_string_hash_tab.TryGetValue(bone_trick_name.ToLower(), out result);
            if (result != null)
                return result;
            Debug.LogFormat("Can't find modifier for {0} {1} : {2}",trick_path,modelname,bone_trick_name.ToLower());
            return null;
        }

        public bool loadFrom(BinStore s)
        {
            s.prepare();
            bool ok = s.prepare_nested(); // will update the file size left
            if (s.end_encountered())
                return ok;
            string _name;
            while (s.nesting_name(out _name))
            {
                s.nest_in();
                if ("Trick" == _name)
                {
                    GeometryModifiers entry = new GeometryModifiers();
                    geometry_mods.Add(entry);
                    ok &= entry.loadFrom(s);
                }
                else if ("Texture" == _name)
                {
                    var entry = new TextureModifiers();
                    texture_mods.Add(entry);
                    ok &= entry.loadFrom(s);
                }
                else
                    Debug.Assert(false, "unknown field referenced.");

                s.nest_out();
            }
            Debug.LogFormat("Loaded {0} GeomMods and {1} TexMods",geometry_mods.Count,texture_mods.Count);
            Debug.Assert(ok);
            return ok;
        }

        private void setupTexOpt(TextureModifiers tmod)
        {
            if (tmod.ScaleST0.x == 0.0f)
                tmod.ScaleST0.x = 1.0f;
            if (tmod.ScaleST0.y == 0.0f)
                tmod.ScaleST0.y = 1.0f;
            if (tmod.ScaleST1.x == 0.0f)
                tmod.ScaleST1.x = 1.0f;
            if (tmod.ScaleST1.y == 0.0f)
                tmod.ScaleST1.y = 1.0f;
            if (tmod.Fade.x != 0.0f || tmod.Fade.y != 0.0f)
                tmod.Flags |= (uint) TexOpt.FADE;
            if (tmod.Blend.Length != 0)
                tmod.Flags |= (uint) TexOpt.DUAL;
            if (tmod.Surface.Length != 0)
            {
                //seqBitNameToIdx(tmod.Surface);
                //qCDebug(logSceneGraph) << "Has surface" << tex->Surface;
            }

            string initial_name = tmod.name;
            tmod.name = Path.GetFileNameWithoutExtension(tmod.name); // cut last extension part
            if (tmod.name.StartsWith("/"))
                tmod.name = tmod.name.Remove(0, 1);
            string lower_name = tmod.name.ToLower();
            if (m_texture_path_to_mod.ContainsKey(lower_name))
            {
                Debug.Log("Duplicate texture info: " + initial_name);
                return;
            }

            m_texture_path_to_mod[lower_name] = tmod;
        }

        public void trickLoadPostProcess()
        {
            m_texture_path_to_mod.Clear();
            g_tricks_string_hash_tab.Clear();
            foreach (TextureModifiers texopt in texture_mods)
                setupTexOpt(texopt);
            foreach (GeometryModifiers trickinfo in geometry_mods)
                setupTrick(trickinfo);
        }

        bool rgbAreZero(Color32 v)
        {
            return v.r == 0 && v.g == 0 && v.b == 0;
        }

        private void setupTrick(GeometryModifiers gmod)
        {
            if (rgbAreZero(gmod.node.TintColor0))
                gmod.node.TintColor0 = new Color32(255, 255, 255, 255);
            if (rgbAreZero(gmod.node.TintColor1))
                gmod.node.TintColor1 = new Color32(255, 255, 255, 255);
            gmod.AlphaRef /= 255.0f;
            if (gmod.ObjTexBias != 0.0f)
                gmod.node._TrickFlags |= TrickFlags.TexBias;
            if (gmod.AlphaRef != 0.0f)
                gmod.node._TrickFlags |= TrickFlags.AlphaRef;
            if (gmod.FogDist.x != 0.0f || gmod.FogDist.y != 0.0f)
                gmod.node._TrickFlags |= TrickFlags.FogHasStartAndEnd;
            if (gmod.ShadowDist != 0.0f)
                gmod.node._TrickFlags |= TrickFlags.CastShadow;
            if (gmod.NightGlow.x != 0.0f || gmod.NightGlow.y != 0.0f)
                gmod.node._TrickFlags |= TrickFlags.NightGlow;
            if (gmod.node.ScrollST0.x != 0.0f || gmod.node.ScrollST0.y != 0.0f)
                gmod.node._TrickFlags |= TrickFlags.ScrollST0;
            if (gmod.node.ScrollST1.x != 0.0f || gmod.node.ScrollST1.y != 0.0f)
                gmod.node._TrickFlags |= TrickFlags.ScrollST1;
            if (gmod.StAnim.Count != 0)
            {
                //        if(setStAnim(&a1->StAnim.front()))
                if (false)
                    gmod.node._TrickFlags |= TrickFlags.STAnimate;
            }

            if (((GroupFlags) gmod.GroupFlags & GroupFlags.VisTray) != 0)
                gmod.ObjFlags |= 0x400;
            if (gmod.name.Length == 0)
                Debug.Log("No name in trick");
            string lower_name = gmod.name.ToLower();
            if (g_tricks_string_hash_tab.ContainsKey(lower_name))
            {
                Debug.Log("duplicate model trick!");
                return;
            }
            g_tricks_string_hash_tab[lower_name] = gmod;
        }
    };
}
