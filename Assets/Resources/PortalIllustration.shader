Shader "FloorGarden/Portal illustration"
{
    Properties {
        [HideInInspector] _DepthTest("Depth test",Float)=4
        [HideInInspector] _DepthWrite("Depth write",Float)=1
        _MainTex("Illustration",2D)="white"{}
        _Tint("Ink",Color)=(1,1,1,1)
        _Portrait("Angel portrait",Float)=0
        _Blink("Blink",Range(0,1))=0
        _TouchGlow("Palm contact",Range(0,1))=0
        _WingPattern("Wing ink",Float)=0

    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Cull Off ZTest [_DepthTest] ZWrite [_DepthWrite]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint; half _Portrait; half _Blink; half _TouchGlow; half _WingPattern;
            CBUFFER_END
            struct Attr {float3 p:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Vary {float4 p:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float3 local:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            Vary Vert(Attr v) {Vary o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(v.p);o.p=TransformWorldToHClip(o.world);o.uv=v.uv;o.local=v.p;return o;}
            half4 Frag(Vary i):SV_Target {
                half4 ink=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*_Tint;
                clip(ink.a-0.35h);
                if (_TouchGlow>.001) {
                    float palm=length((i.uv-float2(.218,.350))/float2(.15,.095));
                    half warm=(1-smoothstep(.25,1,palm))*_TouchGlow;
                    ink.rgb=lerp(ink.rgb,half3(1,.78,.34),.36*warm);
                }
                if (_Portrait>.5 && _Blink>.001) {
                    float2 eye=i.uv.x<.506?float2(.456,.796):float2(.555,.814);
                    float2 d=i.uv-eye;
                    d.y-=.13*d.x;
                    float lid=saturate(1-pow(d.x/.031,2));
                    float aperture=.0075*lid;
                    float closure=smoothstep(-aperture-.001,-aperture+.001,d.y)
                          *(1-smoothstep(aperture+.001,aperture+.004,d.y))*lid;
                    half3 skin=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,float2(i.uv.x,eye.y+.012+.13*d.x)).rgb;
                    float lash=(1-smoothstep(.0007,.002,abs(d.y+.002)))*lid;
                    ink.rgb=lerp(ink.rgb,lerp(skin,skin*.34,lash),saturate(closure*1.6)*_Blink);
                }
                if (_WingPattern>1.5h) {
                    float u=i.uv.x,v=i.uv.y;
                    half3 root=ink.rgb*half3(.57,.58,.55);
                    half3 pearl=lerp(ink.rgb,half3(.97,.86,.67),.48);
                    ink.rgb=lerp(root,pearl,smoothstep(.03,.91,v));
                    float veins=abs(frac(3*u+.28*sin(v*3.1))-.5);
                    float fine=1-smoothstep(.008,.038+fwidth(veins),veins);
                    float rim=min(min(u,1-u),1-v);
                    ink.rgb=lerp(ink.rgb,half3(.59,.415,.22),.65*max(fine,1-smoothstep(.018,.055,rim)));
                    float spot=length(float2((frac(u*4)-.5)*1.1,(v-.84)*8));
                    ink.rgb=lerp(ink.rgb,half3(.95,.86,.66),.80*(1-smoothstep(.11,.21,spot)));
                } else if (_WingPattern>0.5h) {
                    float u=i.uv.x,v=i.uv.y;
                    float vein=abs(frac(u*4+.32*sin(v*4))-.5);
                    float rim=min(min(u,1-u),1-v);
                    float pigment=smoothstep(.025,.075,rim)*smoothstep(.004,.055,vein);
                    ink.rgb=lerp(half3(.12,.075,.052),ink.rgb,pigment);
                    float dotMask=length(float2(frac(u*7)-.5,(v-.9)*9));
                    ink.rgb=lerp(ink.rgb,half3(.99,.91,.70),1-smoothstep(.1,.2,dotMask));
                }
                return half4(ink.rgb,1);
            }
            ENDHLSL
        }
    }
}
