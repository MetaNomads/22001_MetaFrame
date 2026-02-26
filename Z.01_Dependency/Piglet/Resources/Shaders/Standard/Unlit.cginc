#ifndef UNLIT_INCLUDED
#define UNLIT_INCLUDED

#pragma target 3.0

sampler2D _baseColorTexture;
float4 _baseColorFactor;

struct Input
{
    // Note: The `uv` prefix "magically" maps to
    // the corresponding shader property, e.g.
    // `uv_baseColorTexture` -> `_baseColorTexture`.

    float2 uv_baseColorTexture;

    // Note: COLOR faults to (1,1,1,1) if unset

    float4 vertexColor : COLOR;
};

fixed4 LightingUnlit(SurfaceOutput s, fixed3 lightDir, fixed atten)
{
    return fixed4(s.Albedo, s.Alpha);
}

void surf (Input IN, inout SurfaceOutput o)
{
    fixed4 c = tex2D (_baseColorTexture, IN.uv_baseColorTexture) * IN.vertexColor * _baseColorFactor;
    o.Albedo = c.rgb;
    o.Alpha = c.a;
}

#endif