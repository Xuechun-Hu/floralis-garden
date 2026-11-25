Shader "FloorGarden/Porcelain hands"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+12" }
        Pass
        {
            Cull Back ZWrite Off Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input { float3 p:POSITION; float3 normal:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 clip:SV_POSITION; half3 normal:TEXCOORD0; float3 world:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Output Vert(Input v) {
                Output o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(v.p);
                o.clip=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(v.normal);
                return o;
            }
            half4 Frag(Output i):SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n=normalize(i.normal);
                half light=smoothstep(-.55,.65,dot(n,normalize(float3(-.4,.9,-.3))));
                half rim=1-smoothstep(.06,.28,saturate(dot(n,GetWorldSpaceNormalizeViewDir(i.world))));
                half3 porcelain=lerp(half3(.40,.25,.19),half3(.88,.75,.57),light);
                half opacity=lerp(.36h,.60h,rim);
                return half4(lerp(porcelain,half3(.36,.23,.095),rim*.68)*opacity,opacity);
            }
            ENDHLSL
        }
    }
}
