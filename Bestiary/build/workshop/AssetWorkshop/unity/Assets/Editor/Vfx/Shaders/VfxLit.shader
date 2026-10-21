// Preview only: the game's lit particles, approximated. _VfxLitMode 0 is Lux Lit Particles/ Bumped (the game's smoke,
// dust and mist): the texture's red and green are a normal, alpha the shape, colour from _Color and the particle, lit
// with wrapped diffuse and translucency. _VfxLitMode 1 is Custom/LitParticles, Particles/Standard Surface2 and the
// decal and blob shaders: the texture's colour times _Color and the particle, lit, plus _EmissionColor. Opaque or
// blended by _SrcBlend/_DstBlend/_ZWrite.
Shader "Workshop/Vfx/Lit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _SrcBlend ("Src", Float) = 5
        _DstBlend ("Dst", Float) = 10
        _ZWrite ("ZWrite", Float) = 0
        _Cutoff ("Cutoff", Float) = 0
        _InvFade ("Soft factor", Float) = 1
        _ZFadeDistance ("Soft distance", Float) = 0
        _WrappedDiffuse ("Wrap", Float) = 0.2
        _Translucency ("Translucency", Float) = 0.5
        _VfxLitMode ("Mode", Float) = 0
        _VfxSRGB ("sRGB", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Off
            CGPROGRAM
            #pragma vertex VfxVert
            #pragma fragment frag
            #pragma target 3.0
            #include "VfxCommon.cginc"
            sampler2D _MainTex;
            float4 _Color, _EmissionColor;
            float _Cutoff, _InvFade, _ZFadeDistance, _WrappedDiffuse, _Translucency, _VfxLitMode, _SrcBlend, _DstBlend;

            float3 Normal(VfxOut i, float4 tex)
            {
                if (_VfxLitMode > 0.5)
                    return i.normal;
                float3 forward = normalize(i.world - _WorldSpaceCameraPos);
                float3 right = normalize(cross(float3(0, 1, 0), forward));
                float3 up = cross(forward, right);
                float2 xy = tex.rg * 2 - 1;
                return normalize(right * xy.x + up * xy.y - forward * sqrt(saturate(1 - dot(xy, xy)) + 0.2));
            }

            float4 frag(VfxOut i) : SV_Target
            {
                float4 tex = tex2D(_MainTex, i.uv);
                float3 albedo = _VfxLitMode > 0.5 ? lerp(tex.rgb, VfxLinear(tex.rgb), _VfxSRGB) : 1;
                albedo *= VfxLinear(_Color.rgb) * i.color.rgb;
                float3 n = Normal(i, tex);
                float3 light = VfxLighting(i.world, n, _WrappedDiffuse);
                float3 back = _VfxLitMode > 0.5 ? 0 : _VfxSunColor.rgb * _Translucency * saturate(dot(-n, _VfxSunDir.xyz)) * 0.5;
                float alpha = tex.a * _Color.a * i.color.a;
                clip(alpha - _Cutoff);
                float soft = _InvFade > 0 ? VfxSoft(i.screen, i.depth, _InvFade) : 1;
                soft *= _ZFadeDistance > 0 ? VfxSoft(i.screen, i.depth, 1 / _ZFadeDistance) : 1;
                float3 rgb = albedo * (light + back) + VfxLinear(_EmissionColor.rgb);
                return float4(rgb, alpha * soft);
            }
            ENDCG
        }
    }
}
