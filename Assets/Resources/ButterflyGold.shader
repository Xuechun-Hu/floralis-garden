Shader "FloorGarden/Butterfly gold"
{
    Properties { _Written("Revealed strokes", Float) = -1
        _LetterInk("Bronze lettering",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+6" }
        Pass
        {
            Cull Off ZTest LEqual ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../FloorGarden/FloraLetterInk.hlsl"
            float _Written, _LetterInk;
            struct Attr { float3 p:POSITION; float2 uv:TEXCOORD0; float2 ink:TEXCOORD1; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Vary { float4 p:SV_POSITION; float2 uv:TEXCOORD0; float2 ink:TEXCOORD1; half4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Vary vert(Attr a)
            {
                Vary o; UNITY_SETUP_INSTANCE_ID(a); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.p=TransformObjectToHClip(a.p); o.uv=a.uv; o.ink=a.ink; o.color=a.color; return o;
            }
            half4 frag(Vary i):SV_Target
            {
                clip(_Written-i.ink.x);
                float r=length(i.uv*2-1);
                float edge=max(fwidth(r),.07);
                half alpha=1-smoothstep(.91-edge,.91+edge,r);
                half glint=.5+.5*sin(_Time.y*3+i.ink.y*47);
                half3 gold=lerp(half3(.94,.56,.14),half3(1,.94,.62),glint);
                half rim=smoothstep(.57,.87,r);
                half3 ink=lerp(lerp(gold,half3(.36,.19,.055),rim),FloraLetterInk(glint,rim),_LetterInk);
                return half4(ink,alpha*i.color.a);
            }
            ENDHLSL
        }
    }
}
