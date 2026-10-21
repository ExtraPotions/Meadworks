// Preview only: Unity's standard unlit particle shader as the game uses it (Particles/Standard Unlit2: texture times
// _Color times the particle colour, blend from _SrcBlend/_DstBlend, soft particles and camera fading when enabled),
// and with _VfxLegacy 1 the legacy particle shaders (Additive, Alpha Blended: 2 x _TintColor x particle x texture).
// With _VfxHide 1 it draws nothing: distortion and decals, which the preview does not reproduce.
Shader "Workshop/Vfx/Standard Unlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _TintColor ("Tint", Color) = (0.5, 0.5, 0.5, 0.5)
        _SrcBlend ("Src", Float) = 5
        _DstBlend ("Dst", Float) = 10
        _ZWrite ("ZWrite", Float) = 0
        _Cutoff ("Cutoff", Float) = 0
        _SoftParticlesEnabled ("Soft", Float) = 0
        _SoftParticlesFarFadeDistance ("Soft far", Float) = 1
        _InvFade ("Legacy soft", Float) = 0
        _CameraFadingEnabled ("Camera fading", Float) = 0
        _CameraNearFadeDistance ("Camera near", Float) = 1
        _CameraFarFadeDistance ("Camera far", Float) = 2
        _VfxLegacy ("Legacy", Float) = 0
        _VfxHide ("Hide", Float) = 0
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
            float4 _Color, _TintColor;
            float _Cutoff, _SoftParticlesEnabled, _SoftParticlesFarFadeDistance, _InvFade, _CameraFadingEnabled;
            float _CameraNearFadeDistance, _CameraFarFadeDistance, _VfxLegacy, _VfxHide, _SrcBlend, _DstBlend;

            float Fades(VfxOut i)
            {
                float soft = _SoftParticlesEnabled > 0.5 ? VfxSoft(i.screen, i.depth, 1 / max(_SoftParticlesFarFadeDistance, 0.01)) : 1;
                soft *= _InvFade > 0 ? VfxSoft(i.screen, i.depth, _InvFade) : 1;
                float camera = _CameraFadingEnabled > 0.5
                    ? saturate((i.depth - _CameraNearFadeDistance) / max(_CameraFarFadeDistance - _CameraNearFadeDistance, 0.01)) : 1;
                return soft * camera;
            }

            float4 frag(VfxOut i) : SV_Target
            {
                clip(0.5 - _VfxHide);
                float4 tex = VfxTex(_MainTex, i.uv);
                float4 tint = _VfxLegacy > 0.5 ? 2 * float4(VfxLinear(_TintColor.rgb), _TintColor.a) : float4(VfxLinear(_Color.rgb), _Color.a);
                float4 c = tex * tint * i.color;
                clip(c.a - _Cutoff);
                c.a *= Fades(i);
                if (abs(_SrcBlend - 1) < 0.5 && abs(_DstBlend - 10) < 0.5)
                    c.rgb *= c.a;   // the standard shader's Transparent mode premultiplies
                return c;
            }
            ENDCG
        }
    }
}
