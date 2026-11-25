Shader "FloorGarden/ViewTint"
{
    Properties { _Opacity ("Opacity", Range(0,1)) = 0.055 }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            half _Opacity;
            struct Attributes { float3 pos : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 pos : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = float4(v.pos.xy, UNITY_NEAR_CLIP_VALUE, 1);
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half opacity = _Opacity;
                return half4(half3(0.90h, 0.72h, 0.53h) * opacity, opacity);
            }
            ENDHLSL
        }
    }
}
