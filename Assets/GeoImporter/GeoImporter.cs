using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using SEGSRuntime;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine.Rendering;
using Tools = SEGSRuntime.Tools;

namespace SEGSRuntime
{
}

[ScriptedImporter(1, "bin")]
public class GEOImporter : ScriptedImporter
{
    private const string PREFAB_DESTINATION_DIRECTORY = "Assets/Prefabs/";
    private static bool m_RuntimeReady = false;
    internal SceneGraph sg;
    private Dictionary<SceneNode, GameObject> m_imported_prefabs = new Dictionary<SceneNode, GameObject>();
    private static readonly int Cull = Shader.PropertyToID("_Cull");

    private static GameObject CreatePrefabFromModel(string tgt_path, GameObject modelAsset)
    {
        
        Tools.EnsureDirectoryExists(tgt_path);

        string destinationPath = tgt_path + "/" + modelAsset.name + ".prefab";
        //Debug.LogFormat("Prefab for model path '{0}'",destinationPath);
        
        if (File.Exists(destinationPath))
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath);
        }
        PrefabUtility.SaveAsPrefabAssetAndConnect(modelAsset, destinationPath, InteractionMode.AutomatedAction);
        return modelAsset;
    }
    
    GameObject convertFromRoot(SceneNode n)
    {
        GameObject res;
        var path = n.m_src_bin;
        string target_directory = buildNodePrefabPath(n);
        if (sg.isInternalNode(n) == NodeState.UsedAsPrefab && m_imported_prefabs.ContainsKey(n))
        {
            res = null;
        }
        else
        {
            res = new GameObject(n.m_name);
        }

        if (n.m_model != null && res)
        {
            convertModel(n, res, true);
        }

        switch (sg.isInternalNode(n))
        {
            case NodeState.RootNode:
            case NodeState.InternalNode:
                convertChildren(n, path, res);
                break;
            case NodeState.UsedAsPrefab:
                if (!m_imported_prefabs.ContainsKey(n))
                {
                    Debug.LogFormat("Prefab path is {0} tgt {1}",n.m_src_bin,target_directory);
                    convertChildren(n, path, res);
                    res = CreatePrefabFromModel(target_directory, res);
                    m_imported_prefabs[n] = res;
                }

                res = Instantiate(m_imported_prefabs[n]);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return res;
    }

    private static string buildNodePrefabPath(SceneNode n)
    {
        string path = n.m_src_bin;
        int idx = path.LastIndexOf("geobin");
        return PREFAB_DESTINATION_DIRECTORY + path.Substring(idx);
    }

    private static void convertModel(SceneNode n, GameObject res, bool convert_editor_markers)
    {
        string mesh_path;
        string model_path;
        int obj_lib_idx;

        Model mdl = n.m_model;
        ModelModifiers model_trick = mdl.trck_node;
        mesh_path = mdl.geoset.full_geo_path;
        obj_lib_idx = mesh_path.IndexOf("object_library");
        if (obj_lib_idx != -1)
            mesh_path = "Assets/Meshes/" + mesh_path.Substring(obj_lib_idx);
        model_path = mesh_path + "/" + Path.GetFileNameWithoutExtension(mdl.name) + ".asset";

        MeshFilter mf = res.AddComponent<MeshFilter>();
        MeshRenderer ren = res.AddComponent<MeshRenderer>();
        ModelNodeMods sup = res.AddComponent<ModelNodeMods>();
        if (model_trick != null)
        {
            sup.ModelMod = model_trick;
        }

        //mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(model_path);
        if (mf.sharedMesh == null)
        {
            if (model_trick != null)
            {
                if (!convert_editor_markers && model_trick._TrickFlags.HasFlag(TrickFlags.NoDraw))
                {
                    //qDebug() << mdl->name << "Set as no draw";
                    return;
                }

                if (!convert_editor_markers && model_trick._TrickFlags.HasFlag(TrickFlags.EditorVisible))
                {
                    //qDebug() << mdl.name << "Set as editor model";
                    return;
                }

                if (model_trick._TrickFlags.HasFlag(TrickFlags.CastShadow))
                {
                    //qDebug() << "Not converting shadow models"<<mdl.name;
                    return;
                }

                if (model_trick._TrickFlags.HasFlag(TrickFlags.ParticleSys))
                {
                    Debug.Log("Not converting particle sys:" + mdl.name);
                    return;
                }
            }

            if (!mdl.geoset.data_loaded)
            {
                mdl.geoset.LoadData();
                if (mdl.geoset.subs.Count != 0)
                    mdl.geoset.createEngineModelsFromPrefabSet();
            }

            UnityModel res_static;
            if (!GeoSet.s_coh_model_to_engine_model.TryGetValue(mdl, out res_static))
                return;
            if (res_static == null)
                return;
            Tools.EnsureDirectoryExists(mesh_path);
            AssetDatabase.CreateAsset(res_static.m_mesh, model_path);
            AssetDatabase.SaveAssets();
            mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(model_path);
        }

        convertMaterials(ren, mdl);
    }

    private static void convertMaterials(MeshRenderer ren, Model mdl)
    {
        ModelModifiers model_trick = mdl.trck_node;
        Material result; 
        string model_base_name = mdl.name.Split(new string[] {"__"}, StringSplitOptions.None)[0];
        bool isDoubleSided = model_trick != null && model_trick.isFlag(TrickFlags.DoubleSided);
        string mesh_path = mdl.geoset.full_geo_path;
        int obj_lib_idx = mesh_path.IndexOf("object_library");
        if (obj_lib_idx != -1)
            mesh_path = "Assets/Materials/" + mesh_path.Substring(obj_lib_idx);
        string material_base_path = mesh_path + "/" + Path.GetFileNameWithoutExtension(mdl.name);

        if (model_trick != null && model_trick.isFlag(TrickFlags.ColorOnly))
        {
//        result = result.Clone(result.GetName()+"Colored");
//        result.SetShaderParameter("MatDiffColor",Vector4(1.0, 1.0, 0.2f, 1.0f));
        }

        // Select material based on the model blend state
        // Modify material based on the applied model tricks
        Color onlyColor;
        Color tint1 = new Color(1, 1, 1, 1); // Shader Constant 0
        Color tint2 = new Color(1, 1, 1, 1); // Shader Constant 1
        CullMode targetCulling = CullMode.Back;//CULL_CCW;
         /*
         
         CompareMode depthTest = CMP_LESSEQUAL;
         */
        float alphaRef = 0.0f;
        bool depthWrite = true;
        bool isAdditive=false;
        if(null!=model_trick && model_trick._TrickFlags!=0)
        {
            var tflags = model_trick._TrickFlags;
            if ( tflags.HasFlag(TrickFlags.Additive) )
            {
                isAdditive = true;
            }
            if ( tflags.HasFlag(TrickFlags.ColorOnly) )
                onlyColor = model_trick.TintColor0;
            if ( tflags.HasFlag(TrickFlags.DoubleSided) )
                targetCulling = CullMode.Off;
            if ( tflags.HasFlag(TrickFlags.NoZTest) )
            {
                // simulate disabled Z test
                //depthTest = CMP_ALWAYS;
                depthWrite = false;
            }
            if ( tflags.HasFlag(TrickFlags.NoZWrite) )
                depthWrite = false;
            if ( tflags.HasFlag(TrickFlags.SetColor) )
            {
                tint1=model_trick.TintColor0;
                tint2=model_trick.TintColor1;
                tint1.a = 1.0f;
                tint2.a = 1.0f;
            }
            if ( tflags.HasFlag(TrickFlags.ReflectTex0 | TrickFlags.ReflectTex1) ) {
                Debug.Log("Unhandled cubemap reflection");
            }
            if ( tflags.HasFlag(TrickFlags.AlphaRef) ) {
                //qDebug() << "Unhandled alpha ref";
                alphaRef = model_trick.info.AlphaRef;
            }
            if ( tflags.HasFlag(TrickFlags.TexBias) )
                Debug.Log("Unhandled TexBias");
        }
        var vertex_defines = new List<string>();
        var pixel_defines = new List<string>();
        Material preconverted=null;
        /*
        HTexture whitetex = tryLoadTexture(ctx,"white.tga");
        
        Material preconverted = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials");
        if(cache.Exists("./converted/Materials/"+model_base_name+"_mtl.xml"))
            preconverted = cache.GetResource<Material>("./converted/Materials/"+model_base_name+"_mtl.xml");
        bool noLightAngle= mdl.flags & SEGSRuntime.OBJ_NOLIGHTANGLE;
        if(!preconverted) {
            if(mdl.flags&SEGSRuntime.OBJ_TREE)
            {
                preconverted = cache.GetResource<Material>("Materials/DefaultVegetation.xml").Clone();
            }
            else {
                if(noLightAngle)
                    preconverted = cache.GetResource<Material>("Materials/NoLightAngle.xml").Clone();
                else
                    preconverted = cache.GetResource<Material>("Materials/TexturedDual.xml").Clone();

            }
        }
        else {
            preconverted=preconverted.Clone();
        }
        pixel_defines.Add("DIFFMAP");
        pixel_defines.Add("ALPHAMASK");
        */
            switch(mdl.blend_mode)
    {
 case CoHBlendMode.MULTIPLY:
     pixel_defines.Add("COH_MULTIPLY");
     break;
 case CoHBlendMode.MULTIPLY_REG:
     if(!depthWrite && isAdditive) {
         preconverted = new Material(Shader.Find("Unlit/Additive"));    
         preconverted.SetInt(Cull, (int)UnityEngine.Rendering.CullMode.Off);
     }
     pixel_defines.Add("COH_MULTIPLY");
     break;
 case CoHBlendMode.COLORBLEND_DUAL:
     pixel_defines.Add("COH_COLOR_BLEND_DUAL");
     break;
 case CoHBlendMode.ADDGLOW:
     pixel_defines.Add("COH_ADDGLOW");
     break;
 case CoHBlendMode.ALPHADETAIL:
     pixel_defines.Add("COH_ALPHADETAIL");
     break;
 case CoHBlendMode.BUMPMAP_MULTIPLY:
     //preconverted = cache.GetResource<Material>("Materials/TexturedDualBump.xml").Clone();
     pixel_defines.Add("COH_MULTIPLY");
     break;
 case CoHBlendMode.BUMPMAP_COLORBLEND_DUAL:
     //preconverted = cache.GetResource<Material>("Materials/TexturedDualBump.xml").Clone();
     pixel_defines.Add("COH_COLOR_BLEND_DUAL");
     break;
 }/*
 if(model_trick!=null && model_trick.isFlag(TrickFlags.SetColor)) {
     reportUnhandled("SetColor unhandled");
 }
 if(mdl.flags.HasFlag(ModelFlags.OBJ_TREE)) {
     preconverted.SetVertexShaderDefines("TRANSLUCENT");
     pixel_defines<<"TRANSLUCENT";
     preconverted.SetShaderParameter("AlphaRef",0.4f);
     tint1.a = 254.0f/255.0f;
 }
 else if(alphaRef!=0.0f)
     preconverted.SetShaderParameter("AlphaRef",alphaRef);
 preconverted.SetShaderParameter("Col1",tint1);
 preconverted.SetShaderParameter("Col2",tint2);
 preconverted.SetPixelShaderDefines(pixel_defines.join(' '));
 if(isDoubleSided)
     preconverted.SetCullMode(CULL_NONE);

 // int mode= dualTexture ? 4 : 5
 uint geomidx=0;
 bool is_single_mat = mdl.texture_bind_info.Count == 1;
 */
        Material[] materials = new Material[mdl.texture_bind_info.Count];
        int idx = 0;
        Tools.EnsureDirectoryExists(material_base_path);
        
        foreach (var texbind in mdl.texture_bind_info)
        {
            string path_material_name = String.Format("{0}/{1}.mat", material_base_path, idx);
            Material available = AssetDatabase.LoadAssetAtPath<Material>(path_material_name);
            if (available == null)
            {
                string texname = mdl.geoset.tex_names[texbind.tex_idx];
                TextureWrapper wrap = GeoSet.loadTexHeader(texname);
                if (wrap != null)
                {
                    Material mat;
                    if (preconverted != null)
                    {
                        mat = preconverted;
                    }
                    else
                    {
                        if (isAdditive)
                        {
                            mat = new Material(Shader.Find("Unlit/Additive"));
                            mat.SetColor("_Color",tint1);                    
                        }
                        else
                            mat = new Material(Shader.Find("Diffuse Detail"));                        
                    }
                        
                    mat.SetTexture("_MainTex", wrap.tex);
                    mat.SetTextureScale("_MainTex",wrap.scaleUV0);
                    if (wrap.detailname != null)
                    {
                        TextureWrapper detail = GeoSet.loadTexHeader(wrap.detailname);
                        if (null!=detail)
                        {
                            mat.SetTexture("_Detail", detail.tex);
                            mat.SetTextureScale("_Detail",wrap.scaleUV0);
                        }
                        
                    }
                    AssetDatabase.CreateAsset(mat,path_material_name);
                    AssetDatabase.SaveAssets();
                    available = AssetDatabase.LoadAssetAtPath<Material>(path_material_name);
                }
            }

            materials[idx++] = available;
            /*
            HTexture tex = tryLoadTexture(ctx,texname);
            auto iter = g_converted_textures.find(tex.idx);
            geomidx++;
            if(iter==g_converted_textures.end())
                continue;
            string mat_name = string.Format("Materials/{0}{1}_mtl.xml",model_base_name,geomidx-1);
            Material cached_mat = cache.GetResource<Material>(mat_name,false);
            Urho3D.SharedPtr<Urho3D.Texture> &engine_tex(iter.second);
            if(cached_mat && cached_mat.GetTexture(TU_DIFFUSE)==engine_tex)
            {
                boxObject.SetMaterial(geomidx-1,cached_mat);
                continue;
            }
            result = is_single_mat ? preconverted : preconverted.Clone();
            result.SetTexture(TU_DIFFUSE,engine_tex);
            HTexture custom1 = whitetex;
            HTexture bump_tex = {};
            if(tex.info)
            {
                if(!tex.info.Blend.isEmpty())
                    custom1 = tryLoadTexture(ctx,tex.info.Blend);
                if(!tex.info.BumpMap.isEmpty())
                    bump_tex = tryLoadTexture(ctx,tex.info.BumpMap);
            }
            result.SetTexture(TU_CUSTOM1,g_converted_textures[custom1.idx]);
            if(bump_tex)
                result.SetTexture(TU_NORMAL,g_converted_textures[bump_tex.idx]);
    
            QDir modeldir("converted/");
            bool created = modeldir.mkpath("Materials");
            assert(created);
            QString cache_path="./converted/"+mat_name;
            File mat_res(ctx, cache_path, FILE_WRITE);
            result.SetName(mat_name);
            result.Save(mat_res);
            cache.AddManualResource(result);
            boxObject.SetMaterial(geomidx-1,cache.GetResource<Material>(mat_name));
            */
        }

        ren.sharedMaterials = materials;
    }

    private void convertChildren(SceneNode n, string path, GameObject res)
    {
        foreach (SceneNodeChildTransform child in n.m_children)
        {
            GameObject go = convertFromRoot(child.node);
            go.transform.parent = res.transform;
            go.transform.localScale = child.m_matrix2.lossyScale;
            go.transform.rotation = child.m_matrix2.rotation;
            go.transform.position = new Vector3(child.m_matrix2.m03, child.m_matrix2.m13, child.m_matrix2.m23);
        }
    }

    public override void OnImportAsset(AssetImportContext ctx)
    {
        SEGSRuntime.Tools.EnsureDirectoryExists(PREFAB_DESTINATION_DIRECTORY);
        int idx = ctx.assetPath.LastIndexOf("geobin");
        if (-1 == idx)
        {
            ctx.LogImportWarning("File is not located under 'geobin', skipping");
            return;
        }

        if (!m_RuntimeReady)
        {
            RuntimeData rd = RuntimeData.get();
            string basepath = ctx.assetPath.Substring(0, idx - 1);
            if (!basepath.EndsWith("/") && basepath[basepath.Length - 1] != Path.DirectorySeparatorChar)
                basepath += Path.DirectorySeparatorChar;
            rd.prepare(basepath);                           
        }
        GeoSet.s_coh_model_to_engine_model.Clear();
        Debug.Log("Map load commencing");
        sg = SceneGraph.loadWholeMap(ctx.assetPath);
        Debug.Log("Map load done");
        var top_nodes = sg.calculateUsages();
        if (top_nodes.Count != 0)
        {
            //string target_directory = PREFAB_DESTINATION_DIRECTORY + ctx.assetPath.Substring(idx);
            foreach (var pair in top_nodes)
            {
                string target_directory = buildNodePrefabPath(pair.Value);
                Tools.EnsureDirectoryExists(target_directory);
                
                GameObject top_level = convertFromRoot(pair.Value);
                CreatePrefabFromModel(target_directory, top_level);
            }

            foreach (KeyValuePair<SceneNode, GameObject> pair in m_imported_prefabs)
            {
                if (!top_nodes.ContainsValue(pair.Key))
                    GameObject.DestroyImmediate(pair.Value); // remove all non-top-level prefabs from scene.
            }
        }
    }
}

public class CoHPostProcessor : AssetPostprocessor
{
    private const string PREFAB_DESTINATION_DIRECTORY = "Assets/Prefabs/";

    private static bool m_RuntimeReady = false;

    void OnPreprocessAsset()
    {
        if (assetImporter.importSettingsMissing)
        {
            GEOImporter modelImporter = assetImporter as GEOImporter;
            if (modelImporter != null)
            {
                Debug.Log("Correct importer instance selected");
                if (modelImporter.sg != null)
                    Debug.Log("SEGS scene graph available");
            }
        }
    }

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (null != importedAssets)
        {
            foreach (string asset in importedAssets)
            {
                if (!asset.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                    continue;
                Debug.LogFormat("Imported {0}", asset);
                if (asset.ToLower().Contains("geobin"))
                {
                    //Debug.LogFormat("Remove geobin source ? {0}",AssetDatabase.DeleteAsset(asset));
                }
            }
        }
    }
}