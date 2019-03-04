using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

[ScriptedImporter(1, "bin")]
public class GEOImporter : ScriptedImporter
{
    private const string PREFAB_DESTINATION_DIRECTORY = "Assets/Prefabs/";
    private static bool m_RuntimeReady = false;
    internal SceneGraph sg;
    private Dictionary<SceneNode, GameObject> m_imported_prefabs = new Dictionary<SceneNode, GameObject>();
    private List<string> m_created_prefabs = new List<string>();
    private SceneGraph m_previous = null;
    private string m_previous_asset;
    private static RuntimeData s_runtime_data;
    private static readonly int CullMode = Shader.PropertyToID("_CullMode");
    private static readonly int ZTest = Shader.PropertyToID("_ZTest");
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int Detail = Shader.PropertyToID("_Detail");

    private static GameObject InstantiateModelForEditing(GameObject model)
    {
        GameObject instance = GameObject.Instantiate(model);
        instance.name = model.name;
        return instance;
    }

    private static string GetSafeFilename(string filename)
    {
        return string.Join("_", filename.Split(new char[] {'?', '+'}));
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

    static private T getOrCreateComponent<T>(GameObject on) where T : Component
    {
        T res = on.GetComponent<T>();
        if (res == null)
            res = on.AddComponent<T>();
        return res;
    }

    static void checkForLodGroup(SceneNode n, GameObject inside)
    {
        SceneNode child_lod=null;
        foreach (SceneNodeChildTransform child in n.m_children)
        {
            if (child.node != null && child.node.lod_near!=0.0)
            {
                if(child_lod!=null)
                    Debug.LogWarningFormat("Node {0} has multiple children lods!",inside.name);
                child_lod = child.node;
            }
        }

        LODGroup ldgrp;
        if (child_lod != null)
        {
            ldgrp = inside.AddComponent<LODGroup>();
            List<LOD> lods = new List<LOD>();
            float max_dist = child_lod.lod_far + child_lod.lod_far_fade;
            float far_percent = 1.0f - ((child_lod.lod_far + (child_lod.lod_far_fade/1.25f)) / max_dist); // 
            float near_percent = 1.0f - ((child_lod.lod_near + 0.6f*(child_lod.lod_far-child_lod.lod_near))/ max_dist);
            var lodentry = new LOD(near_percent, new[] {inside.GetComponent<Renderer>()});
            lodentry.fadeTransitionWidth = child_lod.lod_far_fade / max_dist;
            lods.Add(lodentry);
            lodentry = new LOD(far_percent, new[] {child_lod.generated.GetComponent<Renderer>()});
            lodentry.fadeTransitionWidth = 0.1f;
            lods.Add(lodentry);
            ldgrp.SetLODs(lods.ToArray());
        }
        else 
        {
            if (n.lod_far != 0.0f && n.lod_near == 0.0f)
            {
                ldgrp = inside.AddComponent<LODGroup>();
                List<LOD> lods = new List<LOD>();
                var lodentry = new LOD(0.03f, new[] {inside.GetComponent<Renderer>()});
                lods.Add(lodentry);
                ldgrp.SetLODs(lods.ToArray());
            }
        }
/*
        Bounds screen_bounds;
        Bounds calculated_child_bounds=new Bounds();
        var self_ren = inside.GetComponent<Renderer>();
        if (null!=self_ren)
        {
            calculated_child_bounds.Encapsulate(self_ren.bounds);
        }
        foreach (Renderer ren in inside.GetComponentsInChildren<Renderer>())
        {
            calculated_child_bounds.Encapsulate(ren.bounds);
        }
        // npw we transform bounds from world space to inside's localspace
        Vector3 ls_max = inside.transform.InverseTransformPoint(calculated_child_bounds.max);
        Vector3 ls_min = inside.transform.InverseTransformPoint(calculated_child_bounds.min);
        calculated_child_bounds.max = ls_max;
        calculated_child_bounds.min = ls_min;
        
        GameObject fakeone = new GameObject();
        Camera front_cam = fakeone.AddComponent<Camera>();
        float percent_far = lodValueToPercentage(calculated_child_bounds, fakeone, n.lod_far);
        float percent_near = lodValueToPercentage(calculated_child_bounds, fakeone, n.lod_near);
        GameObject.DestroyImmediate(fakeone);
        entry.renderers = inside.GetComponent<Renderer>();
*/
        
    }

    private static float lodValueToPercentage(Bounds calculated_child_bounds, GameObject fakeone, float lod_value)
    {
        Vector3 tgt_center = calculated_child_bounds.center;
        fakeone.transform.position = new Vector3(lod_value, tgt_center.y, tgt_center.z);
        float percent_front = getPercentOnScreen(fakeone, calculated_child_bounds);

        fakeone.transform.position = new Vector3(tgt_center.y, lod_value, tgt_center.z);
        float percent_left = getPercentOnScreen(fakeone, calculated_child_bounds);
        return Mathf.Max(percent_left, percent_front);
    }

    private static float getPercentOnScreen(GameObject fakeone, Bounds calculated_child_bounds)
    {
        fakeone.transform.LookAt(calculated_child_bounds.center, Vector3.up);
        Camera camz = fakeone.GetComponent<Camera>();
        camz.projectionMatrix = Matrix4x4.Perspective(90, 4.0f / 3.0f, 0.01f, 1000);
        camz.pixelRect = new Rect(0,0,100,100);
        Vector3 world_pos_min = camz.WorldToScreenPoint(calculated_child_bounds.min);
        Vector3 world_pos_max = camz.WorldToScreenPoint(calculated_child_bounds.max);
        float dx = Mathf.Abs(world_pos_max.x - world_pos_min.x);
        float dy = Mathf.Abs(world_pos_max.y - world_pos_min.y);
        return dx * dy / 100 * 100;
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
                {
                    res = (GameObject) PrefabUtility.InstantiatePrefab(prefab);
                    if (n.generated == null)
                        n.generated = res;
                    return res;
                }

            }

            res = new GameObject(n.m_name);
            res.isStatic = true;
            var flgs = GameObjectUtility.GetStaticEditorFlags(res);
            flgs &= ~StaticEditorFlags.LightmapStatic; 
                
            GameObjectUtility.SetStaticEditorFlags(res,flgs);
            
            n.generated = res;
            // Convert the whole node.

            // converting node components
            convertComponents(n, res);
            // convert model
            if (n.m_model != null)
            {
                convertModel(n, res, true);
            }

            if (!convertChildren(n, res))
            {
                GameObject.DestroyImmediate(res);
                return null;
            }
            
            checkForLodGroup(n, res);
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
                if (n.generated == null)
                    n.generated = res3;
                return res3;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return res;
    }

    static Vector3[] createProbes(int max_vertical, int max_horiz, float radius)
    {
        int onion_layers = radius > 30 ? 2 : 1;
        Vector3[] probe_locations = new Vector3[max_vertical * max_horiz * onion_layers];
        int index = 0;
        if (radius > 30)
        {
            float layer_radius = radius / 2;
            for (int m = 0; m < max_horiz; m++)
            {
                for (int n = 0; n < max_vertical - 1; n++)
                {
                    float x = Mathf.Sin(Mathf.PI * m/max_horiz) * Mathf.Cos(2 * Mathf.PI * n/max_vertical);
                    float y = Mathf.Sin(Mathf.PI * m/max_horiz) * Mathf.Sin(2 * Mathf.PI * n/max_vertical);
                    float z = Mathf.Cos(Mathf.PI * m / max_horiz);
                    probe_locations[index++] = new Vector3(x, y, z) * layer_radius;
                }
            }

        }
        for (int m = 0; m < max_horiz; m++)
        {
            for (int n = 0; n < max_vertical - 1; n++)
            {
                float x = Mathf.Sin(Mathf.PI * m/max_horiz) * Mathf.Cos(2 * Mathf.PI * n/max_vertical);
                float y = Mathf.Sin(Mathf.PI * m/max_horiz) * Mathf.Sin(2 * Mathf.PI * n/max_vertical);
                float z = Mathf.Cos(Mathf.PI * m / max_horiz);
                probe_locations[index++] = new Vector3(x, y, z) * radius;
            }
        }

        return probe_locations;

    }
    private static void convertComponents(SceneNode n, GameObject res)
    {
        if (n.m_light != null)
        {
            convertLightComponent(n, res);
        }

        if (n.m_properties != null)
        {
            var sup = getOrCreateComponent<NodeMods>(res);

            if (sup.Props.Properties == null && n.m_properties.Count != 0)
                sup.Props.Properties = n.m_properties;
        }

        if (n.sound_info != null)
        {
            Debug.Log("Has sound properties");
            SoundInfo coh_snd = n.sound_info;
            var snd = getOrCreateComponent<AudioSource>(res);
            snd.maxDistance = coh_snd.radius;
            snd.minDistance = coh_snd.radius - coh_snd.ramp_feet;
            snd.volume = coh_snd.vol / 255.0f;
            snd.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(coh_snd.name);
            if (null == snd.clip)
            {
                Debug.LogWarningFormat("Failed to locate sound asset {0}", coh_snd.name);
            }
        }

        if (n.m_editor_beacon != null)
        {
            if (n.m_model != null)
            {
                Debug.Log("Not adding beacon sphere to a node with model.");
            }
            else
            {
                var pm = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pm.name = n.m_editor_beacon.name;
                MeshRenderer mr = pm.GetComponent<MeshRenderer>();
                mr.receiveShadows = false;
                pm.transform.localScale = Vector3.one * n.m_editor_beacon.amplitude;
                pm.transform.SetParent(res.transform);
                pm.layer = 9;
                mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Beacon.mat");
            }
        }
    }

    private static void convertLightComponent(SceneNode n, GameObject res)
    {
        if (n.m_light.is_negative)
        {
            Debug.LogWarning("Unity does not support negative lights");
        }
        else
        {
            // since light object is disabled by default, we don't want to disable ourselves,
            // since we have light probes
            var lobj = new GameObject("Omni Light");
            // Light objects are put into seperate layer, to allow fast sphere collider lookups.
            lobj.layer = LayerMask.NameToLayer("OmniLights");
            lobj.transform.SetParent(res.transform);

            Light light = lobj.AddComponent<Light>();
            light.color = n.m_light.color;
            light.range = n.m_light.range;
            light.type = LightType.Point;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.cullingMask = ~(1 << 9); // Don't light the layer 9 - Editor object
            light.enabled = true;
            light.intensity = 4;

            //SphereCollider sp_c = lobj.AddComponent<SphereCollider>();
            //sp_c.radius = light.range;
            //sp_c.isTrigger = true;

            //LightProbeGroup lpb=lobj.AddComponent<LightProbeGroup>();
            //lpb.probePositions = createProbes(3, 2, light.range);
        }
    }

    private static string targetDirForNodePrefab(SceneNode n)
    {
        int idx = n.m_src_bin.LastIndexOf("geobin");
        string target_directory = PREFAB_DESTINATION_DIRECTORY + n.m_src_bin.Substring(idx);
        return target_directory;
    }

    void applyTricks(GameObject res, ModelModifiers mods)
    {
        MeshRenderer ren = res.GetComponent<MeshRenderer>();
        if (mods._TrickFlags.HasFlag(TrickFlags.CameraFace))
        {
            res.AddComponent<AlwaysFaceCamera>();
        }

        if (mods._TrickFlags.HasFlag(TrickFlags.LightFace))
        {
            res.AddComponent<AlwaysFaceSun>();
        }
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
        var sup = res.AddComponent<ModelNodeMods>();
        if (model_trick != null)
        {
            sup.ModelMod = model_trick;
        }

        mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(model_path);
        if (model_trick != null)
        {
            ren.shadowCastingMode = ShadowCastingMode.Off;
            if (model_trick._TrickFlags.HasFlag(TrickFlags.NoDraw))
            {
                ren.receiveShadows = false;
                res.tag = "NoDraw";
                res.layer = 9;
            }

            if (model_trick._TrickFlags.HasFlag(TrickFlags.EditorVisible))
            {
                ren.receiveShadows = false;
                res.layer = 9;
            }

            if (model_trick._TrickFlags.HasFlag(TrickFlags.CastShadow))
            {
                ren.receiveShadows = false;
                ren.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
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
        if (mf.sharedMesh == null)
        {
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

        convertMaterials(ren, mdl, res);
    }

    static bool textureFormatHasAlpha(TextureFormat tf)
    {
        return tf == TextureFormat.Alpha8 ||
               tf == TextureFormat.ARGB32 ||
               tf == TextureFormat.ARGB4444 ||
               tf == TextureFormat.RGBA32 ||
               tf == TextureFormat.DXT5 ||
               tf == TextureFormat.RGBA4444 ||
               tf == TextureFormat.BGRA32 ||
               tf == TextureFormat.RGBAHalf ||
               tf == TextureFormat.RGBAFloat ||
               tf == TextureFormat.RGBA4444 ||
               tf == TextureFormat.DXT5Crunched ||
               tf == TextureFormat.PVRTC_RGBA2 ||
               tf == TextureFormat.PVRTC_RGBA4 ||
               tf == TextureFormat.ETC2_RGBA1 ||
               tf == TextureFormat.ETC2_RGBA8 ||
               tf == TextureFormat.ASTC_RGBA_4x4;
    }
    struct MaterialDescriptor
    {
        public bool depthWrite;
        public bool isAdditive;
        public CullMode targetCulling;

    }
    private static void convertMaterials(MeshRenderer ren, Model mdl, GameObject tgt)
    {
        ModelModifiers model_trick = mdl.trck_node;
        GeometryModifiersData geom_trick = mdl.src_mod;
        RuntimeData rd = RuntimeData.get();
        MaterialDescriptor descriptor;

        string model_base_name = mdl.name.Split(new string[] {"__"}, StringSplitOptions.None)[0];
        bool isDoubleSided = false;
        string mesh_path = mdl.geoset.full_geo_path;
        int obj_lib_idx = mesh_path.IndexOf("object_library");
        if (obj_lib_idx != -1)
            mesh_path = "Assets/Materials/" + mesh_path.Substring(obj_lib_idx);
        string material_base_path = mesh_path + "/" + Path.GetFileNameWithoutExtension(mdl.name);
        if (mdl.name == "Crate1_med_Wood__TintCrates")
        {
            Debug.LogFormat("Crate {0}",mdl.BlendMode.ToString());
        }
        if (model_trick != null && model_trick._TrickFlags.HasFlag(TrickFlags.ColorOnly))
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
        descriptor.depthWrite = true;
        descriptor.isAdditive = false;
        descriptor.targetCulling = UnityEngine.Rendering.CullMode.Back;
        string shader_to_use = "";

        bool disableZtest = false;
        if (null != model_trick && model_trick._TrickFlags != 0)
        {
            var tflags = model_trick._TrickFlags;
            if (tflags.HasFlag(TrickFlags.Additive))
            {
                descriptor.isAdditive = true;
            }

            if (tflags.HasFlag(TrickFlags.ColorOnly))
                onlyColor = model_trick.TintColor0;
            if (tflags.HasFlag(TrickFlags.DoubleSided))
                isDoubleSided = true;
            if (tflags.HasFlag(TrickFlags.NoZTest))
            {
                // simulate disabled Z test
                disableZtest = true;
                //depthTest = CompareFunction.Always;
                descriptor.depthWrite = false;
            }

            if (tflags.HasFlag(TrickFlags.NoZWrite))
                descriptor.depthWrite = false;
            if (tflags.HasFlag(TrickFlags.SetColor))
            {
                tint1 = model_trick.TintColor0;
                tint2 = model_trick.TintColor1;
                tint1.a = 1.0f;
                tint2.a = 1.0f;
            }

            if (tflags.HasFlag(TrickFlags.ReflectTex0) || tflags.HasFlag(TrickFlags.ReflectTex1))
            {
                shader_to_use = "Custom/ReflectGen";
                if (mdl.flags.HasFlag(ModelFlags.OBJ_CUBEMAP))
                {
                    Debug.Log("Unhandled Cubemap");
                }
            }

            if (tflags.HasFlag(TrickFlags.AlphaRef))
            {
                //qDebug() << "Unhandled alpha ref";
                alphaRef = geom_trick.AlphaRef;
            }

            if (tflags.HasFlag(TrickFlags.TexBias))
                Debug.Log("Unhandled TexBias");
        }
        if(isDoubleSided)
            descriptor.targetCulling = UnityEngine.Rendering.CullMode.Off;
        else
            descriptor.targetCulling = UnityEngine.Rendering.CullMode.Back;

        CompareFunction depthTest = CompareFunction.LessEqual;
        TextureWrapper whitetex = GeoSet.loadTexHeader("white");
        var vertex_defines = new List<string>();
        var pixel_defines = new List<string>();
        pixel_defines.Add("DIFFMAP");
        pixel_defines.Add("ALPHAMASK");
        string shader_name;
        switch (mdl.BlendMode)
        {
            case CoHBlendMode.MULTIPLY:
                pixel_defines.Add("COH_MULTIPLY");
                break;
            case CoHBlendMode.MULTIPLY_REG:
                if (!descriptor.depthWrite && descriptor.isAdditive)
                {
                    shader_name = "AddAlpha";
                    descriptor.targetCulling = UnityEngine.Rendering.CullMode.Off;
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
                shader_name = "TexturedDualBump";
                pixel_defines.Add("COH_MULTIPLY");
                break;
            case CoHBlendMode.BUMPMAP_COLORBLEND_DUAL:
                shader_name = "TexturedDualBump";
                pixel_defines.Add("COH_COLOR_BLEND_DUAL");
                break;
        }

        if (mdl.flags.HasFlag(ModelFlags.OBJ_TREE))
        {
            //preconverted.SetVertexShaderDefines("TRANSLUCENT");
            pixel_defines.Add("TRANSLUCENT");
            alphaRef = 0.4f;
            tint1.a = 254.0f / 255.0f;
        }

        if (alphaRef != 0.0f)
        {
            //preconverted.SetShaderParameter("AlphaRef",alphaRef);
        }
    
        /*
        preconverted.SetPixelShaderDefines(pixel_defines.join(' '));
        if(isDoubleSided)
            preconverted.SetCullMode(CULL_NONE);
    
        // int mode= dualTexture ? 4 : 5
        bool is_single_mat = mdl.texture_bind_info.Count == 1;
        */
        Material[] materials = new Material[mdl.texture_bind_info.Count];
        Material additive = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/InstancedAdditive.mat");
        int idx = 0;
        Tools.EnsureDirectoryExists(material_base_path);
        var sup = tgt.GetComponent<ModelNodeMods>();
        sup.GeomTricks = geom_trick;
        VBOPointers vbo = mdl.vbo;
        Shader selected=null;
        if (descriptor.isAdditive)
        {
            selected = Shader.Find("Unlit/Additive");
        }
        else
        {
            if(!String.IsNullOrEmpty(shader_to_use))
                selected = Shader.Find(shader_to_use);
            else
                selected = Shader.Find("CoH/CoHMult");
        }
                                        
        sup.TexWrappers = vbo.assigned_textures;
        foreach (TextureWrapper wrap in vbo.assigned_textures)
        {
            string path_material_name = String.Format("{0}/{1}.mat", material_base_path, idx);
            Material available = AssetDatabase.LoadAssetAtPath<Material>(path_material_name);
            if (available == null)
            {
                if (wrap != null)
                {
                    TextureWrapper detail = null;
                    if (!String.IsNullOrEmpty(wrap.detailname))
                    {
                        detail = GeoSet.loadTexHeader(wrap.detailname);
                    }

                    Material mat= new Material(selected);
                    mat.SetColor("_Color", tint1);
                    mat.SetColor("_Color2", tint2);
                    mat.SetFloat("_AlphaRef",alphaRef);

                    mat.SetTexture("_MainTex", wrap.tex);
                    if (detail != null)
                        mat.SetTexture("_Detail", detail.tex);
                    if(!descriptor.depthWrite)
                        mat.SetInt("_ZWrite", 0);
                    if (disableZtest)
                        mat.SetInt(ZTest, (int) CompareFunction.Always);
                    
                    mat.SetInt(CullMode, (int) descriptor.targetCulling);
                    mat.SetTextureScale(MainTex,wrap.scaleUV1);
                    mat.SetTextureScale(Detail,wrap.scaleUV0);
                    mat.SetInt("_CoHMod",(int)mdl.BlendMode);
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
            */
        }

        if (mdl.flags.HasFlag(ModelFlags.OBJ_HIDE))
        {
            ren.enabled = false;
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


    public override void OnImportAsset(AssetImportContext ctx)
    {
        Tools.EnsureDirectoryExists(PREFAB_DESTINATION_DIRECTORY);
        int idx = ctx.assetPath.LastIndexOf("geobin");
        if (-1 == idx)
        {
            ctx.LogImportWarning("File is not located under 'geobin', skipping");
            return;
        }

        if (null == s_runtime_data) //!m_RuntimeReady
        {
            s_runtime_data = new RuntimeData();
            string basepath = ctx.assetPath.Substring(0, idx - 1);
            if (!basepath.EndsWith("/") && basepath[basepath.Length - 1] != Path.DirectorySeparatorChar)
                basepath += Path.DirectorySeparatorChar;
            s_runtime_data.prepare(basepath);
        }

        if (m_previous_asset == null || m_previous == null)
            sg = SceneGraph.loadWholeMap(ctx.assetPath);
        else
        {
            Debug.Log("Reusing previously loaded graph");
            sg = m_previous;
        }

        m_previous = sg;
        m_previous_asset = ctx.assetPath;
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
                string saved_ones = String.Join("\n", m_created_prefabs);
                ctx.LogImportWarning(String.Format(
                    "The following prefab assets were missing and were created, please retry: {0}",
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
                {
                //    Debug.Log("SEGS scene graph available");
                }
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
                if (asset.ToLower().Contains("geobin"))
                {
                    //Debug.LogFormat("Remove geobin source ? {0}",AssetDatabase.DeleteAsset(asset));
                }
            }
        }
    }
}