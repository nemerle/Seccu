﻿Shader "CoH/CoHMult"
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
                fixed4 c = tex2D (_MainTex, IN.uv) * _Color;
                fixed4 det = tex2D (_Detail, IN.uv_detail);
                //
                switch(_CoHMod)
                {
                    case 2: //color blend dual 
                    c = calc_dual_tint(_Color,_Secondary,c,det);                    
                    break;
                    default: 
                        c = c*det * IN.v_color;
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
