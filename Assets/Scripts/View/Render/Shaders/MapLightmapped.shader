// 3D 地图场景材质：漫反射 × 色调 × 烘焙光照贴图 (不受实时灯光影响，与 2D 角色的不受光风格一致)
// 光照贴图沿用 UE 移动端 LQ 编码：线性光照 L = 0.98 × (lm² × Scale + Add)，项目为 Gamma 色彩空间，输出前转 Gamma (1 / 2.2)
// 光照贴图坐标烘焙在网格 UV2 (TEXCOORD1)，已含图集缩放偏移
Shader "KiHan/Map/Lightmapped"
{
    Properties
    {
        _MainTex ("Diffuse", 2D) = "white" {}
        _Tint ("Tint (Gamma)", Color) = (1,1,1,1)
        [Toggle(_LIGHTMAP)] _UseLightmap ("Use Lightmap", Float) = 0
        [NoScaleOffset] _LightmapTex ("Lightmap", 2D) = "white" {}
        _LightmapScale ("Lightmap Scale", Color) = (1,1,1,1)
        _LightmapAdd ("Lightmap Add", Color) = (0,0,0,0)
        _Exposure ("Exposure", Range(0, 3)) = 1
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "IgnoreProjector"="True" }
        // 与 UE 材质一致默认剔除背面 (TwoSided 的材质改为 Off)；描边外壳等背面几何不会被画出
        Cull [_Cull]
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _LIGHTMAP
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _LightmapTex;
            fixed4 _Tint;
            half4 _LightmapScale;
            half4 _LightmapAdd;
            half _Exposure;
            half _Cutoff;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv2 = v.uv2;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * _Tint;
                clip(c.a - _Cutoff);
            #if _LIGHTMAP
                half3 lm = tex2D(_LightmapTex, i.uv2).rgb;
                half3 light = 0.98 * (lm * lm * _LightmapScale.rgb + _LightmapAdd.rgb) * _Exposure;
                c.rgb *= pow(max(light, 1e-4), 1.0 / 2.2);
            #else
                c.rgb *= _Exposure;
            #endif
                return c;
            }
            ENDCG
        }
    }
}
