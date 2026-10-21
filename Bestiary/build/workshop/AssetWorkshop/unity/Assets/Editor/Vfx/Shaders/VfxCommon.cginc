// Shared by the workshop's preview copies of the game's particle shaders. The workshop project renders in gamma
// colour space and the game in linear, so these shaders do the game's conversions themselves and write linear HDR
// into a float target (VfxPreview grades it like the game's camera afterwards): sRGB textures and material colours are
// linearised; particle vertex colours are not (the game's renderers leave them unconverted). Lighting comes from
// globals VfxLighting sets each frame instead of Unity's light passes, so it is linear too.
#ifndef VFX_COMMON_INCLUDED
#define VFX_COMMON_INCLUDED
#include "UnityCG.cginc"

float4 _VfxSunDir;          // towards the sun, world space
float4 _VfxSunColor;        // linear colour times intensity
float4 _VfxAmbient;         // linear ambient
float4 _VfxLightPos[4];     // xyz position, w range
float4 _VfxLightColor[4];   // linear colour times intensity
float _VfxLightCount;
float _VfxSRGB;             // 1 when the material's texture is sRGB in the game (all but normal-packed flipbooks)
UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

float3 VfxLinear(float3 c) { return pow(max(c, 0), 2.2); }
float4 VfxTex(sampler2D tex, float2 uv)
{
    float4 c = tex2D(tex, uv);
    c.rgb = lerp(c.rgb, VfxLinear(c.rgb), _VfxSRGB);
    return c;
}

// Unity's built-in point light falloff, near enough: 1 / (1 + 25 d^2) faded to nothing at the range.
float3 VfxPointLights(float3 worldPos, float3 normal, float wrap)
{
    float3 sum = 0;
    for (int i = 0; i < 4; i++)
    {
        if (i >= (int)_VfxLightCount) break;
        float3 toLight = _VfxLightPos[i].xyz - worldPos;
        float d = length(toLight) / max(_VfxLightPos[i].w, 0.001);
        float atten = saturate(1 - d) * (1.0 / (1.0 + 25.0 * d * d));
        float lambert = saturate((dot(normal, normalize(toLight)) + wrap) / (1 + wrap));
        sum += _VfxLightColor[i].rgb * atten * lambert;
    }
    return sum;
}

float3 VfxLighting(float3 worldPos, float3 normal, float wrap)
{
    float sun = saturate((dot(normal, _VfxSunDir.xyz) + wrap) / (1 + wrap));
    return _VfxAmbient.rgb + _VfxSunColor.rgb * sun + VfxPointLights(worldPos, normal, wrap);
}

// Soft particles: 0 where the particle touches the scene, 1 once it is `distance` metres in front of it.
float VfxSoft(float4 screenPos, float eyeDepth, float factor)
{
    float scene = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(screenPos)));
    return saturate((scene - eyeDepth) * factor);
}

// No NORMAL here: a particle renderer with custom vertex streams (the game's gradient-mapped flames stream Position,
// Color, UV, Custom1, Custom2) draws nothing for a shader that asks for a stream it lacks. Particles face the camera.
struct VfxIn
{
    float4 vertex : POSITION;
    float4 color : COLOR;
    float4 uv0 : TEXCOORD0;     // uv, then Custom1.xy when the renderer streams custom data
    float4 uv1 : TEXCOORD1;     // Custom1.zw, Custom2.xy
    float4 uv2 : TEXCOORD2;     // Custom2.zw
};

struct VfxOut
{
    float4 pos : SV_POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
    float4 custom1 : TEXCOORD1;
    float4 custom2 : TEXCOORD2;
    float4 screen : TEXCOORD3;
    float3 world : TEXCOORD4;
    float3 normal : TEXCOORD5;
    float depth : TEXCOORD6;
};

VfxOut VfxVertWith(VfxIn v, float3 objectNormal)
{
    VfxOut o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.color = v.color;
    o.uv = v.uv0.xy;
    o.custom1 = float4(v.uv0.zw, v.uv1.xy);
    o.custom2 = float4(v.uv1.zw, v.uv2.xy);
    o.screen = ComputeScreenPos(o.pos);
    o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
    float3 n = UnityObjectToWorldNormal(objectNormal);
    float3 toCamera = normalize(_WorldSpaceCameraPos - o.world);
    o.normal = dot(n, n) > 0.25 ? normalize(n) : toCamera;
    o.depth = -UnityObjectToViewPos(v.vertex).z;
    return o;
}

VfxOut VfxVert(VfxIn v) { return VfxVertWith(v, float3(0, 0, 0)); }

// Meshes (the preview's ground and figure, mesh debris) do have normals.
struct VfxMeshIn
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 color : COLOR;
    float4 uv0 : TEXCOORD0;
};

VfxOut VfxVertMesh(VfxMeshIn v)
{
    VfxIn p;
    p.vertex = v.vertex;
    p.color = v.color;
    p.uv0 = v.uv0;
    p.uv1 = 0;
    p.uv2 = 0;
    return VfxVertWith(p, v.normal);
}
#endif
