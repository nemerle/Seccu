using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;


internal class TexFileHdr
{
    public int     header_size;
    public int     file_size;
    public int     wdth;
    public int     hght;
    public int     flags;
    public int     fade_1;
    public int     fade_2;
    public byte    alpha;
    public byte[]   magic;//3
};

[ScriptedImporter(1, "texture")]
public class TexImporter : ScriptedImporter
{
    private const string PREFAB_DESTINATION_DIRECTORY = "Assets/Resources/";
    private TexFileHdr readHeader(BinaryReader br)
    {
        TexFileHdr res=new TexFileHdr();
        res.header_size = br.ReadInt32();
        res.file_size = br.ReadInt32();
        res.wdth = br.ReadInt32();
        res.hght = br.ReadInt32();
        res.flags = br.ReadInt32();
        res.fade_1 = br.ReadInt32();
        res.fade_2 = br.ReadInt32();
        res.alpha = br.ReadByte();
        res.magic = br.ReadBytes(3);
        return res;
    }
    public override void OnImportAsset(AssetImportContext ctx)
    {
        
        int idx = ctx.assetPath.LastIndexOf("texture_library");
        FileStream fs = File.Open(ctx.assetPath, FileMode.Open);
        BinaryReader fs_reader = new BinaryReader(fs, new ASCIIEncoding());
        TexFileHdr hdr = readHeader(fs_reader);
        if (hdr.magic[0] != 'T' || hdr.magic[1] != 'X' || hdr.magic[2] != '2')
        {
            ctx.LogImportError("Unknown texture format");
            return;
        }

        var name_chars = fs_reader.ReadChars(hdr.header_size - 8 * 4);
        string originalname = new string(name_chars);
        originalname = originalname.Substring(0, originalname.IndexOf('\0'));
        SEGSRuntime.Tools.EnsureDirectoryExists(Path.GetDirectoryName(PREFAB_DESTINATION_DIRECTORY + originalname));
        FileStream tgt = File.Open(PREFAB_DESTINATION_DIRECTORY + originalname,FileMode.Create);
        fs.CopyTo(tgt);
        tgt.Close();
        fs.Close();
    }
}
public class TexturePostprocessor : AssetPostprocessor
{
    
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets, 
        string[] movedAssets, 
        string[] movedFromAssetPaths )
    {
        if (null != importedAssets)
        {
            foreach (string asset in importedAssets)
            {
                if( !asset.EndsWith( ".texture", StringComparison.OrdinalIgnoreCase ) )
                    continue;                
                //Debug.LogFormat("Removal ? {0}",AssetDatabase.DeleteAsset(asset));
            }
        }
    }
}