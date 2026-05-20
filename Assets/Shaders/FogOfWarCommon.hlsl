#ifndef CYBERGAMBIT_FOG_OF_WAR_COMMON_INCLUDED
#define CYBERGAMBIT_FOG_OF_WAR_COMMON_INCLUDED

TEXTURE2D(_FogAlbedoTex);
SAMPLER(sampler_FogAlbedoTex);

float _UseFogAlbedoTex;
float4 _FogAlbedoTile;
float4 _FogColor;
float4 _FogAlbedoTint;

half3 FogOfWarGetRgb(float2 fogUv)
{
    if (_UseFogAlbedoTex > 0.5)
    {
        float2 albedoUv = fogUv * _FogAlbedoTile.xy + _FogAlbedoTile.zw;
        half3 albedo = SAMPLE_TEXTURE2D(_FogAlbedoTex, sampler_FogAlbedoTex, albedoUv).rgb;
        return albedo * _FogAlbedoTint.rgb * _FogColor.rgb;
    }

    return _FogColor.rgb;
}

#endif
