using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SEGSRuntime
{
    [Flags]
    internal enum ModelFlags
    {
        OBJ_ALPHASORT = 0x1,
        OBJ_FULLBRIGHT = 0x4,
        OBJ_NOLIGHTANGLE = 0x10,
        OBJ_DUALTEXTURE = 0x40,
        OBJ_LOD = 0x80,
        OBJ_TREE = 0x100,
        OBJ_DUALTEX_NORMAL = 0x200,
        OBJ_FORCEOPAQUE = 0x400,
        OBJ_BUMPMAP = 0x800,
        OBJ_WORLDFX = 0x1000,
        OBJ_CUBEMAP = 0x2000,
        OBJ_DRAW_AS_ENT = 0x4000,
        OBJ_STATICFX = 0x8000,
        OBJ_HIDE = 0x10000,
    };

    internal class VBOPointers
    {
        public Vector3[] pos;
        public Vector3[] norm;
        public Vector2[] uv1;
        public Vector2[] uv2;

        public Vector3Int[] triangles;
        public List<TextureWrapper> assigned_textures = new List<TextureWrapper>();
        public Vector2[] bone_weights;
        public List<Tuple<ushort, ushort>> bone_indices;
        public bool needs_tangents = false;
    };

    internal class Model
    {
        public Bounds box;
        public string name;
        public ModelFlags flags;
        public float visibility_radius;
        public uint num_textures;
        public PackBlock packed_data = new PackBlock();
        public List<TextureBind> texture_bind_info;
        public long boneinfo_offset = 0;
        public BoneInfo bone_info_data = null;
        public GeoSet geoset;
        public ModelModifiers trck_node = null;
        public Vector3 scale;
        public uint vertex_count;
        public uint model_tri_count;
        public CoHBlendMode blend_mode;

        public VBOPointers vbo;

        // indices of bone matrices influencing this model
        public List<int> m_bones;
        public int m_id;
        public int m_load_state;

        public bool isLoaded()
        {
            return (m_load_state & 4) != 0;
        }

        public bool hasBoneWeights()
        {
            return flags.HasFlag(ModelFlags.OBJ_DRAW_AS_ENT);
        }

        void initLoadedModel(List<TextureWrapper> textures)
        {
            blend_mode = CoHBlendMode.MULTIPLY_REG;
            bool isgeo = false;
            if (name.StartsWith("GEO_", StringComparison.OrdinalIgnoreCase))
            {
                flags |= ModelFlags.OBJ_DRAW_AS_ENT;
                isgeo = true;
                if (name.ToLower().Contains("eyes"))
                {
                    if (null == trck_node)
                        trck_node = new ModelModifiers();
                    trck_node._TrickFlags |= TrickFlags.DoubleSided;
                }
            }

            Debug.Assert(num_textures == texture_bind_info.Count);
            foreach (TextureBind tbind in texture_bind_info)
            {
                TextureWrapper base_tex = textures[tbind.tex_idx];
                TextureWrapper sel_tex = base_tex;
                if (null == base_tex)
                {
                    // missing texture, nothing to do here
                    continue;
                }

                if (!isgeo && null != base_tex.info)
                {
                    var blend = (CoHBlendMode) base_tex.info.BlendType;
                    if (blend == CoHBlendMode.ADDGLOW || blend == CoHBlendMode.COLORBLEND_DUAL ||
                        blend == CoHBlendMode.ALPHADETAIL)
                        sel_tex = GeoSet.loadTexHeader(base_tex.detailname);
                }

                if (base_tex.flags.HasFlag(TextureWrapper.TexFlag.DUAL))
                {
                    flags |= ModelFlags.OBJ_DUALTEXTURE;
                    if (base_tex.BlendType != CoHBlendMode.MULTIPLY)
                        blend_mode = base_tex.BlendType;
                }

                if (!String.IsNullOrEmpty(base_tex.bumpmap))
                {
                    TextureWrapper wrap = GeoSet.loadTexHeader(base_tex.bumpmap);
                    if (wrap.flags.HasFlag(TextureWrapper.TexFlag.BUMPMAP))
                    {
                        flags |= ModelFlags.OBJ_BUMPMAP;
                        blend_mode = (blend_mode == CoHBlendMode.COLORBLEND_DUAL)
                            ? CoHBlendMode.BUMPMAP_COLORBLEND_DUAL
                            : CoHBlendMode.BUMPMAP_MULTIPLY;
                    }

                    if (base_tex.flags.HasFlag(TextureWrapper.TexFlag.CUBEMAPFACE) ||
                        (wrap.flags.HasFlag(TextureWrapper.TexFlag.CUBEMAPFACE)))
                        flags |= ModelFlags.OBJ_CUBEMAP;
                }
            }

            if (trck_node != null && trck_node.info != null)
            {
                flags |= (ModelFlags) trck_node.info.ObjFlags;
            }

            if (blend_mode == CoHBlendMode.COLORBLEND_DUAL ||
                blend_mode == CoHBlendMode.BUMPMAP_COLORBLEND_DUAL)
            {
                if (null == trck_node)
                    trck_node = new ModelModifiers();
                trck_node._TrickFlags |= TrickFlags.SetColor;
            }

            if (blend_mode == CoHBlendMode.ADDGLOW)
            {
                if (null == trck_node)
                    trck_node = new ModelModifiers();
                trck_node._TrickFlags |= TrickFlags.SetColor | TrickFlags.NightLight;
            }

            if (0 == packed_data.norms.uncomp_size) // no normals
                flags |= ModelFlags.OBJ_FULLBRIGHT; // only ambient light
            if (null != trck_node && trck_node._TrickFlags.HasFlag(TrickFlags.Additive))
                flags |= ModelFlags.OBJ_ALPHASORT; // alpha pass
            if (flags.HasFlag(ModelFlags.OBJ_FORCEOPAQUE)) // force opaque
                flags &= ~ModelFlags.OBJ_ALPHASORT;

            if (null != trck_node && null != trck_node.info)
            {
                if (0 != trck_node.info.blend_mode)
                    blend_mode = (CoHBlendMode) trck_node.info.blend_mode;
            }
        }

        internal enum UnpackMode
        {
            UNPACK_FLOATS,
            UNPACK_INTS,
        }
        long unpackedDeltaPack(out int[] tgt_buf, MemoryStream data, uint entry_size, uint num_entries)
        {
            int[] int_acc = {0, 0, 0};
            int bit_offset;
            int processed_val = 0;
            uint extracted_2bits_;
            List<int> entries = new List<int>();
            byte[] delta_buffer = new byte[(entry_size * 2 * num_entries + 7) / 8];
            data.Read(delta_buffer,0,(int)(entry_size * 2 * num_entries + 7) / 8);
            BinaryReader delta_flags = new BinaryReader(new MemoryStream(delta_buffer));
            
            BinaryReader data_src = new BinaryReader(data);
            data_src.ReadByte();// skip over unused scale
            bit_offset = 0;
            for (int entry_idx = 0; entry_idx < num_entries; ++entry_idx)
            {
                for (int idx = 0; idx < entry_size; ++idx)
                {
                    delta_flags.BaseStream.Seek(bit_offset >> 3,SeekOrigin.Begin);
                    extracted_2bits_ = ((uint) delta_flags.ReadByte() >> (bit_offset & 0x7)) & 3;
                    bit_offset += 2;
                    switch (extracted_2bits_)
                    {
                        case 0:
                            processed_val = 0;
                            break;
                        case 1:
                            processed_val = (int) data_src.ReadByte() - 127;
                            break;
                        case 2:
                        {
                            byte t1 = data_src.ReadByte();
                            byte t2 = data_src.ReadByte();
                            processed_val = (int) ((t2 << 8) | t1) - 32767;
                            break;
                        }
                        case 3:
                        {
                            byte t1 = data_src.ReadByte();
                            byte t2 = data_src.ReadByte();
                            byte t3 = data_src.ReadByte();
                            byte t4 = data_src.ReadByte();
                            processed_val = (t4 << 24) | (t3 << 16) | (t2 << 8) | t1;
                            break;
                        }
                        default: break;
                    }

                    int_acc[idx] += processed_val + 1;
                    entries.Add(int_acc[idx]);
                }
            }

            tgt_buf = entries.ToArray();
            return data_src.BaseStream.Position;
        }

        long unpackedDeltaPack(out float[] tgt_buf, MemoryStream data, uint entry_size, uint num_entries)
        {
            float[] float_acc = {0, 0, 0};
            int bit_offset;
            int processed_val = 0;
            uint extracted_2bits_;
            float scaling_val;
            float inv_scale;
            List<float> entries = new List<float>();
            byte[] delta_buffer = new byte[(entry_size * 2 * num_entries + 7) / 8];
            data.Read(delta_buffer,0,(int)(entry_size * 2 * num_entries + 7) / 8);
            BinaryReader delta_flags = new BinaryReader(new MemoryStream(delta_buffer));
            BinaryReader data_src = new BinaryReader(data);

            bit_offset = 0;
            scaling_val = 1.0f;
            inv_scale = (float) (1 << data_src.ReadByte());
            if (inv_scale != 0.0f)
                scaling_val = 1.0f / inv_scale;
            for (int entry_idx = 0; entry_idx < num_entries; ++entry_idx)
            {
                for (int idx = 0; idx < entry_size; ++idx)
                {
                    delta_flags.BaseStream.Seek(bit_offset >> 3,SeekOrigin.Begin);
                    extracted_2bits_ = ((uint) delta_flags.ReadByte() >> (bit_offset & 0x7)) & 3;
                    bit_offset += 2;
                    switch (extracted_2bits_)
                    {
                        case 0:
                            processed_val = 0;
                            break;
                        case 1:
                            processed_val = (int) data_src.ReadByte() - 127;
                            break;
                        case 2:
                        {
                            byte t1 = data_src.ReadByte();
                            byte t2 = data_src.ReadByte();
                            processed_val = (int) ((t2 << 8) | t1) - 32767;
                            break;
                        }
                        case 3:
                        {
                            byte t1 = data_src.ReadByte();
                            byte t2 = data_src.ReadByte();
                            byte t3 = data_src.ReadByte();
                            byte t4 = data_src.ReadByte();
                            processed_val = (t4 << 24) | (t3 << 16) | (t2 << 8) | t1;
                            break;
                        }
                        default: break;
                    }

                    float extracted_val;
                    if (extracted_2bits_ == 3)
                    {
                        byte[] raw = BitConverter.GetBytes(processed_val);
                        extracted_val = BitConverter.ToSingle(raw, 0);
                    }
                    else
                        extracted_val = (float) processed_val * scaling_val;

                    float_acc[idx] += extracted_val;
                    entries.Add(float_acc[idx]);
                }
            }

            tgt_buf = entries.ToArray();
            return data_src.BaseStream.Position;
        }
        MemoryStream geoUnpack(DeltaPack a1)
        {
            if (0==a1.uncomp_size)
                return null;

            if (a1.compressed_size!=0)
            {
                byte[] unpacked = null;
                Tools.DecompressData(a1.compressed_data, out unpacked,a1.uncomp_size);
                return new MemoryStream(unpacked);
            }
            return a1.compressed_data;
        }
        void geoUnpackDeltas<T>(DeltaPack src, T[] target, uint entry_size, uint num_entries)
        {
            if (0 == src.uncomp_size)
                return;
            long consumed_bytes;
            MemoryStream src_stream = geoUnpack(src);
            
            if (typeof(T) == typeof(Vector2) || typeof(T) == typeof(Vector3))
            {
                float[] result;
                consumed_bytes = unpackedDeltaPack(out result, src_stream, entry_size, num_entries);
                if (typeof(T) == typeof(Vector2))
                {
                    Vector2[] tgt_cast = target as Vector2[]; 
                    for (int entry_idx = 0; entry_idx < num_entries; ++entry_idx)
                    {
                        tgt_cast[entry_idx][0] = result[entry_idx * 2 + 0];
                        tgt_cast[entry_idx][1] = result[entry_idx * 2 + 1];
                    }
                }
                else if (typeof(T) == typeof(Vector3))
                {
                    Vector3[] tgt_cast = target as Vector3[]; 
                    for (int entry_idx = 0; entry_idx < num_entries; ++entry_idx)
                    {
                        tgt_cast[entry_idx][0] = result[entry_idx * 3 + 0];
                        tgt_cast[entry_idx][1] = result[entry_idx * 3 + 1];
                        tgt_cast[entry_idx][2] = result[entry_idx * 3 + 2];
                    }
                }
            }
            else if (typeof(T) == typeof(Vector3Int))
            {
                int[] result;
                consumed_bytes = unpackedDeltaPack(out result, src_stream, entry_size, num_entries);
                Vector3Int[] tgt_cast = target as Vector3Int[]; 
                for (int entry_idx = 0; entry_idx < num_entries; ++entry_idx)
                {
                    tgt_cast[entry_idx][0] = result[entry_idx * 3 + 0];
                    tgt_cast[entry_idx][1] = result[entry_idx * 3 + 1];
                    tgt_cast[entry_idx][2] = result[entry_idx * 3 + 2];
                }
            }
        }
        void geoUnpackDeltas(DeltaPack src, Vector3[] unpacked_data, uint num_entries)
        {
            geoUnpackDeltas(src, unpacked_data, 3, num_entries);
        }
        void geoUnpackDeltas(DeltaPack src, Vector2[] unpacked_data, uint num_entries)
        {
            geoUnpackDeltas(src, unpacked_data, 2, num_entries);
        }

        void geoUnpackDeltas(DeltaPack src, Vector3Int[] unpacked_data, uint num_entries)
        {
            geoUnpackDeltas(src, unpacked_data, 3, num_entries);
        }
        VBOPointers fillVbo()
        {
            VBOPointers vbo = new VBOPointers();
            vbo.triangles = new Vector3Int[model_tri_count];
            Vector3Int[] triangles = vbo.triangles;
            geoUnpackDeltas(packed_data.tris, triangles, model_tri_count);
            uint total_size = 0;
            uint Vertices3D_bytes = 12 * vertex_count;
            total_size += Vertices3D_bytes;
            if (packed_data.norms.uncomp_size!=0)
                total_size += Vertices3D_bytes;
            if (packed_data.sts.uncomp_size!=0)
                total_size += 2 * 8 * vertex_count;

            vbo.pos = new Vector3[vertex_count];

            geoUnpackDeltas(packed_data.verts, vbo.pos, vertex_count);
            if (packed_data.norms.uncomp_size!=0)
            {
                vbo.norm = new Vector3[vertex_count];
                geoUnpackDeltas(packed_data.norms, vbo.norm, vertex_count);
            }

            if (packed_data.sts.uncomp_size!=0)
            {
                vbo.uv1 = new Vector2[vertex_count];
                geoUnpackDeltas(packed_data.sts, vbo.uv1, 2, vertex_count);
                vbo.uv2 = vbo.uv1;
            }

            if (hasBoneWeights())
            {
                vbo.bone_weights = new Vector2[vertex_count];
                vbo.bone_indices = new List<Tuple<ushort, ushort>>();//.resize(this.vertex_count);
                BinaryReader weight_stream = new BinaryReader(geoUnpack(packed_data.weights));
                BinaryReader bi_stream = new BinaryReader(geoUnpack(packed_data.matidxs));
                for (int i = 0; i < vertex_count; ++i)
                {
                    vbo.bone_indices[i] = new Tuple<ushort,ushort>(bi_stream.ReadByte(),bi_stream.ReadByte());
                    vbo.bone_weights[i].x = (weight_stream.ReadByte() / 255.0f);
                    vbo.bone_weights[i].y = 1.0f - vbo.bone_weights[i].x;
                }
            }

            if (bumpMapped())
            {
                vbo.needs_tangents = true;
            }

            return vbo;
        }

        private bool bumpMapped()
        {
            return flags.HasFlag(ModelFlags.OBJ_DRAW_AS_ENT | ModelFlags.OBJ_BUMPMAP);
        }

        public UnityModel modelCreateObjectFromModel(List<TextureWrapper> modelTextures)
        {
            initLoadedModel(modelTextures);
            vbo = fillVbo();
            vbo.assigned_textures.Clear();
            foreach(TextureBind tbind in texture_bind_info)
            {
                vbo.assigned_textures.Add(modelTextures[tbind.tex_idx]);
            }

            var fromScratchModel = new UnityModel();
            fromScratchModel.m_mesh = new Mesh();
            fromScratchModel.m_mesh.vertices = vbo.pos;
            if(vbo.norm!=null)
                fromScratchModel.m_mesh.normals = vbo.norm;
            if(vbo.uv1!=null)
                fromScratchModel.m_mesh.uv = vbo.uv1;
            if(vbo.needs_tangents)
                fromScratchModel.m_mesh.RecalculateTangents();
            fromScratchModel.m_mesh.RecalculateBounds();
            int geom_count = texture_bind_info.Count;
            fromScratchModel.m_mesh.subMeshCount = geom_count;
            int face_offset=0;
            for(int i=0; i<geom_count; ++i)
            {
                TextureBind tbind = texture_bind_info[i];
                int[] geom_triangles = new int[tbind.tri_count*3];
                for (int vi = 0; vi < tbind.tri_count; ++vi)
                {
                    Vector3Int tri = vbo.triangles[face_offset + vi];
                    geom_triangles[3 * vi] = tri[0];
                    geom_triangles[3 * vi+1] = tri[1];
                    geom_triangles[3 * vi+2] = tri[2];
                }
                fromScratchModel.m_mesh.SetTriangles(geom_triangles,i);
                face_offset+=tbind.tri_count;
            }
            return fromScratchModel;
        }
    }
}