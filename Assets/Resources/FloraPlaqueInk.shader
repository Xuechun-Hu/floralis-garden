Shader "FloorGarden/Flora plaque ink"
{
    Properties { _MainTex("Print",2D)="white"{} _Row("Feedback row",Float)=-1
        [HideInInspector] _DepthWrite("Depth write",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" }
        Pass
        {
            Cull Off ZWrite [_DepthWrite] ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../FloorGarden/FloraLetterInk.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); float _Row;
            struct A {float3 p:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(a.p);o.uv=a.uv;return o;}
            half4 frag(V i):SV_Target{
                float2 uv=i.uv;
                if(_Row>=0)uv.y=(7-_Row+uv.y)/8;
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv);
                if(_Row<0){clip(c.a-.35h);return c;}
                half coverage=c.a;
                half shimmer=.5h+.5h*sin(_Time.y+dot(uv,float2(68,29)));
                return half4(FloraLetterInk(shimmer,1-smoothstep(.12h,.92h,coverage)),coverage);
            }
            ENDHLSL
        }
    }
}
