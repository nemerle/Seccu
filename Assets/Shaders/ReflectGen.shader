Shader "Custom/ReflectGen"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Detail ("Detail", 2D) = "grey" {}
        [PerRenderData] _Color("Color", Color) = (1,1,1,1)
    }

    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        CGPROGRAM
            #pragma surface surf Standard  vertex:vert  alpha
            struct Input {
                float2 uv_MainTex : TEXCOORD0;
                float2 gen_uv : TEXCOORD2;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 v_color : COLOR;
            };
            sampler2D _MainTex;
            sampler2D _Detail;
            float _AlphaRef;
            fixed4 _Color,_Secondary;
            int _CoHMod;

            float3 reflectionMap(in float3 normal, in float3 ecPosition3)
            {
               float NdotU, m;
               float3 u;
               u = normalize(ecPosition3);
               return (reflect(u, normal));
            }
            
            void vert (inout appdata_full v,out Input tgt) {
              UNITY_INITIALIZE_OUTPUT(Input,tgt);
              float3 ecPosition = UnityObjectToViewPos(v.vertex.xyz);
              tgt.gen_uv = reflectionMap( v.normal, ecPosition );
            }
            void surf (Input IN, inout SurfaceOutputStandard o) {
                float4 res=tex2D (_MainTex, IN.gen_uv);
                //o.Albedo = res.rgb;
                o.Albedo.rgb = res;
                o.Alpha = res.a;
            }
        ENDCG
    }
    //Fallback "Diffuse"
  }
/*
Shader "Custom/ReflectGen"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Detail ("Detail", 2D) = "grey" {}
        [PerRenderData] _Color("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

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
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 gen_uv : TEXCOORD1;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _Detail;
            float4 _MainTex_ST;
            float4 _Color;

            float3 reflectionMap(in float3 normal, in float3 ecPosition3)
            {
               float NdotU, m;
               float3 u;
               u = normalize(ecPosition3);
               return (reflect(u, normal));
            }
            v2f vert (appdata v)
            {
                v2f o;
                float3 ecPosition = UnityObjectToViewPos(v.vertex.xyz);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.gen_uv = reflectionMap( v.normal, ecPosition ).xy;

                UNITY_TRANSFER_FOG(o,o.vertex);
                o.color.xyz = v.normal * 0.5 + 0.5;
                o.color.w = 1.0;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 diffInput = tex2D(_MainTex, i.gen_uv);
                fixed4 diffColor;
                diffColor= diffInput; // * 4
                #ifdef VERTEXCOLOR
                    diffColor.rgb *= vColor.rgb;
                #endif
                
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, diffColor);
                return diffColor;
            }
            ENDCG
        }
    }
}

*/