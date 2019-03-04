using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using UnityEditor;

namespace SEGSRuntime
{
    public enum CoHBlendMode
    {
        MULTIPLY = 0,
        MULTIPLY_REG = 1,
        COLORBLEND_DUAL = 2,
        ADDGLOW = 3,
        ALPHADETAIL = 4,
        BUMPMAP_MULTIPLY = 5,
        BUMPMAP_COLORBLEND_DUAL = 6,
        INVALID = 255,
    };
    [Serializable]
    public class TextureWrapper
    {
        [Flags]
        public enum TexFlag
        {
            ALPHA = 0x0001,
            RGB8 = 0x0002,
            COMP4 = 0x0004,
            COMP8 = 0x0008,
            DUAL = 0x0010,
            CLAMP = 0x0080,
            CUBEMAPFACE = 0x0200,
            REPLACEABLE = 0x0400,
            BUMPMAP = 0x0800,
            BUMPMAP_MIRROR = 0x1000,
        };

        public Texture tex;
        public string detailname;
        public string bumpmap;
        [EnumFlag]
        public TexFlag flags = 0;
        public Vector2 scaleUV0 = new Vector2(0, 0);
        public Vector2 scaleUV1 = new Vector2(0, 0);

        public CoHBlendMode BlendType = 0;
        public TextureModifiers_Data info = null;
    }

    internal class Tools
    {
        static public string getFilepathCaseInsensitive(string fpath)
        {
            // Windows is far too lax about case sensitivity. Consequently
            // filenames aren't consistent. This should derive the filename
            // based upon a case-insensitive comparison, and use the actual
            // formatted filepath when loading scene data.

            // check file exists, if so, return original path
            if (File.Exists(fpath))
                return fpath;

            // get base from path
            string base_path = new FileInfo(fpath).Directory.FullName;
            Debug.LogFormat("Search for {0} in {1}",fpath,base_path);
            string reconstructed_path="";
            string[] parts = base_path.Split('/');
            for (int x = 0; x < parts.Length; ++x)
            {
                if(parts[x].Length==0)
                    continue;
               
                if (Directory.Exists(reconstructed_path + "/" +parts[x]))
                {
                    reconstructed_path += "/" + parts[x];
                }
                else
                {
                    string[] alldirs = Directory.GetDirectories(reconstructed_path);
                    string last_part="";
                    int idx = -1;
                    for (int didx = 0; didx < alldirs.Length; ++didx)
                    {
                        last_part = Path.GetFileName(alldirs[didx]);
                        if (0 == String.Compare(last_part, parts[x], StringComparison.OrdinalIgnoreCase))
                        {
                            idx = didx;
                            break;
                        }
                    }
                    if (idx == -1)
                    {
                        Debug.LogWarningFormat("Cannot find path {0} under {1}",fpath,reconstructed_path);
                        return fpath;
                    }
                    reconstructed_path += "/" + last_part;
                }
            }

            base_path = reconstructed_path; //remove last slash
            if (!Directory.Exists(base_path))
            {
                Debug.LogWarning("Failed to open" + base_path);
            }

            string[] files = Directory.GetFiles(base_path);
            foreach (string f in files)
            {
                string fl = Path.GetFileName(f);
                if (fpath.EndsWith(fl, StringComparison.OrdinalIgnoreCase))
                    fpath = base_path + "/" + fl;
            }

            return fpath;
        }

        static public bool groupInLibSub(string name)
        {
            if (name.Contains("/"))
            {
                return !name.StartsWith("maps");
            }
            return !name.StartsWith("grp");
        }

        static public Color32 RGBAToColor32(uint v)
        {
            Color32 res = new Color32();
            res.r = (byte) (v & 0xFF);
            v >>= 8;
            res.b = (byte) (v & 0xFF);
            v >>= 8;
            res.g = (byte) (v & 0xFF);
            v >>= 8;
            res.a = (byte) (v & 0xFF);
            return res;
        }
        static public Matrix4x4 transformFromYPRandTranslation(Vector3 pyr, Vector3 translation)
        {
            float cos_p = Mathf.Cos(pyr.x);
            float neg_sin_p = -Mathf.Sin(pyr.x);
            float cos_y = Mathf.Cos(pyr.y);
            float neg_sin_y = -Mathf.Sin(pyr.y);
            float cos_r = Mathf.Cos(pyr.z);
            float neg_sin_r = -Mathf.Sin(pyr.z);
            float tmp = -cos_y * neg_sin_p;
            Matrix4x4 rotmat = new Matrix4x4();

            rotmat[0, 0] = cos_r * cos_y - neg_sin_y * neg_sin_p * neg_sin_r;
            rotmat[0, 1] = neg_sin_r * cos_p;
            rotmat[0, 2] = tmp * neg_sin_r + cos_r * neg_sin_y;
            rotmat[1, 0] = -(neg_sin_r * cos_y) - neg_sin_y * neg_sin_p * cos_r;
            rotmat[1, 1] = cos_r * cos_p;
            rotmat[1, 2] = tmp * cos_r - neg_sin_r * neg_sin_y;
            rotmat[2, 0] = -(neg_sin_y * cos_p);
            rotmat[2, 1] = -neg_sin_p;
            rotmat[2, 2] = cos_y * cos_p;
            rotmat[0, 3] = rotmat[1, 3] = rotmat[2, 3] = 0;
            rotmat.SetRow(3, new Vector4(translation.x, translation.y, translation.z, 1));
            return rotmat;
        }

        private static void CopyStream(System.IO.Stream input, System.IO.Stream output)
        {
            byte[] buffer = new byte[2000];
            int len;
            while ((len = input.Read(buffer, 0, 2000)) > 0)
            {
                output.Write(buffer, 0, len);
            }

            output.Flush();
        }

        public static void DecompressData(byte[] inData, out byte[] outData, int tgt_size)
        {
            ICSharpCode.SharpZipLib.Zip.Compression.Inflater infl =
                new ICSharpCode.SharpZipLib.Zip.Compression.Inflater(false);
            var inStream = new InflaterInputStream(new MemoryStream(inData), infl);
            outData = new byte[tgt_size];
            try
            {
                int numRead = inStream.Read(outData, 0, tgt_size);
                if (numRead != tgt_size)
                {
                    throw  new Exception();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected exception - '{0}'", ex.Message);
                throw;
            }
        }
        public static void DecompressData(MemoryStream inData, out byte[] outData,int tgt_size)
        {
            ICSharpCode.SharpZipLib.Zip.Compression.Inflater infl =
                new ICSharpCode.SharpZipLib.Zip.Compression.Inflater(false);
            var inStream = new InflaterInputStream(inData, infl);
            inStream.IsStreamOwner = false;
            outData = new byte[tgt_size];

            int count = tgt_size;
            try
            {
                int numRead = inStream.Read(outData, 0, tgt_size);
                if (numRead != tgt_size)
                {
                    throw  new Exception();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected exception - '{0}'", ex.Message);
                throw;
            }
        }

        public static void EnsureDirectoryExists(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }

    struct PackInfo
    {
        public int compressed_size;
        public int uncomp_size;
        public int compressed_data_off;
    };

    class UnityModel
    {
        public Mesh m_mesh;
        public Material m_material;
    }

    struct Model32
    {
        public int flg1;
        public float radius;
        public int vbo;
        public uint num_textures;
        public short id;
        public byte blend_mode;
        public char loadstate;
        public int boneinfo;
        public int trck_node;
        public uint vertex_count;
        public uint model_tri_count;
        public int texture_bind_offsets;
        public int unpacked_1;
        public Vector3 grid_pos;
        public float grid_size;
        public float grid_invsize;
        public float grid_tag;
        public int grid_numbits;
        public int ctris;
        public int triangle_tags;
        public int bone_name_offset;
        public int num_altpivots;
        public int extra;
        public Vector3 m_scale;
        public Vector3 m_min;
        public Vector3 m_max;
        public int geoset_list_idx;
        public PackInfo[] pack_data; //[7]
    };

    internal class GeoSet
    {
        public string geopath;
        public string name;
        public GeoSet parent_geoset = null;
        public List<Model> subs = new List<Model>();
        public List<string> tex_names = new List<string>();

        public byte[] m_geo_data;
        public int geo_data_size;
        public bool data_loaded = false;

         Dictionary<string, TextureWrapper> s_coh_tex_to_wrapper = new Dictionary<string, TextureWrapper>();
        public string full_geo_path;

        /// load the given geoset, used when loading scene-subgraph and nodes
        public static GeoSet Load(string m, string base_path)
        {
            RuntimeData rd=RuntimeData.get();
            GeoSet res;
            if (rd.s_name_to_geoset.TryGetValue(m, out res))
                return res;

            return findAndPrepareGeoSet(m, base_path);
        }

        private static GeoSet findAndPrepareGeoSet(string fname, string base_path)
        {
            GeoSet geoset = null;
            string name_fixed = fname.Replace(".anm", ".geo");
            string true_path = Tools.getFilepathCaseInsensitive(base_path + name_fixed);
            FileStream fp;
            try
            {
                fp = File.Open(true_path, FileMode.Open);
            }
            catch (Exception)
            {
                Debug.LogError("Can't find .geo file" + fname);
                return null;
            }

            geoset = new GeoSet();
            if (true_path.StartsWith(base_path))
            {
                geoset.geopath = true_path.Substring(base_path.Length);
            }
            else
                throw new InvalidOperationException("True path is not a sub-path of base directory");

            geoset.geosetLoadHeader(fp);
            geoset.full_geo_path = true_path;
            fp.Close();
            RuntimeData rd=RuntimeData.get();
            rd.s_name_to_geoset[fname] = geoset;
            return geoset;
        }

        internal struct TexBlockInfo
        {
            public int size1;
            public uint texname_blocksize;
            public int bone_names_size;
            public int tex_binds_size;
        };

        TexBlockInfo readTexBlock(BinaryReader br)
        {
            TexBlockInfo res;
            res.size1 = br.ReadInt32();
            res.texname_blocksize = br.ReadUInt32();
            res.bone_names_size = br.ReadInt32();
            res.tex_binds_size = br.ReadInt32();
            return res;
        }

        struct GeosetHeader32
        {
            public char[] name;
            public int unkn1;
            public int subs_idx;
            public int num_subs;
        };

        GeosetHeader32 readGeosetHeader(BinaryReader br)
        {
            GeosetHeader32 res;
            res.name = br.ReadChars(128);
            res.unkn1 = br.ReadInt32();
            res.subs_idx = br.ReadInt32();
            res.num_subs = br.ReadInt32();
            return res;
        }

        string readCStringAtOffset(BinaryReader br, int offset)
        {
            long start = br.BaseStream.Position;
            br.BaseStream.Seek(offset, SeekOrigin.Begin);
            char c = br.ReadChar();
            string entry = "";
            while (c != 0)
            {
                entry += c;
                c = br.ReadChar();
            }

            return entry;
        }

        private List<string> convertTextureNames(BinaryReader br)
        {
            List<string> res = new List<string>();
            int num_textures = br.ReadInt32();
            int[] indices = new int[num_textures];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = br.ReadInt32();
            }

            long start_of_strings_area = br.BaseStream.Position;
            long max_end = start_of_strings_area;
            for (int idx = 0; idx < num_textures; ++idx)
            {
                // fixup the offsets by adding the end of index area
                br.BaseStream.Seek(start_of_strings_area + indices[idx], SeekOrigin.Begin);
                char c = br.ReadChar();
                string entry = "";
                while (c != 0)
                {
                    entry += c;
                    c = br.ReadChar();
                }

                if (br.BaseStream.Position > max_end)
                    max_end = br.BaseStream.Position;
                res.Add(entry);
            }

            br.BaseStream.Seek(max_end, SeekOrigin.Begin);

            return res;
        }

        Vector3 readVector3(BinaryReader br)
        {
            Vector3 res = new Vector3();
            res.x = br.ReadSingle();
            res.y = br.ReadSingle();
            res.z = br.ReadSingle();
            return res;
        }

        private Model32[] readModel32(BinaryReader decompHdr, int header32NumSubs)
        {
            Model32[] res = new Model32[header32NumSubs];
            for (int i = 0; i < header32NumSubs; ++i)
            {
                var start = decompHdr.BaseStream.Position;
                res[i].flg1 = decompHdr.ReadInt32();
                res[i].radius = decompHdr.ReadSingle();
                res[i].vbo = decompHdr.ReadInt32();
                res[i].num_textures = decompHdr.ReadUInt32();
                res[i].id = decompHdr.ReadInt16();
                res[i].blend_mode = decompHdr.ReadByte();
                res[i].loadstate = (char) decompHdr.ReadByte();
                res[i].boneinfo = decompHdr.ReadInt32();
                res[i].trck_node = decompHdr.ReadInt32();
                res[i].vertex_count = decompHdr.ReadUInt32();
                res[i].model_tri_count = decompHdr.ReadUInt32();
                res[i].texture_bind_offsets = decompHdr.ReadInt32();
                res[i].unpacked_1 = decompHdr.ReadInt32();
                res[i].grid_pos = readVector3(decompHdr);
                res[i].grid_size = decompHdr.ReadSingle();
                res[i].grid_invsize = decompHdr.ReadSingle();
                res[i].grid_tag = decompHdr.ReadSingle();
                res[i].grid_numbits = decompHdr.ReadInt32();
                res[i].ctris = decompHdr.ReadInt32();
                res[i].triangle_tags = decompHdr.ReadInt32();
                res[i].bone_name_offset = decompHdr.ReadInt32();
                res[i].num_altpivots = decompHdr.ReadInt32();
                res[i].extra = decompHdr.ReadInt32();
                res[i].m_scale = readVector3(decompHdr);
                res[i].m_min = readVector3(decompHdr);
                res[i].m_max = readVector3(decompHdr);
                res[i].geoset_list_idx = decompHdr.ReadInt32();
                res[i].pack_data = new PackInfo[7];
                for (int j = 0; j < 7; ++j)
                {
                    res[i].pack_data[j].compressed_size = decompHdr.ReadInt32();
                    res[i].pack_data[j].uncomp_size = decompHdr.ReadInt32();
                    res[i].pack_data[j].compressed_data_off = decompHdr.ReadInt32();
                }
            }

            return res;
        }

        static List<TextureBind> convertTexBinds(uint cnt, int offset, BinaryReader src)
        {
            List<TextureBind> res = new List<TextureBind>();
            long curpos = src.BaseStream.Position;
            src.BaseStream.Seek(offset, SeekOrigin.Begin);
            for (uint i = 0; i < cnt; ++i)
            {
                TextureBind tb;
                tb.tex_idx = src.ReadUInt16();
                tb.tri_count = src.ReadUInt16();
                res.Add(tb);
            }

            src.BaseStream.Seek(curpos, SeekOrigin.Begin);
            return res;
        }

        static Model convertAndInsertModel(GeoSet tgt, Model32 v)
        {
            Model z = new Model();

            z.flags = (ModelFlags)v.flg1;
            z.visibility_radius = v.radius;
            z.num_textures = v.num_textures;
            z.m_id = v.id;
            z.boneinfo_offset = v.boneinfo;

            z.BlendMode = (CoHBlendMode) v.blend_mode;
            z.vertex_count = v.vertex_count;
            z.model_tri_count = v.model_tri_count;
            z.scale = v.m_scale;
            z.box.SetMinMax(v.m_min, v.m_max);
            for (byte i = 0; i < 7; ++i)
            {
                DeltaPack dp_blk = z.packed_data.get(i);
                PackInfo pi = v.pack_data[i];
                dp_blk.compressed_size = pi.compressed_size;
                dp_blk.uncomp_size = pi.uncomp_size;
                dp_blk.compressed_data = null;
                dp_blk.buffer_offset = pi.compressed_data_off;
            }

            tgt.subs.Add(z);
            return z;
        }

        private void geosetLoadHeader(FileStream fp)
        {
            int anm_hdr_size;
            int headersize;
            BinaryReader br = new BinaryReader(fp, new ASCIIEncoding());
            anm_hdr_size = br.ReadInt32();
            anm_hdr_size -= 4;
            headersize = br.ReadInt32();

            byte[] zipmem = br.ReadBytes(anm_hdr_size);
            byte[] decompr;
            Tools.DecompressData(zipmem, out decompr,headersize);

            Stream unc_arr = new MemoryStream(decompr);
            BinaryReader decomp_hdr = new BinaryReader(unc_arr, new ASCIIEncoding());

            //const uint8_t * mem = (const uint8_t *)unc_arr.data();
            TexBlockInfo info = readTexBlock(decomp_hdr);
            this.geo_data_size = info.size1;

            var ver = decomp_hdr.BaseStream.Position;
            tex_names = convertTextureNames(decomp_hdr);

            
            decomp_hdr.BaseStream.Seek(ver + info.texname_blocksize, SeekOrigin.Begin);

            MemoryStream bone_name_stream = new MemoryStream(decomp_hdr.ReadBytes(info.bone_names_size));
            BinaryReader bone_name_reader = new BinaryReader(bone_name_stream, new ASCIIEncoding());

            MemoryStream tex_binds_stream = new MemoryStream(decomp_hdr.ReadBytes(info.tex_binds_size));
            BinaryReader tex_bind_reader = new BinaryReader(tex_binds_stream, new ASCIIEncoding());


            GeosetHeader32 header32 = readGeosetHeader(decomp_hdr);
            Model32[] ptr_subs = readModel32(decomp_hdr, header32.num_subs);
            parent_geoset = this;
            name = new string(header32.name);
            name = name.Substring(0, Math.Max(0, name.IndexOf('\0')));
            bool has_alt_pivot = false;
            for (int idx = 0; idx < header32.num_subs; ++idx)
            {
                Model32 sub_model = ptr_subs[idx];
                List<TextureBind> binds = new List<TextureBind>();
                if (info.tex_binds_size != 0)
                {
                    binds = convertTexBinds(sub_model.num_textures, sub_model.texture_bind_offsets, tex_bind_reader);
                }

                if (sub_model.num_altpivots > 0)
                    has_alt_pivot |= true;
                Model m = convertAndInsertModel(this, sub_model);
                m.texture_bind_info = binds;
                m.geoset = this;
                m.name = readCStringAtOffset(bone_name_reader, sub_model.bone_name_offset);
                ptr_subs[idx] = sub_model;
            }

            if (this.subs.Count != 0)
                addModelStubs(this);
            if (has_alt_pivot)
                Debug.Log("Alternate model pivots were not converted");
        }

        void addModelStubs(GeoSet geoset)
        {
            RuntimeData rd = RuntimeData.get();
            foreach (Model m in geoset.subs)
            {
                GeometryModifiersData gmod = rd.m_modifiers.findGeomModifier(m.name, "");
                if (gmod != null)
                {
                    if (null == m.trck_node)
                        m.trck_node = new ModelModifiers();
                    if (m.name == "_FLRSHPS_WIN_H__FLRSHPS_Window")
                    {
                        Debug.LogFormat("GMod for geoset model {0}",gmod);
                    }
                    m.trck_node = gmod.node.clone();
                    m.src_mod = gmod;
                }
            }
        }

        private void fixupDataPtr(DeltaPack a, byte[] b)
        {
            if (a.uncomp_size != 0)
            {
                MemoryStream ms = new MemoryStream(b,a.buffer_offset,a.compressed_size==0 ? a.uncomp_size : a.compressed_size);

                a.compressed_data = ms;
            }
        }

        public void LoadData()
        {
            FileStream fs = File.Open(full_geo_path, FileMode.Open);
            BinaryReader fr = new BinaryReader(fs);
            int buffer;

            fs.Seek(0, SeekOrigin.Begin);
            buffer = fr.ReadInt32();
            fs.Seek(buffer + 8, SeekOrigin.Begin);

            m_geo_data = fr.ReadBytes(geo_data_size);

            foreach (Model current_sub in subs)
            {
                fixupDataPtr(current_sub.packed_data.tris, m_geo_data);
                fixupDataPtr(current_sub.packed_data.verts, m_geo_data);
                fixupDataPtr(current_sub.packed_data.norms, m_geo_data);
                fixupDataPtr(current_sub.packed_data.sts, m_geo_data);
                fixupDataPtr(current_sub.packed_data.grid, m_geo_data);
                fixupDataPtr(current_sub.packed_data.weights, m_geo_data);
                fixupDataPtr(current_sub.packed_data.matidxs, m_geo_data);
                if (current_sub.boneinfo_offset != 0)
                {
                    Debug.Log("Not converting bones yet");
                    //convertModelBones(current_sub, (ModelBones_32 *)(buffer_b + current_sub.boneinfo_offset));
                }
            }
            fs.Close();
            data_loaded = true;
        }

        public void createEngineModelsFromPrefabSet()
        {
            var model_textures = getModelTextures(tex_names);
            RuntimeData rd=RuntimeData.get();
            foreach (Model model in subs)
            {
                rd.s_coh_model_to_engine_model[model] = model.modelCreateObjectFromModel(model_textures);
            }
        }

        static private string getNamedTexturePath(string name)
        {
            string base_name = Path.GetFileNameWithoutExtension(name);
            string[] guids2 = AssetDatabase.FindAssets(base_name, new[] {"Assets/Resources/texture_library"});
            foreach (string guid2 in guids2)
            {
                string resource_path = AssetDatabase.GUIDToAssetPath(guid2);
                if (Path.GetFileNameWithoutExtension(resource_path) == base_name)
                {
                    return resource_path;
                }
            }
            guids2 = AssetDatabase.FindAssets("t:Texture", new[] {"Assets/Resources/texture_library"});
            foreach (string gid in guids2)
            {
                string resource_path = AssetDatabase.GUIDToAssetPath(gid);
                if (resource_path.ToLower().Contains(base_name.ToLower()))
                {
                    Debug.LogFormat("Has it as different case! {0},{1}",resource_path,base_name);
                    return resource_path;
                }
                
            }
            return null;
        }

        ///
        /// \brief Will split the \arg texpath into directories, and finds the closest TextureModifiers
        /// that matches a directory
        /// \param texpath contains a full path to the texture
        /// \return texture modifier object, if any
        ///
        static TextureModifiers_Data modFromTextureName(string texpath)
        {
            RuntimeData rd = RuntimeData.get();
            List<string> split = texpath.Split('/').ToList();
            while (split.Count != 0)
            {
                if (split[0] == "texture_library")
                {
                    split.RemoveAt(0);
                    break;
                }

                split.RemoveAt(0);
            }

            SceneModifiers mods = rd.m_modifiers;
            var texmods = mods.m_texture_path_to_mod;
            // scan from the back of the texture path, until a modifier is found.
            if (texpath.Contains("shape"))
            {
                Debug.LogFormat("Mod for {0} -{1}",texpath,string.Join(",",split));
            }
            while (split.Count != 0)
            {
                TextureModifiers_Data texmod_val;
                if (texmods.TryGetValue(split[split.Count - 1].ToLower(), out texmod_val))
                {
                    return texmod_val;
                }

                split.RemoveAt(split.Count - 1);
            }

            return null;
        }
        [Flags]
        enum TexOpt : uint
        {
            FADE        = 0x0001,
            DUAL        = 0x0010,
            REPLACEABLE = 0x0800,
            BUMPMAP     = 0x1000,
        };
        static public TextureWrapper loadTexHeader(string tex_name)
        {
            string fname = getNamedTexturePath(tex_name);
            if (String.IsNullOrEmpty(fname))
            {
                Debug.LogFormat("LoadTexHeader failed for asset {0}->{1}",tex_name,fname);
                return null;
            }
            RuntimeData rd=RuntimeData.get();
            TextureWrapper res;
            
            FileInfo tex_path = new FileInfo(fname);
            string lookupstring = Path.GetFileNameWithoutExtension(tex_path.Name).ToLower();

            if (rd.m_loaded_textures.TryGetValue(lookupstring, out res))
            {
                return res;
            }
            
            res = new TextureWrapper();
            res.tex = AssetDatabase.LoadAssetAtPath<Texture>(fname);
            
            res.info = modFromTextureName(Path.GetDirectoryName(fname)+"/"+Path.GetFileNameWithoutExtension(fname));
            TexOpt texopt_flags = 0;
            if (res.info != null)
                texopt_flags = (TexOpt)res.info.Flags;
            string upname = fname.ToUpper();
            if (upname.Contains("PLAYERS/") ||
                upname.Contains("ENEMIES/") ||
                upname.Contains("NPCS/"))
                res.flags |= TextureWrapper.TexFlag.BUMPMAP_MIRROR | TextureWrapper.TexFlag.CLAMP;

            if (upname.Contains("MAPS/"))
                res.flags |= TextureWrapper.TexFlag.CLAMP;

            if (texopt_flags.HasFlag(TexOpt.REPLACEABLE))
                res.flags |= TextureWrapper.TexFlag.REPLACEABLE;

            if (texopt_flags.HasFlag(TexOpt.BUMPMAP))
                res.flags |= TextureWrapper.TexFlag.BUMPMAP;

            res.scaleUV0 = new Vector2(1, 1);
            res.scaleUV1 = new Vector2(1, 1);

            if (res.info!=null && 0!=res.info.BumpMap.Length)
                res.bumpmap = res.info.BumpMap;
            string detailname;
            if (texopt_flags.HasFlag(TexOpt.DUAL))
            {
                if (res.info.Blend.Length!=0)
                {
                    res.flags |= TextureWrapper.TexFlag.DUAL;
                    res.BlendType = (CoHBlendMode)res.info.BlendType;
                    res.scaleUV0 =  res.info.ScaleST0;
                    res.scaleUV1 =  res.info.ScaleST1;
                    res.detailname = res.info.Blend;

                    if (res.BlendType == CoHBlendMode.ADDGLOW && res.detailname.ToLower() == "grey")
                    {
                        res.detailname = "black";
                    }

                    // copy the 'res' into the handle based storage, and record the handle
                    rd.m_loaded_textures[lookupstring] = res;
                    return res;
                }

                Debug.Log("Detail texture " + res.info.Blend + " does not exist for texture mod" + res.info.name);
                detailname = "grey";
            }
            else if (lookupstring.ToLower()=="invisible")
            {
                detailname = "invisible";
            }
            else
            {
                detailname = "grey";
            }

            if (res.BlendType == CoHBlendMode.ADDGLOW && detailname.ToLower()=="grey")
            {
                detailname = "black";
            }

            res.detailname = detailname;
            // copy the 'res' into the handle based storage, and record the handle
            rd.m_loaded_textures[lookupstring] = res;
            return res;
        }

        private List<TextureWrapper> getModelTextures(List<string> names)
        {
            List<TextureWrapper> res = new List<TextureWrapper>();
            TextureWrapper white_w = loadTexHeader("white.dds");
            
            for (int tex_idx = 0; tex_idx < names.Count; ++tex_idx)
            {
                TextureWrapper found = white_w; // initialize this, so if we're missing a texture, we'll get something
                string fe;
                string tex_to_find = names[tex_idx];
                if (tex_to_find.ToUpper().Contains("PORTAL"))
                    tex_to_find = "invisible.dds";

                string local_path = getNamedTexturePath(tex_to_find);
                if (local_path == null)
                {
                    fe = String.Format("Model needs texture {0}\n", tex_to_find);
                    fe += String.Format("Resource system does not have it");
                    Debug.LogWarning(fe);
                }

                if (local_path != null)
                    found = loadTexHeader(tex_to_find);
                // TODO: make missing textures much more visible ( high contrast + text ? )
                res.Add(found);
            }

            if (names.Count == 0)
                res.Add(white_w);
            return res;
        }
    };
}
