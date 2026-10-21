// Preview only: the game's Custom/Gradient Mapped Particle (Unlit), as its compiled code does it (read from the game's
// own shader, disassembled): one texture channel g picks a colour between the particle's two custom data colours
// (Custom1 where g is 1, Custom2 where it is 0), times the particle colour; g is also the alpha (GradientAsAlpha);
// soft and camera fades multiply the alpha; with Blend SrcColor One (the game's usual) the colour is premultiplied.
Shader "Workshop/Vfx/Gradient Mapped"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _SrcBlend ("Src", Float) = 3
        _DstBlend ("Dst", Float) = 1
        _GradientChannel ("Gradient channel", Float) = 0
        _GradientAsAlpha ("Gradient as alpha", Float) = 1
        _SoftParticles ("Soft", Float) = 1
        _SoftFadeFactor ("Soft factor", Float) = 1
        _SoftNearFade ("Soft near", Float) = 0
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
            float _SrcBlend, _GradientChannel, _GradientAsAlpha, _SoftParticles, _SoftFadeFactor, _SoftNearFade, _CameraFadeFactor;

            float4 frag(VfxOut i) : SV_Target
            {
                float4 tex = VfxTex(_MainTex, i.uv);
                float g = tex[(int)_GradientChannel];
                float4 grad = lerp(i.custom2, i.custom1, g);
                float soft = _SoftParticles > 0.5 ? VfxSoft(i.screen, i.depth + _SoftNearFade, _SoftFadeFactor) : 1;
                float a = soft * i.color.a * saturate(i.depth * _CameraFadeFactor);
                float shape = _GradientAsAlpha > 0.5 ? g : tex.a;
                bool srcColor = abs(_SrcBlend - 3) < 0.5;
                float3 rgb = (srcColor ? i.color.rgb * a : i.color.rgb) * (srcColor ? grad.rgb * shape : grad.rgb);
                float alpha = a * (srcColor ? grad.a * shape : grad.a) * shape;
                return float4(rgb, alpha);
            }
            ENDCG
        }
    }
}
