  Shader "CoH/CoHMult"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Color2 ("Color2", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Detail ("Detail", 2D) = "white" {}
        _BumpMap ("Bumpmap", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0,1)) = 0.01
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _AlphaRef ("AlphaRef", Range(0,1)) = 0.0
        [Enum(SEGSRuntime.CoHBlendMode)] _CoHMod("CoHBlendMode",Int) = 0        
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode("CullMode",Int) = 2
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest",Int) = 4
        [Enum(Off,0,On,1)] _ZWrite("ZWrite",Int) = 1
    }

    SubShader {
        LOD 200
        AlphaToMask On
        Cull [_CullMode]
        ZTest [_ZTest]
        ZWrite [_ZWrite]
        Tags { "Queue"="Geometry" "RenderType"="Transparent" }
        
        CGPROGRAM
            #pragma surface surf Standard vertex:vert
          /* Uniforms */
            sampler2D _MainTex;
            sampler2D _Detail;
            sampler2D _BumpMap;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
                UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
                UNITY_DEFINE_INSTANCED_PROP(float, _AlphaRef)
                UNITY_DEFINE_INSTANCED_PROP(int, _CoHMod)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color2)
            // put more per-instance properties here
            UNITY_INSTANCING_BUFFER_END(Props)
    
            struct Input {
                float4 v_color : COLOR;
                float2 uv_MainTex : TEXCOORD0;
                float2 uv_Detail : TEXCOORD1;
            };
            
            void vert (inout appdata_full v, out Input o) {
              UNITY_INITIALIZE_OUTPUT(Input,o);
              o.uv_Detail = o.uv_MainTex;
            }
            float4 calc_dual_tint(float4 const0, float4 const1, float4 tex0, float4 tex1)
            {
                float4 dual_color;
                dual_color.rgb = lerp(const0.rgb, const1.rgb, tex1.rgb);
                dual_color.rgb = lerp(dual_color.rgb, float3(1.0, 1.0, 1.0), tex0.w);
                dual_color.rgb *= tex0.rgb;
                dual_color.a = tex1.a * const0.a;
                return dual_color;
            }
            float4 calc_single_tint(in float4 color, in float4 tex)
            {
                float3 tinted = lerp(color.rgb, tex.rgb, tex.a);
                return float4( tinted, color.a );
            }
    
            void surf (Input IN, inout SurfaceOutputStandard o) {
                float4 main_tex =tex2D (_MainTex, IN.uv_MainTex);
                float4 base_color;
                fixed4 c = main_tex;
                fixed4 det = tex2D (_Detail, IN.uv_Detail);
                // Retrieve per-instance uniforms
                fixed4 inst_Color = UNITY_ACCESS_INSTANCED_PROP(Props, _Color); 
                fixed4 inst_Color2 = UNITY_ACCESS_INSTANCED_PROP(Props, _Color2);
                float inst_AlphaRef = UNITY_ACCESS_INSTANCED_PROP(Props, _AlphaRef);
                float inst_Smoothness = UNITY_ACCESS_INSTANCED_PROP(Props, _Smoothness);
                float inst_Metallic = UNITY_ACCESS_INSTANCED_PROP(Props, _Metallic);
                 
                switch(UNITY_ACCESS_INSTANCED_PROP(Props, _CoHMod))
                {
                    case 1:
                        c = main_tex * det;
                        c.rgb		*= IN.v_color.rgb; //8*
                        c.a *= inst_Color.a;
                        break;
                    case 2: //color blend dual 
                    c = calc_dual_tint(inst_Color,inst_Color2,c,det);
                    break;
                    case 3: // add glow
                    base_color = calc_single_tint(inst_Color, main_tex );
                    // add glow to the base color from det;
                    c = base_color;
                    c.xyz = c.xyz + det.xyz;                     
                    break;
                    case 6:
                        c = calc_dual_tint(inst_Color,inst_Color2,c,det);
                        o.Normal = tex2D (_BumpMap, IN.uv_MainTex);
                    break;
                    default:
                        c = float4(1,0,1,1); 
                        //c = c*det * IN.v_color * inst_Color;
                        break;
                }
                if (c.a <= inst_AlphaRef)
                {
                    discard;
                }
                o.Albedo = c;
                o.Alpha = c.a;
                o.Smoothness = inst_Smoothness;
                o.Metallic = inst_Metallic;
            }
        ENDCG
    }
    Fallback "Diffuse"
  }
/*
Shader "CoH/CoHMult"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Secondary ("Secondary", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Detail ("Detail", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.01
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _AlphaRef ("AlphaRef", Range(0,1)) = 0.0
        [Enum(SEGSRuntime.CoHBlendMode)] _CoHMod("CoHBlendMode",Int) = 0        
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode("CullMode",Int) = 2
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest",Int) = 4
        [Enum(Off,0,On,1)] _ZWrite("ZWrite",Int) = 1
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
        LOD 200
        AlphaToMask On
        Blend SrcAlpha OneMinusSrcAlpha 
        Cull [_CullMode]
        ZTest [_ZTest]
        ZWrite [_ZWrite]
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #pragma target 3.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uv_detail : TEXCOORD1;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 v_color : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _Detail;
            float _AlphaRef;
            fixed4 _Color,_Secondary;
            float4 _MainTex_ST;
            float4 _Detail_ST;
            int _CoHMod;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv_detail = TRANSFORM_TEX(v.uv, _Detail);
                UNITY_TRANSFER_FOG(o,o.vertex);
                o.v_color = _Color;
                return o;
            }

            float4 calc_dual_tint(float4 const0, float4 const1, float4 tex0, float4 tex1)
            {
                float4 dual_color;
                dual_color.rgb = lerp(const0.rgb, const1.rgb, tex1.rgb);
                dual_color.rgb = lerp(dual_color.rgb, float3(1.0, 1.0, 1.0), tex0.w);
                dual_color.rgb *= tex0.rgb;
                dual_color.a = tex1.a * const0.a;
                return dual_color;
            }
            fixed4 frag (v2f IN) : SV_Target
            {
                // Albedo comes from a texture tinted by color
                float4 c = tex2D (_MainTex, IN.uv) ;
                float4 det = tex2D (_Detail, IN.uv_detail);
                //
                switch(_CoHMod)
                {
                    case 2: //color blend dual 
                    c = calc_dual_tint(_Color,_Secondary,c,det);
                    break;
                    default: 
                        c = c*det * IN.v_color *_Color;
                        break;
                }
                if (c.a <= _AlphaRef)
                {
                    discard;
                }
                return c;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
*/