Shader "FloorGarden/Figure alignment"
{
    Properties { _MainTex("Target outline",2D)="black"{} _Ready("Aligned",Range(0,1))=0 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest+5"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); float _Ready;
            struct Input { float3 p:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Vary { float4 p:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO };
            Vary Vert(Input i) { Vary o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(i.p);o.uv=i.uv;return o; }
            half4 Frag(Vary i):SV_Target {half a=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).r;return half4(lerp(half3(.83,.49,.14),half3(1,.96,.66),_Ready),a*lerp(.72,1,_Ready));}
            ENDHLSL
        }
    }
}
