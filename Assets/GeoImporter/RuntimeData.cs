using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SEGSRuntime {
    internal class RuntimeData 
{
    public PrefabStore m_prefab_mapping = null;
    public SceneModifiers m_modifiers = null;
    public Dictionary<string,TextureWrapper> m_loaded_textures=new Dictionary<string,TextureWrapper>();
    const uint tricks_i0_requiredCrc = 0xB46B669E;
    public Dictionary<string, GeoSet> s_name_to_geoset = new Dictionary<string, GeoSet>();
    public Dictionary<Model, UnityModel> s_coh_model_to_engine_model = new Dictionary<Model, UnityModel>();

    private static RuntimeData s_instance = null;
    public RuntimeData() {
        s_instance = this;
    }
    public bool prepare(string directory_path)
    {
        if(!read_prefab_definitions(directory_path))
            return false;
        if(!read_model_modifiers(directory_path))
            return false;
        Debug.Log("CoH runtime data prepared");
        return true;
    }
    bool read_data_to(string directory_path,string storage,SceneModifiers target,uint CRC)
    {
        BinStore bin_store=new BinStore();
        if(!bin_store.open(directory_path+storage,CRC))
        {
            Debug.Log("failure");
            Debug.LogWarning("Couldn't load " + storage + " from " + directory_path);
            Debug.LogWarning("Using piggtool, ensure that bin.pigg has been extracted to ./data/");
            return false;
        }

        bool res=target.loadFrom(bin_store);
        bin_store.close();

        if(!res)
        {
            Debug.Log("Reading " + directory_path +" " + storage + " ... failure");
            Debug.LogWarning("Couldn't load " + directory_path+" " +storage+": wrong file format?");
        }
        
        return res;
    }

    private bool read_model_modifiers(string directory_path)
    {
        //if(null!=m_modifiers)
        //    return true;
        SceneModifiers tricks_store=new SceneModifiers();
        if(!read_data_to(directory_path,"bin/tricks.bin",tricks_store,tricks_i0_requiredCrc))
        {
            return false;
        }
        m_modifiers = tricks_store;
        m_modifiers.trickLoadPostProcess();
        return true;
    }

    private bool read_prefab_definitions(string directory_path)
    {
        //if(null==m_prefab_mapping)
            m_prefab_mapping = new PrefabStore(directory_path);
        return m_prefab_mapping.prepareGeoLookupArray(directory_path);
    }

    public static RuntimeData get()
    {
        return s_instance;
    }
}
}