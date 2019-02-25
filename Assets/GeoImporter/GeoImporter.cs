using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using SEGSRuntime;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Tools = SEGSRuntime.Tools;
using System.Text;

namespace SEGSRuntime
{
}

static public class GameObjectTypeLogging
{
    static public void LogStageInformation(GameObject go)
    {
        // First check if input GameObject is persistent before checking what stage the GameObject is in 
        if (EditorUtility.IsPersistent(go))
        {
            if (!PrefabUtility.IsPartOfPrefabAsset(go))
            {
                Debug.Log(
                    "The GameObject is a temporary object created during import. OnValidate() is called two times with a temporary object during import: First time is when saving cloned objects to .prefab file. Second event is when reading .prefab file objects during import");
            }
            else
            {
                Debug.Log("GameObject is part of an imported Prefab Asset (from the Library folder)");
            }

            return;
        }

        // If the GameObject is not persistent let's determine which stage we are in first because getting Prefab info depends on it
        var mainStage = StageUtility.GetMainStageHandle();
        var currentStage = StageUtility.GetStageHandle(go);
        if (currentStage == mainStage)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(go))
            {
                var type = PrefabUtility.GetPrefabAssetType(go);
                var path = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(go));
                Debug.Log(string.Format(
                    "GameObject is part of a Prefab Instance in the MainStage and is of type: {0}. It comes from the prefab asset: {1}",
                    type, path));
            }
            else
            {
                Debug.Log("GameObject is a plain GameObject in the MainStage");
            }
        }
        else
        {
            var prefabStage = PrefabStageUtility.GetPrefabStage(go);
            if (prefabStage != null)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    var type = PrefabUtility.GetPrefabAssetType(go);
                    var nestedPrefabPath =
                        AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(go));
                    Debug.Log(string.Format(
                        "GameObject is in a PrefabStage. The GameObject is part of a nested Prefab Instance and is of type: {0}. The opened Prefab asset is: {1} and the nested Prefab asset is: {2}",
                        type, prefabStage.prefabAssetPath, nestedPrefabPath));
                }
                else
                {
                    var prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabStage.prefabAssetPath);
                    var type = PrefabUtility.GetPrefabAssetType(prefabAssetRoot);
                    Debug.Log(string.Format(
                        "GameObject is in a PrefabStage. The opened Prefab is of type: {0}. The GameObject comes from the prefab asset: {1}",
                        type, prefabStage.prefabAssetPath));
                }
            }
            else if (EditorSceneManager.IsPreviewSceneObject(go))
            {
                Debug.Log(
                    "GameObject is not in the MainStage, nor in a PrefabStage. But it is in a PreviewScene so could be used for Preview rendering or other utilities.");
            }
            else
            {
                Debug.LogError("Unknown GameObject Info");
            }
        }
    }

    static public void LogPrefabInformation(GameObject go)
    {
        StringBuilder stringBuilder = new StringBuilder();

        // First check if input GameObject is persistent before checking what stage the GameObject is in 
        if (EditorUtility.IsPersistent(go))
        {
            if (!PrefabUtility.IsPartOfPrefabAsset(go))
            {
                stringBuilder.Append(
                    "The GameObject is a temporary object created during import. OnValidate() is called two times with a temporary object during import: First time is when saving cloned objects to .prefab file. Second event is when reading .prefab file objects during import");
            }
            else
            {
                stringBuilder.Append("GameObject is part of an imported Prefab Asset (from the Library folder).\n");
                stringBuilder.AppendLine("Prefab Asset: " + GetPrefabInfoString(go));
            }

            Debug.Log(stringBuilder.ToString());
            return;
        }

        PrefabStage prefabStage = PrefabStageUtility.GetPrefabStage(go);
        if (prefabStage != null)
        {
            GameObject openPrefabThatContentsIsPartOf =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabStage.prefabAssetPath);
            stringBuilder.AppendFormat(
                "The GameObject is part of the Prefab contents of the Prefab Asset:\n{0}\n\n",
                GetPrefabInfoString(openPrefabThatContentsIsPartOf));
        }

        if (!PrefabUtility.IsPartOfPrefabInstance(go))
        {
            stringBuilder.Append("The GameObject is a plain GameObject (not part of a Prefab instance).\n");
        }
        else
        {
            // This is the Prefab Asset that can be applied to via the Overrides dropdown.
            GameObject outermostPrefabAssetObject = PrefabUtility.GetCorrespondingObjectFromSource(go);
            // This is the Prefab Asset that determines the icon that is shown in the Hierarchy for the nearest root.
            GameObject nearestRootPrefabAssetObject =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go));
            // This is the Prefab Asset where the original version of the object comes from.
            GameObject originalPrefabAssetObject = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
            stringBuilder.AppendFormat(
                @"Prefab Asset of the outermost Prefab instance the input GameObject is part of is:
{0}
Prefab Asset of the nearest Prefab instance root the input GameObject is part of is:
{1}
Prefab Asset of the innermost Prefab instance the input GameObject is part of is:
{2}
Complete nesting chain from outermost to original:
",
                GetPrefabInfoString(outermostPrefabAssetObject),
                GetPrefabInfoString(nearestRootPrefabAssetObject),
                GetPrefabInfoString(originalPrefabAssetObject));

            GameObject current = outermostPrefabAssetObject;
            while (current != null)
            {
                stringBuilder.AppendLine(GetPrefabInfoString(current));
                current = PrefabUtility.GetCorrespondingObjectFromSource(current);
            }
        }

        stringBuilder.AppendLine("");

        Debug.Log(stringBuilder.ToString());
    }

    static string GetPrefabInfoString(GameObject prefabAssetGameObject)
    {
        string name = prefabAssetGameObject.transform.root.gameObject.name;
        string assetPath = AssetDatabase.GetAssetPath(prefabAssetGameObject);
        PrefabAssetType type = PrefabUtility.GetPrefabAssetType(prefabAssetGameObject);
        return string.Format("<b>{0}</b> (type: {1}) at '{2}'", name, type, assetPath);
    }
}

[ScriptedImporter(1, "bin")]
public class GEOImporter : ScriptedImporter
{
    private const string PREFAB_DESTINATION_DIRECTORY = "Assets/Prefabs/";
    private static bool m_RuntimeReady = false;
    internal SceneGraph sg;
    private Dictionary<SceneNode, GameObject> m_imported_prefabs = new Dictionary<SceneNode, GameObject>();
    private List<string> m_created_prefabs=new List<string>();

    private static GameObject InstantiateModelForEditing(GameObject model)
    {
        GameObject instance = GameObject.Instantiate(model);
        instance.name = model.name;
        return instance;
    }

    private static string GetSafeFilename(string filename)
    {
        return string.Join("_", filename.Split(new char[] {'?','+'}));
    }

    private static GameObject GetPrefabAsset(string tgt_path, string model_name)
    {
        string destinationPath = GetSafeFilename(tgt_path + "/" + model_name + ".prefab");

        if (!File.Exists(destinationPath)) 
            return null;
        
        var pfbsrc = AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath);

        if (pfbsrc == null)
            Debug.LogErrorFormat("Failed to read prefab data: {0}", destinationPath);

        return pfbsrc;

    }

    private GameObject CreatePrefabFromModel(string tgt_path, GameObject modelAsset)
    {
        Tools.EnsureDirectoryExists(tgt_path);

        string destinationPath = GetSafeFilename(tgt_path + "/" + modelAsset.name + ".prefab");
        var pfbsrc = GetPrefabAsset(tgt_path, modelAsset.name);
        if (pfbsrc != null)
            return pfbsrc;
        m_created_prefabs.Add(destinationPath);
        var pfb = PrefabUtility.SaveAsPrefabAsset(modelAsset, destinationPath);
        GameObject.DestroyImmediate(modelAsset);

        return null;
    }

    GameObject convertFromRoot(SceneNode n)
    {
        GameObject res = null;
        var target_directory = targetDirForNodePrefab(n);
        if (!m_imported_prefabs.ContainsKey(n))
        {
            if (sg.isInternalNode(n) != NodeState.InternalNode)
            {
                var prefab = GetPrefabAsset(target_directory, n.m_name);
                if (prefab != null)
                    return (GameObject) PrefabUtility.InstantiatePrefab(prefab);
            }

            res = new GameObject(n.m_name);
            if (n.m_model != null)
            {
                convertModel(n, res, true);
            }

            if (!convertChildren(n, res))
            {
                GameObject.DestroyImmediate(res);
                return null;
            }
        }

        switch (sg.isInternalNode(n))
        {
            case NodeState.InternalNode:
                break;
            case NodeState.RootNode:
            case NodeState.UsedAsPrefab:
                if (!m_imported_prefabs.ContainsKey(n))
                {
                    var res2 = CreatePrefabFromModel(target_directory, res);
                    m_imported_prefabs[n] = res2;
                }

                if (m_imported_prefabs[n] == null)
                    return null;

                var res3 = (GameObject) PrefabUtility.InstantiatePrefab(m_imported_prefabs[n]);
                GameObjectTypeLogging.LogPrefabInformation(res3);
                return res3;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return res;
    }

    private static string targetDirForNodePrefab(SceneNode n)
    {
        int idx = n.m_src_bin.LastIndexOf("geobin");
        string target_directory = PREFAB_DESTINATION_DIRECTORY + n.m_src_bin.Substring(idx);
        return target_directory;
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
        model_path = GetSafeFilename(mesh_path + "/" + Path.GetFileNameWithoutExtension(mdl.name) + ".asset");

        MeshFilter mf = res.AddComponent<MeshFilter>();
        MeshRenderer ren = res.AddComponent<MeshRenderer>();
        ModelNodeMods sup = res.AddComponent<ModelNodeMods>();
        if (model_trick != null)
        {
            sup.ModelMod = model_trick;
        }

        mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(model_path);
        if (model_trick != null)
        {
            if (model_trick._TrickFlags.HasFlag(TrickFlags.NoDraw))
            {
                res.tag = "NoDraw";
                res.layer = 9;
                ren.shadowCastingMode = ShadowCastingMode.Off;
            }

            if (model_trick._TrickFlags.HasFlag(TrickFlags.EditorVisible))
            {
                res.layer = 9;
                ren.shadowCastingMode = ShadowCastingMode.Off;
            }

            if (model_trick._TrickFlags.HasFlag(TrickFlags.CastShadow))
            {
                ren.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }

            if (model_trick._TrickFlags.HasFlag(TrickFlags.ParticleSys))
            {
                Debug.Log("Not converting particle sys:" + mdl.name);
                return;
            }
        }
        if (mf.sharedMesh == null)
        {

            if (!mdl.geoset.data_loaded)
            {
                mdl.geoset.LoadData();
                if (mdl.geoset.subs.Count != 0)
                    mdl.geoset.createEngineModelsFromPrefabSet();
            }

            UnityModel res_static;
            RuntimeData rd = RuntimeData.get();
            if (!rd.s_coh_model_to_engine_model.TryGetValue(mdl, out res_static))
                return;
            if (res_static == null)
                return;
            SEGSRuntime.Tools.EnsureDirectoryExists(mesh_path);
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
        float alphaRef = 0.0f;
        bool depthWrite = true;
        bool isAdditive=false;
        CullMode targetCulling=CullMode.Back;
        
        bool disableZtest=false;
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
                disableZtest = true;
                //depthTest = CompareFunction.Always;
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
        CompareFunction depthTest = CompareFunction.LessEqual;
        /*
        
        HTexture whitetex = tryLoadTexture(ctx,"white.tga");
        
        var vertex_defines = new List<string>();
        var pixel_defines = new List<string>();
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
            switch(mdl.blend_mode)
    {
    case CoHBlendMode.MULTIPLY:
        pixel_defines.Add("COH_MULTIPLY");
        break;
    case CoHBlendMode.MULTIPLY_REG:
        if(!depthWrite && isAdditive) {
            preconverted = cache.GetResource<Material>("Materials/AddAlpha.xml").Clone();
            preconverted.SetCullMode(CULL_NONE);
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
        preconverted = cache.GetResource<Material>("Materials/TexturedDualBump.xml").Clone();
        pixel_defines.Add("COH_MULTIPLY");
        break;
    case CoHBlendMode.BUMPMAP_COLORBLEND_DUAL:
        preconverted = cache.GetResource<Material>("Materials/TexturedDualBump.xml").Clone();
        pixel_defines << "COH_COLOR_BLEND_DUAL";
        break;
    }
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
                    TextureWrapper detail = null;
                    if (!String.IsNullOrEmpty(wrap.detailname))
                    {
                        detail = GeoSet.loadTexHeader(wrap.detailname);
                    }
                    Material mat;
                    if (isAdditive)
                    {
                        mat = new Material(Shader.Find("Unlit/Additive"));
                        mat.SetColor("_Color",tint1);
                    }
                    else
                        mat = new Material(Shader.Find("Diffuse"));
                    mat.SetTexture("_MainTex", wrap.tex);
                    if(detail!=null)
                        mat.SetTexture("_Detail",detail.tex);
                    mat.SetInt("_ZWrite",depthWrite ? 1 : 0);
                    if(disableZtest)
                        mat.SetInt("_ZTest",(int) CompareFunction.Always);
                    mat.SetInt("_Cull",(int)targetCulling);
                    AssetDatabase.CreateAsset(mat, path_material_name);
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

    private bool convertChildren(SceneNode n, GameObject res)
    {
        bool all_ok = true;
        foreach (SceneNodeChildTransform child in n.m_children)
        {
            GameObject go = convertFromRoot(child.node);
            all_ok &= (go != null);
            if (go != null)
            {
                go.transform.parent = res.transform;
                go.transform.localScale = child.m_matrix2.lossyScale;
                go.transform.rotation = child.m_matrix2.rotation;
                go.transform.position = new Vector3(child.m_matrix2.m03, child.m_matrix2.m13, child.m_matrix2.m23);
            }
        }

        return all_ok;
    }

    private static RuntimeData s_runtime_data;
    public override void OnImportAsset(AssetImportContext ctx)
    {
        Tools.EnsureDirectoryExists(PREFAB_DESTINATION_DIRECTORY);
        int idx = ctx.assetPath.LastIndexOf("geobin");
        if (-1 == idx)
        {
            ctx.LogImportWarning("File is not located under 'geobin', skipping");
            return;
        }
        if (null==s_runtime_data) //!m_RuntimeReady
        {
            s_runtime_data = new RuntimeData();
            string basepath = ctx.assetPath.Substring(0, idx - 1);
            if (!basepath.EndsWith("/") && basepath[basepath.Length - 1] != Path.DirectorySeparatorChar)
                basepath += Path.DirectorySeparatorChar;
            s_runtime_data.prepare(basepath);
        }
        sg = SceneGraph.loadWholeMap(ctx.assetPath);
        var top_nodes = sg.calculateUsages();
        if (top_nodes.Count != 0)
        {
            bool some_prefabs_were_missing = false;
            foreach (var pair in top_nodes)
            {
                GameObject top_level = convertFromRoot(pair.Value);
                if (top_level == null)
                    some_prefabs_were_missing = true;
                else
                    GameObject.DestroyImmediate(top_level);
            }

            foreach (KeyValuePair<SceneNode, GameObject> pair in m_imported_prefabs)
            {
                if (!top_nodes.ContainsValue(pair.Key))
                    GameObject.DestroyImmediate(pair.Value); // remove all non-top-level prefabs from scene.
            }

            if (some_prefabs_were_missing == false)
                createTopNodesInstances(top_nodes);
            else
            {
                string saved_ones = String.Join("\n",m_created_prefabs);
                ctx.LogImportWarning(String.Format("The following prefab assets were missing and were created, please retry: {0}",
                    saved_ones));
            } 
        }
    }

    private static void createTopNodesInstances(SortedList<string, SceneNode> top_nodes)
    {
        foreach (var pair in top_nodes)
        {
            var n = pair.Value;
            string pref_path = n.m_src_bin;
            int idxp = pref_path.LastIndexOf("geobin");
            string src_directory = PREFAB_DESTINATION_DIRECTORY + pref_path.Substring(idxp);

            var prefabv = GetPrefabAsset(src_directory, n.m_name);
            PrefabUtility.InstantiatePrefab(prefabv);
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