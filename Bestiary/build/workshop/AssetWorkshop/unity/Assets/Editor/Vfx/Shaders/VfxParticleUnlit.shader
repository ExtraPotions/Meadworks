// Preview only: the game's Custom/Particle (Unlit), as its compiled code does it: the texture is a mask (one channel,
// _AlphaChannel, alpha by default), all colour comes from the particle; with Blend SrcColor One the colour is
// premultiplied by the alpha.
Shader "Workshop/Vfx/Particle Unlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _SrcBlend ("Src", Float) = 3
        _DstBlend ("Dst", Float) = 1
        _AlphaChannel ("Alpha channel", Float) = 3
        _SoftParticles ("Soft", Float) = 0
        _SoftFadeFactor ("Soft factor", Float) = 1
        _SoftNearFade ("Soft near", Float) = 0.5
        _CameraFadeFactor ("Camera fade", Float) = 1
        _VfxSRGB ("sRGB", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex VfxVert
            #pragma fragment frag
            #pragma target 3.0
            #include "VfxCommon.cginc"
            sampler2D _MainTex;
            float _SrcBlend, _AlphaChannel, _SoftParticles, _SoftFadeFactor, _SoftNearFade, _CameraFadeFactor;

            float4 frag(VfxOut i) : SV_Target
            {
                float4 tex = VfxTex(_MainTex, i.uv);
                float mask = tex[(int)_AlphaChannel];
                float soft = _SoftParticles > 0.5 ? VfxSoft(i.screen, i.depth + _SoftNearFade, _SoftFadeFactor) : 1;
                float a = mask * i.color.a * soft * saturate(i.depth * _CameraFadeFactor);
                float3 rgb = abs(_SrcBlend - 3) < 0.5 ? i.color.rgb * a : i.color.rgb;
                return float4(rgb, a);
            }
            ENDCG
        }
    }
}
