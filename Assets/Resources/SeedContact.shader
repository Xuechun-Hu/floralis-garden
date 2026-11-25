Shader "FloorGarden/Seed contact"
{
    Properties { _Contact("Contact strength",Float)=0 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent"}
        Pass
        {
            Cull Off ZWrite Off Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _Contact;
            struct A {float3 p:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A v){V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(v.p);o.uv=v.uv;return o;}
            half4 frag(V i):SV_Target{float2 p=i.uv*2-1;half a=_Contact*pow(saturate(1-dot(p,p)),3);return half4(half3(.13,.085,.043)*a,a);}
            ENDHLSL
        }
    }
}
