using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class LightCalc : MonoBehaviour
{
    public Color m_Ambient;
    public HashSet<Light> m_Lights = new HashSet<Light>();

    private int layer_bit = 0;
    private const int activeLightCount = 10;

    // On start add the contribution of the ambient light and all lights
    // assigned to the lights array to all baked probes.
    void Start()
    {
        m_Ambient = RenderSettings.ambientLight;
        layer_bit = LayerMask.NameToLayer("OmniLights");
        recalculateLightProbes(null);

        InvokeRepeating("Lookup", 1.0f, 1.0f);
    }


    void Lookup()
    {
        Collider[] omnis_in_radius = Physics.OverlapSphere(transform.position, 50, 1 << layer_bit);
        // check for lights to enable
        SortedList<float, Light> light_list = new SortedList<float, Light>();
        foreach (Collider omni in omnis_in_radius)
        {
            var lght = omni.gameObject.GetComponent<Light>();
            float selection = lght.color.grayscale /
                              (0.001f + (omni.gameObject.transform.position - transform.position).magnitude);
            light_list.Add(selection, lght);
        }

        // now we take 3 lights from this list and enable them
        List<Light> enabled_lights = new List<Light>();
        List<Light> lights_to_disable = new List<Light>();
        var light_list_values = light_list.Values;
        for (int i = 0; i < activeLightCount; ++i)
        {
            int idx = light_list_values.Count - (1 + i);
            if (idx >= 0)
            {
                enabled_lights.Add(light_list_values[idx]);
                light_list_values[idx].enabled = true;
                light_list_values[idx].shadows = LightShadows.Hard;
                m_Lights.Add(light_list_values[idx]);
            }
        }

        bool all_alread_enabled=true;
        foreach (var l in enabled_lights)
        {
            if (!m_Lights.Contains(l))
                all_alread_enabled = false;
        }

        if (all_alread_enabled && m_Lights.Count == enabled_lights.Count)
            return;
        foreach (Light l in m_Lights)
        {
            if (!enabled_lights.Contains(l))
            {
                l.enabled = false;
                lights_to_disable.Add(l);
            }
        }

        foreach (Light l in lights_to_disable)
            m_Lights.Remove(l);

        recalculateLightProbes(null);

        Debug.LogFormat("There are {0} omni-lights in range, enabled {1}, disabled {2}", omnis_in_radius.Length,
            enabled_lights.Count, lights_to_disable.Count);
    }

    private void recalculateLightProbes(List<Light> lights_in_range)
    {
        Collider[] omnis_in_radius = Physics.OverlapSphere(transform.position, 150, 1 << layer_bit);
        SphericalHarmonicsL2[] bakedProbes = LightmapSettings.lightProbes.bakedProbes;
        Vector3[] probePositions = LightmapSettings.lightProbes.positions;
        List<Light> light_list = new List<Light>();
        foreach (Collider omni in omnis_in_radius)
        {
            var lght = omni.gameObject.GetComponent<Light>();
            if (!lght.enabled) // only inactive lights influence the built lightprobes
                light_list.Add(lght);
        }


        int probeCount = LightmapSettings.lightProbes.count;
        Debug.LogFormat("Lightprobe counts  {0}  {1}", LightmapSettings.lightProbes.bakedProbes.Length, probeCount);
        //GameObject.Find
        // Clear all probes
        if (bakedProbes.Length < probeCount)
            bakedProbes = new SphericalHarmonicsL2[probeCount];
        for (int i = 0; i < probeCount; i++)
            bakedProbes[i].Clear();

        // Add ambient light to all probes
        for (int i = 0; i < probeCount; i++)
            bakedProbes[i].AddAmbientLight(m_Ambient);

        // Add directional and point lights' contribution to all probes
        foreach (Light l in light_list)
        {
            if (l.type == LightType.Directional)
            {
                for (int i = 0; i < probeCount; i++)
                    bakedProbes[i].AddDirectionalLight(-l.transform.forward, l.color, l.intensity);
            }
            else if (l.type == LightType.Point)
            {
                for (int i = 0; i < probeCount; i++)
                    SHAddPointLight(probePositions[i], l.transform.position, l.range, l.color, l.intensity,
                        ref bakedProbes[i]);
            }
        }

        LightmapSettings.lightProbes.bakedProbes = bakedProbes;
    }

    void SHAddPointLight(Vector3 probePosition, Vector3 position, float range, Color color, float intensity,
        ref SphericalHarmonicsL2 sh)
    {
        // From the point of view of an SH probe, point light looks no different than a directional light,
        // just attenuated and coming from the right direction.
        Vector3 probeToLight = position - probePosition;
        if (probeToLight.sqrMagnitude > range)
            return;
        float attenuation = 1.0F / (1.0F + 25.0F * probeToLight.sqrMagnitude / (range * range));
        sh.AddDirectionalLight(probeToLight.normalized, color, intensity * attenuation);
    }
}