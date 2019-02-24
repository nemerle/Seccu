using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;

namespace SEGSRuntime
{
    internal class BinStore // binary storage
    {
        class FileEntry {
            public string name;
            public uint date=0;
        };

        private Stream m_understream;
        private BinaryReader m_str;
        ulong bytes_read=0;
        uint bytes_to_read=0;
        List<uint> m_file_sizes = new List<uint>(); // implicit stack
        List<FileEntry> m_entries = new List<FileEntry>();

        bool canRead(uint bcount)
        {
            if(m_file_sizes.Count>0 && current_fsize()<bcount)
                return false;
            return (m_understream.Length-m_understream.Position)>=bcount;
        }

        void updateAfterRead(uint bcount)
        {
            if(m_file_sizes.Count>0)
            {
                bytes_read+=bcount;
                m_file_sizes[m_file_sizes.Count-1]-=bcount;
            }
        }
        string read_pstr(ulong maxlen)
        {
            ushort len=0;
            if(read(out len)!=true)
                return null;
            if(len<=maxlen)
            {
                char[] buf = m_str.ReadChars(len);
                if(m_file_sizes.Count>0)
                {
                    m_file_sizes[m_file_sizes.Count-1]-=len;
                    bytes_read+=len;
                }
                fixup();
                return new string(buf);
            }
            return null;
        }
        void    skip_pstr()
        {
            ushort len=0;
            read(out len);
            m_understream.Seek(len, SeekOrigin.Current);
        }
        bool    read_data_blocks(bool file_data_blocks)
        {
            if(!file_data_blocks)
            {
                skip_pstr();
                uint v;
                read(out v);
                if(v!=0)
                    m_understream.Seek(v,SeekOrigin.Current);
                return true;
            }
            string hdr = read_pstr(20);
            uint sz;
            read(out sz);

            long read_start = m_understream.Position;
            if(!hdr.StartsWith("Files1")||sz<=0)
                return false;
            int num_data_blocks;
            read(out num_data_blocks);
            for (int blk_idx=0; blk_idx<num_data_blocks; ++blk_idx)
            {
                FileEntry fe=new FileEntry();
                fe.name = read_pstr(260);
                read(out fe.date);
                m_entries.Add(fe);
            }
            uint read_end = (uint)m_understream.Position;
            m_file_sizes.Add((uint)m_understream.Length-read_end);
            return (sz==(read_end-read_start));
        }
        bool    check_bin_version_and_crc(uint req_crc)
        {
            string tgt;
            uint crc_from_file;
            string magic_contents = System.Text.Encoding.ASCII.GetString(m_str.ReadBytes(8));
            read(out crc_from_file);
            tgt=read_pstr(4096);

            if( !magic_contents.StartsWith("CrypticS") || tgt.Substring(0,6)!="Parse4" || (req_crc!=0 && crc_from_file != req_crc) ) //
            {
                m_understream.Close();
                return false;
            }
            return true;
        }
        uint    current_fsize() {return m_file_sizes[m_file_sizes.Count-1];}
        uint    read_header(out string name, ulong maxlen)
        {
            name = read_pstr(maxlen);
            uint res;
            if(!read(out res))
                return ~0U;
            if(res==0)
                return ~0U;
            return res;
        }
        void fixup()
        {
            byte nonmult4 = (byte)(((m_understream.Position + 3) & ~3) - m_understream.Position);
            if(nonmult4!=0)
            {
                m_understream.Seek(nonmult4, SeekOrigin.Current);
                bytes_read+=nonmult4;
                if(m_file_sizes.Count>0)
                    m_file_sizes[m_file_sizes.Count-1]-=nonmult4;

            }
        }

        public bool read(out int v)
        {
            if (!canRead(sizeof(int)))
            {
                v = 0;
                return false;
            }
            v=m_str.ReadInt32();
            updateAfterRead(sizeof(int));
            return true;
        }
        public bool read(out uint v)
        {
            if (!canRead(sizeof(uint)))
            {
                v = 0;
                return false;
            }
            v=m_str.ReadUInt32();
            updateAfterRead(sizeof(uint));
            return true;
        }
        public bool read(out ushort v)
        {
            if (!canRead(sizeof(ushort)))
            {
                v = 0;
                return false;
            }
            v=m_str.ReadUInt16();
            updateAfterRead(sizeof(ushort));
            return true;
        }
        public bool read(out byte v)
        {
            if (!canRead(sizeof(byte)))
            {
                v = 0;
                return false;
            }
            v=m_str.ReadByte();
            updateAfterRead(sizeof(byte));
            return true;
        }
        public bool read(out float v)
        {
            if (!canRead(sizeof(float)))
            {
                v = 0;
                return false;
            }
            v=m_str.ReadSingle();
            updateAfterRead(sizeof(float));
            return true;
        }
        public bool read(out Vector2 v)
        {
            v = new Vector2();
            bool ok = true;
            ok &= read(out v.x);
            ok &= read(out v.y);
            return ok;
        }
        public bool read(out Vector3 v)
        {
            v = new Vector3();
            bool ok = true;
            ok &= read(out v.x);
            ok &= read(out v.y);
            ok &= read(out v.z);
            return ok;
        }
        public string     source_name() {
            return read_str(12000);
        }
        string read_str(ulong maxlen)
        {
            string result = read_pstr(maxlen);
            fixup();
            return result;
        }
        public bool read(out Color32 rgb)
        {
            bool parse_ok=true;
            Color32 res = new Color32(0,0,0,0);
            parse_ok &= read(out res.r);
            parse_ok &= read(out res.g);
            parse_ok &= read(out res.b);
            res.a = 0;
            byte skipped;
            read(out skipped);
            rgb = res;
            return parse_ok;
        }

        public bool read(out string val)
        {
            val=read_str(12000);
            return true;
        }

        public void prepare()
        {
            read(out bytes_to_read);
            bytes_read=0;
        }

        public bool prepare_nested()
        {
            bool result= bytes_to_read==bytes_read;
            Debug.Assert(bytes_to_read==bytes_read);
            bytes_to_read = m_file_sizes[m_file_sizes.Count-1];
            return result;
        }

        public bool nesting_name(out string name)
        {
            uint expected_size = read_header(out name,12000);
            if(expected_size == ~0U)
                return false;
            bytes_to_read = expected_size;
            if(m_file_sizes.Count>0)
                m_file_sizes[m_file_sizes.Count-1]-=bytes_to_read;
            m_file_sizes.Add(bytes_to_read); // the size of structure being read. + sizeof(uint32_t)
            return true;
        }
        public void        nest_in() {  }
        public void nest_out()
        {
            m_file_sizes.RemoveAt(m_file_sizes.Count - 1); 
        }

        public bool end_encountered()
        {
            return (m_file_sizes[m_file_sizes.Count-1])==0;
        }
        public bool open(string name, uint required_crc)
        {
            if(m_str==null || null==m_understream || !m_understream.CanRead)
            {
                m_understream =  File.Open(name.Replace('/',Path.DirectorySeparatorChar),FileMode.Open);
                if(m_understream==null)
                    return false;
                m_str = new BinaryReader(m_understream,new ASCIIEncoding());
            }
            bool result = check_bin_version_and_crc(required_crc);
            return result && read_data_blocks(true);
        }
    };
}
