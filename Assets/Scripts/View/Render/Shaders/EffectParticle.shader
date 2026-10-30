// 原版特效 (FXMaker 面片 / 粒子) 通用 Shader：颜色 = 2 × _TintColor × 顶点色 × 贴图 (与 Unity 旧版粒子 Shader 一致)
// 混合方式由材质的 _SrcBlend / _DstBlend 决定 (叠加 SrcAlpha One / 半透明 SrcAlpha OneMinusSrcAlpha)；
// 2D 战斗中特效总是画在角色之上：不写深度、不做深度测试，渲染队列排在角色 (Transparent) 之后
Shader "KiHan/Effect/Particle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (0.5, 0.5, 0.5, 0.5)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+100" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TintColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return 2.0f * i.color * _TintColor * tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
}
