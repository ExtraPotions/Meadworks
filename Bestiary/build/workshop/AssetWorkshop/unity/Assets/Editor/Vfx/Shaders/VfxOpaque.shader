// Preview only: opaque lit surfaces in linear light: the preview's ground and stand-in figure, and mesh debris dressed in
// the game's opaque shaders (Standard, Custom/StaticRock, Custom/Piece, Custom/Creature). Texture times _Color times the
// particle colour, lit by the preview's sun, ambient and the effect's lights. Writes depth for soft particles.
Shader "Workshop/Vfx/Opaque"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _VfxSRGB ("sRGB", Float) = 1
        _VfxTiling ("Tiling", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex VfxVertMesh
            #pragma fragment frag
            #pragma target 3.0
            #include "VfxCommon.cginc"
            sampler2D _MainTex;
            float4 _Color;
            float _VfxTiling;

            float4 frag(VfxOut i) : SV_Target
            {
                float3 albedo = VfxTex(_MainTex, i.uv * _VfxTiling).rgb * VfxLinear(_Color.rgb) * i.color.rgb;
                return float4(albedo * VfxLighting(i.world, normalize(i.normal), 0), 1);
            }
            ENDCG
        }
        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"
            struct v2f { V2F_SHADOW_CASTER; };
            v2f vert(appdata_base v) { v2f o; TRANSFER_SHADOW_CASTER_NORMALOFFSET(o) return o; }
            float4 frag(v2f i) : SV_Target { SHADOW_CASTER_FRAGMENT(i) }
            ENDCG
        }
    }
}
