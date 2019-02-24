using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SEGSRuntime
{
    internal class PrefabStore
    {
        private Dictionary<string, GeoStoreDef> m_dir_to_geoset = new Dictionary<string, GeoStoreDef>();
        private Dictionary<string, GeoStoreDef> m_modelname_to_geostore = new Dictionary<string, GeoStoreDef>();
        private string m_base_path;
        private GeoStoreDef m_geostore_sentinel = new GeoStoreDef(); //

        public PrefabStore(string directoryPath)
        {
            m_base_path = directoryPath;
        }

        public bool prepareGeoLookupArray(string base_path)
        {
            FileStream defnames;
            try
            {
                defnames = File.Open(base_path + "bin/defnames.bin", FileMode.Open);
            }
            catch (Exception e)
            {
                Debug.LogError(String.Format("Failed to open {0}/bin/defnames.bin", base_path));
                return false;
            }

            string contents;
            using (var sr = new StreamReader(defnames))
            {
                contents = sr.ReadToEnd();
            }

            string[] defnames_arr = contents.Replace("CHUNKS.geo", "Chunks.geo").Split('\0');
            string lookup_str;
            GeoStoreDef current_geosetinf = null;
            foreach (string str in defnames_arr)
            {
                int last_slash = str.LastIndexOf('/');
                if (-1 != last_slash)
                {
                    string geo_path = str.Substring(0, last_slash);
                    lookup_str = geo_path.ToLower();
                    if (!m_dir_to_geoset.ContainsKey(lookup_str))
                    {
                        m_dir_to_geoset[lookup_str] = new GeoStoreDef();
                    }

                    current_geosetinf = m_dir_to_geoset[lookup_str];
                    current_geosetinf.geopath = geo_path;
                }

                current_geosetinf.entries.Add(str.Substring(last_slash + 1));
                m_modelname_to_geostore[str.Substring(last_slash + 1)] = current_geosetinf;
            }

            return true;
        }

        public Model modelFind(string geoset_name, string model_name)
        {
            Model ptr_sub = null;

            if (string.IsNullOrEmpty(model_name) || string.IsNullOrEmpty(geoset_name))
            {
                Debug.LogError("Bad model/geometry set requested:");
                if (!string.IsNullOrEmpty(model_name))
                    Debug.LogError("Model: " + model_name);
                if (!string.IsNullOrEmpty(geoset_name))
                    Debug.LogError("GeoFile: " + geoset_name);
                return null;
            }

            GeoSet geoset = GeoSet.Load(geoset_name, m_base_path);
            if (null == geoset) // failed to load the geometry set
            {
                Debug.LogErrorFormat("Geoset load failed {0},{1}", geoset_name, m_base_path);
                return null;
            }

            int end_of_name_idx = model_name.IndexOf("__");
            if (end_of_name_idx == -1)
                end_of_name_idx = model_name.Length;

            string basename = model_name.Substring(0, end_of_name_idx);

            foreach (Model m in geoset.subs)
            {
                string modelname = m.name;
                if (modelname.Length == 0)
                    continue;

                bool subs_in_place = (modelname.Length <= end_of_name_idx ||
                                      modelname.Substring(end_of_name_idx).StartsWith("__"));
                if (subs_in_place && modelname.StartsWith(basename, StringComparison.OrdinalIgnoreCase))
                    ptr_sub = m; // TODO: return immediately
            }

            return ptr_sub;
        }

        public void sceneGraphWasReset()
        {
            foreach (var v in m_dir_to_geoset)
            {
                v.Value.loaded = false;
            }
        }

        internal GeoStoreDef groupGetFileEntryPtr(string full_name)
        {
            string key = full_name.Substring(full_name.LastIndexOf('/') + 1);
            int idx = key.IndexOf("__");
            if (idx != -1)
                key = key.Substring(0, idx);
            GeoStoreDef res;
            if (!m_modelname_to_geostore.TryGetValue(key, out res))
            {
                Debug.LogFormat("Failed to get geo file for model {0} : {1}", full_name, m_modelname_to_geostore.Count);
            }

            return res;
        }

        public Model groupModelFind(string path)
        {
            string model_name = path.Substring(path.LastIndexOf('/') + 1);
            var val = groupGetFileEntryPtr(model_name);
            return val != null ? modelFind(val.geopath, model_name) : null;
        }

        public bool loadNamedPrefab(string name, LoadingContext ctx)
        {
            GeoStoreDef geo_store = groupGetFileEntryPtr(name);
            if (null == geo_store)
                return false;
            if (geo_store.loaded)
                return true;

            geo_store.loaded = true;
            // load given prefab's geoset
            GeoSet.Load(geo_store.geopath, m_base_path);
            ctx.loadSubgraph(geo_store.geopath, this);
            return loadPrefabForNode(ctx.m_target.getNodeByName(name), ctx);
        }

        internal bool loadPrefabForNode(SceneNode node, LoadingContext ctx) //groupLoadRequiredLibsForNode
        {
            GeoStoreDef gf;

            if (null == node || false == node.in_use)
                return false;

            if (null != node.m_geoset_info)
                gf = node.m_geoset_info;
            else
            {
                gf = groupGetFileEntryPtr(node.m_name);
                node.m_geoset_info = gf;
                if (null == node.m_geoset_info)
                {
                    node.m_geoset_info = m_geostore_sentinel; // prevent future load attempts
                }
            }

            if (null == gf || gf == m_geostore_sentinel)
                return false;

            if (!gf.loaded)
            {
                gf.loaded = true;
                GeoSet.Load(gf.geopath, m_base_path); // load given subgraph's root geoset
                ctx.loadSubgraph(gf.geopath, this);
            }

            return true;
        }
    }

    internal class DeltaPack
    {
        public int compressed_size;
        public int uncomp_size;
        public int buffer_offset;
        public MemoryStream compressed_data;
    };

    internal struct TextureBind
    {
        public ushort tex_idx;
        public ushort tri_count;
    };

    internal class PackBlock
    {
        public DeltaPack tris = new DeltaPack();
        public DeltaPack verts = new DeltaPack();
        public DeltaPack norms = new DeltaPack();
        public DeltaPack sts = new DeltaPack();
        public DeltaPack weights = new DeltaPack();
        public DeltaPack matidxs = new DeltaPack();
        public DeltaPack grid = new DeltaPack();

        public DeltaPack get(byte idx)
        {
            switch (idx)
            {
                case 0: return tris;
                case 1: return verts;
                case 2: return norms;
                case 3: return sts;
                case 4: return weights;
                case 5: return matidxs;
                case 6: return grid;
            }

            return null;
        }
    };

    internal class BoneInfo
    {
        public int numbones;
        public int[] bone_ID = new int[15];
    };

    internal class GeoStoreDef
    {
        public string geopath; //!< a path to a .geo file
        public List<string> entries = new List<string>(); //!< the names of models contained in a geoset
        public bool loaded = false;
    }
}