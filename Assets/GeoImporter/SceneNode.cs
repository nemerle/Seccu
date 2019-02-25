using System;
using System.Collections.Generic;
using UnityEngine;

namespace SEGSRuntime
{
    internal class SceneNode
    {
        public SceneNode(int nest_level)
        {
            is_LOD_fade_node = 0;
            m_nest_level = nest_level;
        }

        GeoStoreDef m_belongs_to_geoset = null;
        public List<SceneNodeChildTransform> m_children=new List<SceneNodeChildTransform>();
        public List<GroupProperty_Data> m_properties = null;

        public LightProperties m_light;
        public SoundInfo sound_info;
        public Model m_model = null;
        public GeoStoreDef m_geoset_info = null; // where is this node from ?
        public string m_name;
        public string m_dir;
        public string m_src_bin;
        public Bounds m_bbox;
        public int m_index_in_scenegraph = 0;
        public int m_nest_level;

        public Vector3 m_center;
        public float radius = 0;
        public float vis_dist = 0;
        public float lod_near = 0;
        public float lod_far = 0;
        public float lod_near_fade = 0;
        public float lod_far_fade = 0;
        public float lod_scale = 0;

        public float shadow_dist = 0;

        //HandleT<20,12,struct SoundInfo> sound_info;
        public bool lod_fromtrick = false;

        // Start of bit flags
        public uint is_LOD_fade_node;
        public uint shell;
        public bool tray;
        public bool region_marker;
        public bool volume_trigger;
        public bool water_volume;
        public bool lava_volume;
        public bool sewer_volume;
        public bool door_volume;
        public bool in_use;
        public bool parent_fade;

        public bool key_light;

        // end of bit flags
        public int m_bone_slot = 0;

        public byte boneId()
        {
            return (byte) (m_bone_slot & 0xFF);
        }

        public void groupApplyModifiers()
        {
            RuntimeData rd = RuntimeData.get();

            Model model = m_model;
            if (null == model)
                return;
            GeometryModifiersData mods = rd.m_modifiers.findGeomModifier(model.name, m_dir);
            if (null == mods)
                return;

            if (mods.LodNear != 0.0f)
                lod_near = mods.LodNear;
            if (mods.LodFar != 0.0f)
                lod_far = mods.LodFar;
            if (mods.LodNearFade != 0.0f)
                lod_near_fade = mods.LodNearFade;
            if (mods.LodFarFade != 0.0f)
                lod_far_fade = mods.LodFarFade;
            if (mods.LodScale != 0.0f)
                lod_scale = mods.LodScale;

            GroupFlags v1 = (GroupFlags) mods.GroupFlags;
            shadow_dist = mods.ShadowDist;
            parent_fade = v1.HasFlag(GroupFlags.ParentFade);
            region_marker = v1.HasFlag(GroupFlags.RegionMarker);
            volume_trigger = v1.HasFlag(GroupFlags.VolumeTrigger);
            water_volume = v1.HasFlag(GroupFlags.WaterVolume);
            lava_volume = v1.HasFlag(GroupFlags.LavaVolume);
            sewer_volume = v1.HasFlag(GroupFlags.SewerWaterVolume);
            door_volume = v1.HasFlag(GroupFlags.DoorVolume);
            key_light = v1.HasFlag(GroupFlags.KeyLight);
            tray = v1.HasFlag(GroupFlags.VisTray) | v1.HasFlag(GroupFlags.VisOutside);

            if (mods.LodNear != 0.0f || mods.LodFar != 0.0f || mods.LodNearFade != 0.0f || mods.LodFarFade != 0.0f ||
                mods.LodScale != 0.0f)
                lod_fromtrick = true;
            if (mods.node._TrickFlags.HasFlag(TrickFlags.NoColl))
                ; //TODO: disable collisions for this node
            if (mods.node._TrickFlags.HasFlag(TrickFlags.SelectOnly))
                ; // set the model's triangles as only selectable ?? ( selection mesh ? )
            if (mods.node._TrickFlags.HasFlag(TrickFlags.NotSelectable))
                ; //
        }

        public void setNodeNameAndPath(SceneGraph scene, string obj_path)
        {
            string result = "";
            int strlenobjec = "object_library".Length;
            if (obj_path.StartsWith("object_library", StringComparison.OrdinalIgnoreCase))
                obj_path = obj_path.Remove(0, strlenobjec + 1);
            if (Tools.groupInLibSub(obj_path))
                result = "object_library/";

            result += obj_path;
            int last_separator = result.LastIndexOf('/');
            string key = result.Substring(last_separator + 1);
            string lowkey = key.ToLower();

            if (!scene.name_to_node.ContainsKey(lowkey))
            {
                scene.name_to_node[lowkey] = this;
            }

            m_name = key;
            m_dir = "";

            if (last_separator != -1)
                m_dir = result.Substring(0, last_separator);
        }

        public void addChildNodes(SceneGraphNode_Data inp_data, LoadingContext ctx, PrefabStore store)
        {
            if (inp_data.p_Grp == null || inp_data.p_Grp.Count == 0)
                return;

            foreach (GroupLoc_Data dat in inp_data.p_Grp)
            {
                string new_name = ctx.groupRename(dat.name, false);
                SceneNodeChildTransform child = new SceneNodeChildTransform();
                child.node = ctx.m_target.getNodeByName(new_name);
                if (null == child.node)
                {
                    bool loaded = store.loadNamedPrefab(new_name, ctx);
                    if (!loaded)
                        Debug.LogError("Cannot load named prefab " + new_name + " result is " + loaded);
                    child.node = ctx.m_target.getNodeByName(new_name);
                }

                // construct from euler angles
                Quaternion qPitch = UnityEngine.Quaternion.AngleAxis(Mathf.Rad2Deg*dat.rot.x, new Vector3(-1, 0, 0));
                Quaternion qYaw = UnityEngine.Quaternion.AngleAxis(Mathf.Rad2Deg*dat.rot.y, new Vector3(0, 1, 0));
                Quaternion qRoll = UnityEngine.Quaternion.AngleAxis(Mathf.Rad2Deg*dat.rot.z, new Vector3(0, 0, 1));
                Quaternion rotQuat = qYaw * qPitch * qRoll;
                child.m_matrix2 = Matrix4x4.TRS(dat.pos, rotQuat, new Vector3(1, 1, 1));
                if (null != child.node)
                    m_children.Add(child);
                else
                {
                    Debug.LogError("Node " + m_name + "\ncan't find member " + dat.name);
                }
            }
        }

        public bool nodeCalculateBounds()
        {
            float geometry_radius=0.0f;
            float maxrad=0.0f;
            Model model;
            Bounds bbox=new Bounds();
            bool set = false;

            if( null!=m_model )
            {
                model = this.m_model;
                bbox.Encapsulate(model.box);
                                  
                geometry_radius = bbox.size.magnitude * 0.5f;
                set = true;
            }

            foreach( SceneNodeChildTransform child in this.m_children )
            {
                Vector3 dst = child.m_matrix2.MultiplyPoint(child.node.m_center);
                Vector3 v_radius = new Vector3(child.node.radius,child.node.radius,child.node.radius);
                bbox.Encapsulate(dst+v_radius);
                bbox.Encapsulate(dst-v_radius);
                set = true;
            }

            if( !set )
            {
                bbox = new Bounds();
            }
            radius = bbox.size.magnitude * 0.5f;
            m_bbox = bbox;
            m_center = bbox.center; // center
            foreach ( SceneNodeChildTransform child2 in this.m_children )
            {
                Vector3 toChildCenter = child2.m_matrix2.MultiplyPoint(child2.node.m_center) - this.m_center;
                float r = toChildCenter.magnitude + child2.node.radius;
                maxrad = Math.Max(maxrad,r);
            }

            if( maxrad != 0.0f )
                radius = maxrad;

            radius = Math.Max(geometry_radius,this.radius);
            return this.radius == 0.0f && (this.m_children.Count!=0);
            
        }

        public void nodeSetVisBounds()
        {
            //TODO: fix this
            Vector3 dv;
            float maxrad = 0.0f;
            float maxvis = 0.0f;

            if( lod_scale == 0.0f )
                lod_scale = 1.0f;

            if( m_model!=null )
            {
                Model model = m_model;
                dv = model.box.size;
                maxrad = dv.magnitude * 0.5f + shadow_dist;
                if( lod_far == 0.0f )
                {
                    lod_far = (maxrad + 10.0f) * 10.0f;
                    lod_far_fade = lod_far * 0.25f;
                    // lod_far is autogenned here
                }
                maxvis = lod_far + lod_far_fade;
            }

            foreach(SceneNodeChildTransform entr in m_children)
            {
                dv = entr.m_matrix2.MultiplyPoint(entr.node.m_center);
                dv -= m_center;
                maxrad = Math.Max(maxrad,dv.magnitude + entr.node.radius + entr.node.shadow_dist);
                maxvis = Math.Max(maxvis,dv.magnitude + entr.node.vis_dist * entr.node.lod_scale);
            }

            if( shadow_dist == 0.0f )
                shadow_dist = maxrad - radius;

            vis_dist = maxvis;
            
        }
    };
}