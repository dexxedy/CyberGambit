Shader "CyberGambit/FogOfWarAction"
{
    Properties
    {
        _ExploredTex ("Explored", 2D) = "black" {}
        _VisibleTex ("Visible", 2D) = "black" {}
        _WorldMinXZ ("World Min XZ", Vector) = (0, 0, 0, 0)
        _WorldMaxXZ ("World Max XZ", Vector) = (16, 16, 0, 0)
        _FogColor ("Fog Color", Color) = (0.02, 0.05, 0.09, 1)
        _FogAlbedoTex ("Fog Albedo", 2D) = "white" {}
        _UseFogAlbedoTex ("Use Fog Albedo", Float) = 0
        _FogAlbedoTile ("Fog Albedo Tile", Vector) = (4, 4, 0, 0)
        _FogAlbedoTint ("Fog Albedo Tint", Color) = (1, 1, 1, 1)
        _UnexploredAlpha ("Unexplored Alpha", Range(0, 1)) = 0.92
        _ExploredAlpha ("Explored Alpha", Range(0, 1)) = 0.55
        _HeightStart ("Height Start", Float) = -1
        _HeightEnd ("Height End", Float) = 12
        _SkyDepthThreshold ("Sky Depth Threshold", Range(0.9, 1)) = 0.99
        _VisibleFade ("Visible Fade", Float) = 0.12
        _ExploredFade ("Explored Fade", Float) = 0.08
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "FogOfWarAction"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "FogOfWarCommon.hlsl"

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);
            TEXTURE2D(_ExploredTex);
            SAMPLER(sampler_ExploredTex);
            TEXTURE2D(_VisibleTex);
            SAMPLER(sampler_VisibleTex);

            float4 _WorldMinXZ;
            float4 _WorldMaxXZ;
            float _UnexploredAlpha;
            float _ExploredAlpha;
            float _HeightStart;
            float _HeightEnd;
            float _SkyDepthThreshold;
            float _VisibleFade;
            float _ExploredFade;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                float4 positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
#if UNITY_UV_STARTS_AT_TOP
                positionCS.y = -positionCS.y;
#endif
                output.positionCS = positionCS;
                output.uv = uv;
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

            bool IsSkyDepth(float rawDepth)
            {
#if UNITY_REVERSED_Z
                return rawDepth <= 1e-4;
#else
                return rawDepth >= _SkyDepthThreshold;
#endif
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);

                float rawDepth = SampleSceneDepth(uv);
                if (IsSkyDepth(rawDepth))
                    return sceneColor;

                float3 worldPos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float2 fogUv = WorldXZToFogUV(worldPos);

                float visible = SAMPLE_TEXTURE2D(_VisibleTex, sampler_VisibleTex, fogUv).r;
                float explored = SAMPLE_TEXTURE2D(_ExploredTex, sampler_ExploredTex, fogUv).r;

                float visOcclude = 1.0 - smoothstep(0.0, _VisibleFade, visible);
                float exploredAmt = smoothstep(0.0, _ExploredFade, explored);
                float unexpFactor = visOcclude * (1.0 - exploredAmt);
                float expFactor = visOcclude * exploredAmt;

                float fogStrength = unexpFactor * _UnexploredAlpha + expFactor * _ExploredAlpha;
                float heightMul = smoothstep(_HeightEnd, _HeightStart, worldPos.y);
                float finalFog = saturate(fogStrength * heightMul);

                half3 fogRgb = FogOfWarGetRgb(fogUv);
                return lerp(sceneColor, half4(fogRgb, 1.0), finalFog);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
