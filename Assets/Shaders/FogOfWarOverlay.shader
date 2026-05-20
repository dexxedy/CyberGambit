Shader "CyberGambit/FogOfWarOverlay"
{
    Properties
    {
        _ExploredTex ("Explored", 2D) = "black" {}
        _VisibleTex ("Visible", 2D) = "black" {}
        _WorldMinXZ ("World Min XZ", Vector) = (0, 0, 0, 0)
        _WorldMaxXZ ("World Max XZ", Vector) = (16, 16, 0, 0)
        _FogColor ("Fog Color", Color) = (0.02, 0.04, 0.07, 1)
        _FogAlbedoTex ("Fog Albedo", 2D) = "white" {}
        _UseFogAlbedoTex ("Use Fog Albedo", Float) = 0
        _FogAlbedoTile ("Fog Albedo Tile", Vector) = (4, 4, 0, 0)
        _FogAlbedoTint ("Fog Albedo Tint", Color) = (1, 1, 1, 1)
        _UnexploredAlpha ("Unexplored Alpha", Range(0, 1)) = 0.92
        _ExploredAlpha ("Explored Alpha", Range(0, 1)) = 0.55
        _VisibleFade ("Visible Fade", Float) = 0.12
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+200"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "FogOfWarOverlay"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "FogOfWarCommon.hlsl"

            TEXTURE2D(_ExploredTex);
            SAMPLER(sampler_ExploredTex);
            TEXTURE2D(_VisibleTex);
            SAMPLER(sampler_VisibleTex);

            float4 _WorldMinXZ;
            float4 _WorldMaxXZ;
            float _UnexploredAlpha;
            float _ExploredAlpha;
            float _VisibleFade;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.worldPos = world;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                return output;
            }

            float2 WorldXZToFogUV(float3 worldPos)
            {
                float2 minXZ = _WorldMinXZ.xy;
                float2 maxXZ = _WorldMaxXZ.xy;
                float2 uv;
                uv.x = (worldPos.x - minXZ.x) / max(0.0001, maxXZ.x - minXZ.x);
                uv.y = (worldPos.z - minXZ.y) / max(0.0001, maxXZ.y - minXZ.y);
                return saturate(uv);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 fogUv = WorldXZToFogUV(input.worldPos);
                float visible = SAMPLE_TEXTURE2D(_VisibleTex, sampler_VisibleTex, fogUv).r;
                float explored = SAMPLE_TEXTURE2D(_ExploredTex, sampler_ExploredTex, fogUv).r;

                float visOcclude = 1.0 - smoothstep(0.0, _VisibleFade, visible);
                if (visOcclude <= 0.001)
                    discard;

                float exploredAmt = smoothstep(0.0, _VisibleFade, explored);
                float alpha = lerp(_UnexploredAlpha, _ExploredAlpha, exploredAmt);
                alpha *= visOcclude;

                half3 fogRgb = FogOfWarGetRgb(fogUv);
                return half4(fogRgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
