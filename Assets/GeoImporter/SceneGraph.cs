using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using SEGSRuntime;
using UnityEditor.MemoryProfiler;
using UnityEngine;

namespace SEGSRuntime
{
    internal interface IBinLoadable
    {
        bool loadFrom(BinStore s);
    }

    internal class GroupLoc_Data : IBinLoadable
    {
        public string name;
        public Vector3 pos = new Vector3(0, 0, 0);
        public Vector3 rot = new Vector3(0, 0, 0);

        public bool loadFrom(BinStore s)
        {
            bool ok = true;
            s.prepare();
            ok &= s.read(out name);
            ok &= s.read(out pos);
            ok &= s.read(out rot);
            ok &= s.prepare_nested();
            Debug.Assert(ok);
            Debug.Assert(s.end_encountered());
            return ok;
        }
    };
        [Serializable]
    public class GroupProperty_Data2
    {
        public string propName;
        public string propValue;
        public int propertyType; // 1 - propValue contains float radius, 0 propValue is plain string
    };
    [Serializable]
    internal class GroupProperty_Data : GroupProperty_Data2,IBinLoadable
    {
        public bool loadFrom(BinStore s)
        {
            bool ok = true;
            s.prepare();
            ok &= s.read(out propName);
            ok &= s.read(out propValue);
            ok &= s.read(out propertyType);
            ok &= s.prepare_nested();
            Debug.Assert(ok);
            Debug.Assert(s.end_encountered());
            return ok;
        }
    };

    internal class TintColor_Data : IBinLoadable
    {
        public uint clr1;
        public uint clr2;

        public bool loadFrom(BinStore s)
        {
            bool ok = true;
            s.prepare();
            ok &= s.read(out clr1);
            ok &= s.read(out clr2);
            ok &= s.prepare_nested();
            Debug.Assert(ok);
            Debug.Assert(s.end_encountered());
            return ok;
        }
    };

    internal class ReplaceTex_Data : IBinLoadable
    {
        public int texIdxToReplace;
        public string repl_with;

        public bool loadFrom(BinStore s)
        {
            bool ok = true;
            s.prepare();
            ok &= s.read(out texIdxToReplace);
            ok &= s.read(out repl_with);
            ok &= s.prepare_nested();
            Debug.Assert(ok);
            Debug.Assert(s.end_encountered());
            return ok;
        }
    };

    internal class DefSound_Data : IBinLoadable
    {
        public string name;
        public float volRel1;
        public float sndRadius;
        public float snd_ramp_feet;
        public uint sndFlags;

        public bool loadFrom(BinStore s)
        {
            bool ok = true;
            s.prepare();
            ok &= s.read(out name);
            ok &= s.read(out volRel1);
            ok &= s.read(out sndRadius);
            ok &= s.read(out snd_ramp_feet);
            ok &= s.read(out sndFlags);
            ok &= s.prepare_nested();
            Debug.Assert(ok);
            Debug.Assert(s.end_encountered());
            return ok;
        }
    };

    internal class DefLod_Data : IBinLoadable
    {
        public float Far;
        public float FarFade;
        public float Near;
        public float NearFade;
        public float Scale;

        public bool loadFrom(BinStore s)
        {
            bool ok = true;
            s.prepare();
            ok &= s.read(out Far);
            ok &= s.read(out FarFade);
            ok &= s.read(out Near);
            ok &= s.read(out NearFade);
            ok &= s.read(out Scale);
            ok &= s.prepare_nested();
            Debug.Assert(ok);
            Debug.Assert(s.end_encountered());
            return ok;
        }
    };

    internal class DefOmni_Data : IBinLoadable
    {
        public Color32 omniColor;
        public float Size;
        public int isNegative;

        public bool loadFrom(BinStore s)
        {
            bool ok = true;
            s.prepare();
            uint color_data;
            ok &= s.read(out color_data);
            ok &= s.read(out Size);
            ok &= s.read(out isNegative);
            ok &= s.prepare_nested();
            Debug.Assert(ok);
            Debug.Assert(s.end_encountered());
            omniColor = Tools.RGBAToColor32(color_data);
            return ok;
        }
    };

    internal class DefBeacon_Data : IBinLoadable
    {
        public string name;
        public float amplitude; // maybe rotation speed ?

        public bool loadFrom(BinStore s)
        {
            bool ok = true;
            s.prepare();
            ok &= s.read(out name);
            ok &= s.read(out amplitude);
            ok &= s.prepare_nested();
            Debug.Assert(ok);
            Debug.Assert(s.end_encountered());
            return ok;
        }
    };

    internal class DefFog_Data : IBinLoadable
    {
        float fogZ;
        float fogX;
        float fogY;
        uint fogClr1;
        uint fogClr2;

        public bool loadFrom(BinStore s)
        {
            bool ok = true;
            s.prepare();
            ok &= s.read(out fogZ);
            ok &= s.read(out fogX);
            ok &= s.read(out fogY);
            ok &= s.read(out fogClr1);
            ok &= s.read(out fogClr2);
            ok &= s.prepare_nested();
            Debug.Assert(ok);
            Debug.Assert(s.end_encountered());
            return ok;
        }
    };

    internal class DefAmbient_Data : IBinLoadable
    {
        uint clr;

        public bool loadFrom(BinStore s)
        {
            bool ok = true;
            s.prepare();
            ok &= s.read(out clr);
            ok &= s.prepare_nested();
            Debug.Assert(ok);
            Debug.Assert(s.end_encountered());
            return ok;
        }
    };

    internal class SoundInfo
    {
        enum Excl
        {
            Exclude = 1 //!< If this flag is set, sound 'source' is a 'muffler' that will quite down other close sources
        };

        public string name;
        public float radius = 0;
        public float ramp_feet = 0;
        public ushort flags;
        public byte vol = 0; // sound source volume 0-255
    }

    internal class SceneRootNode_Data
    {
        public string name;
        public Vector3 pos = new Vector3(0, 0, 0);
        public Vector3 rot = new Vector3(0, 0, 0);

        public bool loadFrom(BinStore s)
        {
            bool ok = true;
            s.prepare();
            ok &= s.read(out name);
            ok &= s.read(out pos);
            ok &= s.read(out rot);
            ok &= s.prepare_nested(); // will update the file size left
            Debug.Assert(ok);
            Debug.Assert(s.end_encountered());
            return ok;
        }

        public void addRoot(LoadingContext ctx, PrefabStore store)
        {
            string newname = ctx.groupRename(name, false);
            var def = ctx.m_target.getNodeByName(newname);
            if (null == def)
            {
                if (store.loadNamedPrefab(newname, ctx))
                {
                    def = ctx.m_target.getNodeByName(newname);
                }
            }

            if (null == def)
            {
                Debug.LogErrorFormat("{0}: Missing reference:{1}=>{2}", ctx.m_renamer.basename,name, newname);
                return;
            }

            var ref_entr = ctx.m_target.newRef();
            ref_entr.node = def;
            ref_entr.mat = Tools.transformFromYPRandTranslation(rot, pos);
        }
    };

    internal class SceneGraph_Data
    {
        const uint scenegraph_i0_2_requiredCrc = 0xD3432007;

        public List<SceneGraphNode_Data> Def = new List<SceneGraphNode_Data>();
        public List<SceneRootNode_Data> Ref = new List<SceneRootNode_Data>();
        public string Scenefile;
        public int Version;

        private bool loadFrom(BinStore s)
        {
            bool ok = true;
            s.prepare();
            ok &= s.read(out Version);
            ok &= s.read(out Scenefile);
            ok &= s.prepare_nested(); // will update the file size left
            Debug.Assert(ok);
            if (s.end_encountered())
                return ok;
            string _name;
            while (s.nesting_name(out _name))
            {
                s.nest_in();
                if ("Def" == _name || "RootMod" == _name)
                {
                    var entry = new SceneGraphNode_Data();
                    ok &= entry.loadFrom(s);
                    Def.Add(entry);
                }
                else if ("Ref" == _name)
                {
                    var entry = new SceneRootNode_Data();
                    ok &= entry.loadFrom(s);
                    Ref.Add(entry);
                }
                else
                    Debug.Assert(false, "unknown field referenced.");

                s.nest_out();
            }

            return ok;
        }

        public bool LoadSceneData(string fname)
        {
            BinStore binfile = new BinStore();
            string fixed_path = Tools.getFilepathCaseInsensitive(fname);

            if (!binfile.open(fixed_path, scenegraph_i0_2_requiredCrc))
            {
                Debug.LogError("Failed to open original bin:" + fixed_path);
                return false;
            }

            if (!loadFrom(binfile))
            {
                Debug.LogError("Failed to load data from original bin:" + fixed_path);
                binfile.close();
                return false;
            }

            binfile.close();
            return true;
        }

        public void serializeIn(LoadingContext ctx, PrefabStore prefabs, string binpath)
        {
            foreach (SceneGraphNode_Data node_dat in Def)
                node_dat.addNode(ctx, prefabs, binpath);
            foreach (SceneRootNode_Data root_dat in Ref)
                root_dat.addRoot(ctx, prefabs);
        }
    };

    internal class SceneNodeChildTransform
    {
        public SceneNode node;
        public Matrix4x4 m_matrix2;
    };

    internal class LightProperties
    {
        public Color color;
        public float range;
        public bool is_negative;
    };


    internal class RootNode
    {
        public Matrix4x4 mat;
        public SceneNode node = null;
        public int index_in_roots_array = 0;
    };

    internal enum NodeState
    {
        RootNode=0,
        UsedAsPrefab = 1, // referenced as a child in many places 
        InternalNode = 2, // referenced as a child from a single place
    }
    internal class SceneGraph
    {
        // Static scene nodes loaded/created from map definition file
        List<SceneNode> all_converted_defs = new List<SceneNode>();

        List<RootNode> refs = new List<RootNode>();

        private Dictionary<SceneNode, int> m_use_counts= new Dictionary<SceneNode, int>();
        // does not include dynamic nodes
        public Dictionary<string, SceneNode> name_to_node=new Dictionary<string, SceneNode>();

        public SortedList<string,SceneNode> calculateUsages()
        {
            var topLevelNodes = new SortedList<string,SceneNode>();
            foreach (SceneNode node in all_converted_defs)
            {
                if (node!=null)
                {
                    foreach (SceneNodeChildTransform child in node.m_children)
                    {
                        if(!m_use_counts.ContainsKey(child.node))
                            m_use_counts.Add(child.node,1);
                        else
                            m_use_counts[child.node] += 1;
                    }

                    if (node.m_nest_level != 0) //from included file - not a top level node ?
                    {
                        if(!m_use_counts.ContainsKey(node))
                            m_use_counts[node]=0; //just remembering the node, so it won't show in top level ones.  
                    }
                }
            }

            foreach (SceneNode node in all_converted_defs)
            {
                if (!m_use_counts.ContainsKey(node))
                {
                    if (topLevelNodes.ContainsKey(node.m_name))
                    {
                        Debug.LogFormat("Not returning duplicate node {0}",node.m_name);
                        continue;
                    }
                    topLevelNodes.Add(node.m_name,node);
                }
            }

            return topLevelNodes;

        }
        public static SceneGraph loadWholeMap(string filename)
        {
            RuntimeData rd = RuntimeData.get();

            SceneGraph m_scene_graph = new SceneGraph();
            LoadingContext ctx = new LoadingContext(0);
            ctx.m_target = m_scene_graph;
            int geobin_idx = filename.IndexOf("geobin");
            int maps_idx = filename.IndexOf("maps");
            ctx.m_base_path = filename.Substring(0, geobin_idx);
            Debug.Assert(rd.m_prefab_mapping != null);
            string upcase_city = filename;
            upcase_city = upcase_city.Replace("city_", "City_");
            upcase_city = upcase_city.Replace("hazard_", "Hazard_");
            upcase_city = upcase_city.Replace("/trial_", "/Trial_");
            if (upcase_city.StartsWith("trial_"))
                upcase_city = "Trial_" + upcase_city.Substring(6);    
            upcase_city = upcase_city.Replace("zones_", "Zones_");
            rd.m_prefab_mapping.sceneGraphWasReset();
            bool res = ctx.loadSceneGraph(maps_idx==-1 ? upcase_city : upcase_city.Substring(maps_idx), rd.m_prefab_mapping);
            if (!res)
            {
                return null;
            }

            return m_scene_graph;
        }

        public SceneNode getNodeByName(string name)
        {
            string filename;
            int idx = name.LastIndexOf('/');
            if (idx == -1)
                filename = name;
            else
                filename = name.Substring(idx + 1);
            SceneNode res;
            name_to_node.TryGetValue(filename.ToLower(), out res);
            return res;
        }

        public SceneNode newDef(int nest_level)
        {
            SceneNode res = new SceneNode(nest_level);
            res.m_index_in_scenegraph = all_converted_defs.Count;
            all_converted_defs.Add(res);
            res.in_use = true;
            return res;
        }

        public RootNode newRef()
        {
            int idx;
            for(idx=0; idx<refs.Count; ++idx)
                if(null==refs[idx] || null==refs[idx].node)
                    break;

            if(idx>=refs.Count)
            {
                idx = refs.Count;
                refs.Add(null);
            }

            refs[idx] = new RootNode();
            refs[idx].index_in_roots_array = idx;
            return refs[idx];
        }

        public NodeState isInternalNode(SceneNode sceneNode)
        {
            int usecount;
            if (m_use_counts.TryGetValue(sceneNode, out usecount))
            {
                return usecount == 1 ? NodeState.InternalNode : NodeState.UsedAsPrefab;
            }
            return NodeState.RootNode;
        }
    }
    [Serializable]
    internal class NameList
    {
        // map from old node name to a new name
        public Dictionary<string, string> new_names = new Dictionary<string, string>();
        public string basename;
    };
}