using System;
using System.Collections.Generic;
using UnityEngine;

namespace SEGSRuntime
{
    internal class SceneGraphNode_Data
    {
        [Flags]
        public enum NFlags
        {
            Ungroupable = 1,
            FadeNode = 2,
        };

        public string name;
        public string p_Obj;
        public string type;
        public int flags;
        public List<GroupLoc_Data> p_Grp = new List<GroupLoc_Data>();
        public List<GroupProperty_Data> p_Property = new List<GroupProperty_Data>();
        public List<TintColor_Data> p_TintColor = new List<TintColor_Data>();
        public List<DefSound_Data> p_Sound = new List<DefSound_Data>();
        public List<ReplaceTex_Data> p_ReplaceTex = new List<ReplaceTex_Data>();
        public List<DefOmni_Data> p_Omni = new List<DefOmni_Data>();
        public List<DefBeacon_Data> p_Beacon = new List<DefBeacon_Data>();
        public List<DefFog_Data> p_Fog = new List<DefFog_Data>();
        public List<DefAmbient_Data> p_Ambient = new List<DefAmbient_Data>();
        public List<DefLod_Data> p_Lod = new List<DefLod_Data>();

        private bool loadSub<T>(BinStore s, List<T> tgt) where T : IBinLoadable, new()
        {
            T entry = new T();
            bool ok = entry.loadFrom(s);
            tgt.Add(entry);
            return ok;
        }

        public void postprocessNodeFlags(SceneNode node)
        {
            if ((flags & (int) NFlags.Ungroupable) != 0)
            {
                //        node->is_ungroupable = 1; // only useful for editing
            }

            if ((flags & (int) NFlags.FadeNode) != 0)
            {
                node.is_LOD_fade_node = 1;
            }
        }

        public bool loadFrom(BinStore s)
        {
            bool ok = true;
            s.prepare();
            ok &= s.read(out name);
            ok &= s.read(out type);
            ok &= s.read(out flags);
            ok &= s.read(out p_Obj);
            ok &= s.prepare_nested(); // will update the file size left
            Debug.Assert(ok);
            if (s.end_encountered())
                return ok;
            string _name;
            while (s.nesting_name(out _name))
            {
                s.nest_in();
                if ("Group" == _name)
                {
                    ok &= loadSub(s, p_Grp);
                }
                else if ("Property" == _name)
                {
                    ok &= loadSub(s, p_Property);
                }
                else if ("TintColor" == _name)
                {
                    ok &= loadSub(s, p_TintColor);
                }
                else if ("Ambient" == _name)
                {
                    ok &= loadSub(s, p_Ambient);
                }
                else if ("Omni" == _name)
                {
                    ok &= loadSub(s, p_Omni);
                }
                else if ("Sound" == _name)
                {
                    ok &= loadSub(s, p_Sound);
                }
                else if ("ReplaceTex" == _name)
                {
                    ok &= loadSub(s, p_ReplaceTex);
                }
                else if ("Beacon" == _name)
                {
                    ok &= loadSub(s, p_Beacon);
                }
                else if ("Fog" == _name)
                {
                    ok &= loadSub(s, p_Fog);
                }
                else if ("Lod" == _name)
                {
                    ok &= loadSub(s, p_Lod);
                }
                else
                    Debug.Assert(false, "unknown field referenced.");

                s.nest_out();
            }

            return ok;
        }

        public bool addNode(LoadingContext ctx, PrefabStore prefabs, string path)
        {
            if (this.p_Grp.Count == 0 && String.IsNullOrEmpty(this.p_Obj))
                return false;

            string obj_path = ctx.groupRename(this.name, true);
            SceneNode node = ctx.m_target.getNodeByName(obj_path);
            if (null == node)
            {
                node = ctx.m_target.newDef(ctx.nesting_level);
                node.m_src_bin = path;
                if (0 != this.p_Property.Count)
                    node.m_properties = new List<GroupProperty_Data2>(this.p_Property);
            }

            if (this.p_Obj.Length != 0)
            {
                node.m_model = prefabs.groupModelFind(this.p_Obj);
                if (null == node.m_model)
                {
                    Debug.LogError("Cannot find root geometry in " + this.p_Obj);
                }

                node.groupApplyModifiers();
            }

            node.setNodeNameAndPath(ctx.m_target, obj_path);
            node.addChildNodes(this, ctx, prefabs);

            if (node.m_children.Count == 0 && null == node.m_model)
            {
                Debug.LogFormat("Should delete def {0} after conversion it has no children, nor models", name);
                return false;
            }

            postprocessNodeFlags(node);
            postprocessLOD(node);
            postprocessTextureReplacers(node);
            postprocessTintColor(node);
            postprocessAmbient(node);
            postprocessFog(node);
            postprocessEditorBeacon( node);
            postprocessSound(node);
            postprocessLight(node);

            node.nodeCalculateBounds();
            node.nodeSetVisBounds();
            return true;
        }

        private void postprocessTextureReplacers(SceneNode node)
        {
            //TODO: This needs pretty urgent attention, MapViewer does not handle this at all
            // and it's a pretty important piece of the puzzle.
            foreach (ReplaceTex_Data texData in p_ReplaceTex)
            {
                Debug.Log("Texture to Replace: " + texData.repl_with);
                // HInstanceMod tr = InstanceModStorage::instance().create();
                // tr->addTextureReplacement(tex_repl.texUnit,tex_repl.repl_with);
            }
        }

        private void postprocessLOD(SceneNode node)
        {
            if (p_Lod.Count == 0)
                return;

            DefLod_Data lod_data = p_Lod[0];
            node.lod_scale = lod_data.Scale;

            if (node.lod_fromtrick)
                return;

            node.lod_far = lod_data.Far;
            node.lod_far_fade = lod_data.FarFade;
            node.lod_near = lod_data.Near;
            node.lod_near_fade = lod_data.NearFade;
        }

        void postprocessTintColor(SceneNode node)
        {
            //TODO: only 1 tint is used here, either change the source structure or consider how multi-tint would work ?
            if (p_TintColor.Count == 0)
                return;
            TintColor_Data tint_data = p_TintColor[0];
            //TODO: MapViewer does not handle this case
            //Nodes with a tint set could use that value, if proper ModelModifiers flag was set.
            //ColorOnly models would use first tint color
            //DistAlpha would use alpha from first tint color
            //SetColor would set the blend colors to tint values
        }

        void postprocessAmbient(SceneNode node)
        {
            //TODO: only one value is used here, either change the source structure or consider how multi-ambient would work ?
            if (p_Ambient.Count == 0)
                return;

            DefAmbient_Data light_data = p_Ambient[0];
            //NOTE: original engine used the same value for first fog color and ambient light!
            //TODO: MapViewer does not handle this info yet
            // HAmbientLight l = AmbientLightStorage::instance().create()
            // l->color = light_data.clr;
        }

        void postprocessFog(SceneNode node)
        {
            //TODO: only 1 fog value is used here, either change the source structure or consider how multi-fog would work ?
            if (p_Fog.Count == 0)
                return;

            DefFog_Data fog_data = p_Fog[0];
            //TODO: MapViewer does not handle this info yet
            // HFog f = FogInfoStorage::instance().create()
            // f->color_1 = fog_data.fogClr1;
            // f->color_2 = fog_data.fogClr2;
            // f->radius = fog_data.fogZ;
            // f->near = fog_data.fogX;
            // f->far = fog_data.fogY;
        }

        void postprocessEditorBeacon(SceneNode node)
        {
            if (p_Beacon.Count == 0)
                return;
            // mostly markers like TrafficBeacon/CombatBeacon/BasicBeacon
            DefBeacon_Data bcn = p_Beacon[0];
//TODO: consider if we want to allow the use of editor beacons ?
//        HBeacon b = BeaconStorage::instance().create();
//        b->name = bcn.name;
//        b->radius = bcn.amplitude;
//        node->m_editor_beacon=b;
        }

        void postprocessSound(SceneNode node)
        {
            if (p_Sound.Count == 0)
                return;
            //TODO: consider multiple sounds per-node to reduce node spam ?
            Debug.Assert(p_Sound.Count == 1);
            DefSound_Data snd = p_Sound[0];
            SoundInfo handle = new SoundInfo();
            handle.name = snd.name;
            handle.radius = snd.sndRadius; 
            handle.ramp_feet = snd.snd_ramp_feet;
            handle.flags = (ushort) snd.sndFlags;
            node.sound_info = handle;
        }

        void postprocessLight(SceneNode node)
        {
            if (p_Omni.Count == 0)
                return;
            DefOmni_Data omnid = p_Omni[0];
            node.m_light = new LightProperties
            {
                color = omnid.omniColor,
                range = omnid.Size,
                is_negative = omnid.isNegative!=0
            };
        }
    };
}