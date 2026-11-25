Shader "FloorGarden/Angel gaussian splats"
{
    Properties { _Scatter("Scatter time",Float)=-1 _Portrait("Source portrait",2D)="white"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_Portrait); SAMPLER(sampler_Portrait);
            float _Scatter;
            struct Input {float3 p:POSITION;float4 color:COLOR;float2 corner:TEXCOORD0;float3 a:TEXCOORD1;float3 b:TEXCOORD2;float3 c:TEXCOORD3;float2 uv:TEXCOORD4;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Vary {float4 p:SV_POSITION;half4 color:COLOR;float2 g:TEXCOORD0;float3 detail:TEXCOORD1;float lightBand:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            float2 ProjectAxis(float3 axis,float3 v)
            {
                float3 d=mul((float3x3)UNITY_MATRIX_MV,axis);
                return d.xy-d.z*v.xy/min(v.z,-.025);
            }
            Vary Vert(Input i)
            {
                Vary o; UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p=i.p;
                float radius=length(p);
                float noise=frac(sin(dot(i.uv,float2(75.23,187.1)))*43758.55);
                float t=saturate((_Scatter-.52*radius-.14*noise)/.94);
                float mote=step(.974,noise);
                float phase=noise*516+i.uv.y*4;
                p+=t*t*float3(.28*sin(phase+t*3),.45+.48*noise,-.19+.21*cos(phase));
                float3 v=mul(UNITY_MATRIX_MV,float4(p,1)).xyz;
                float2 a=ProjectAxis(i.a,v),b=ProjectAxis(i.b,v),c=ProjectAxis(i.c,v);
                float regular=.00135*length(mul((float3x3)UNITY_MATRIX_MV,float3(1,0,0)));
                float xx=a.x*a.x+b.x*b.x+c.x*c.x+regular*regular;
                float xy=a.x*a.y+b.x*b.y+c.x*c.y;
                float yy=a.y*a.y+b.y*b.y+c.y*c.y+regular*regular;
                float l00=sqrt(max(xx,1e-10)),l10=xy/l00,l11=sqrt(max(yy-l10*l10,1e-10));
                float2 g=2.75*i.corner;
                float2 billboard=float2(l00*g.x,l10*g.x+l11*g.y);
                // Project the splat footprint back into the calibrated SHARP input photograph.
                float3 worldOffset=mul((float3x3)UNITY_MATRIX_I_V,float3(billboard,0));
                float3 delta=mul((float3x3)unity_WorldToObject,worldOffset);
                float depth=max(i.p.z+1.47*.85+delta.z,.05);
                float2 uv=i.uv+(float2(1.110,.888)*delta.xy-(i.uv-.5)*delta.z)/depth;
                float facing=saturate(-TransformWorldToObjectDir(GetWorldSpaceNormalizeViewDir(TransformObjectToWorld(i.p))).z);
                o.detail=float3(uv,smoothstep(.35,.82,facing)*(1-smoothstep(0,.18,t)));
                float height=saturate((i.p.y+.55)/1.25);
                o.lightBand=(_Scatter>=0) ? (1-smoothstep(.01,.13,abs(_Scatter*1.75-height))) : 0;
                v.xy+=lerp(billboard,(.0003+.0021*mote)*g,smoothstep(0,.72,t));
                o.p=mul(UNITY_MATRIX_P,float4(v,1));o.g=g;
                float3 rgb=i.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    rgb=SRGBToLinear(rgb);
                #endif
                o.color=half4(lerp(rgb,half3(1,.69,.25),smoothstep(.03,.48,t)),i.color.a*(1-smoothstep(lerp(.08,.65,mote),lerp(.53,1,mote),t)));
                return o;
            }
            half4 Frag(Vary i):SV_Target{
                half alpha=i.color.a*exp(-.5*dot(i.g,i.g));clip(alpha-.005);
                half4 detail=SAMPLE_TEXTURE2D(_Portrait,sampler_Portrait,i.detail.xy);
                half3 pigment=lerp(i.color.rgb,detail.rgb,.93*i.detail.z*detail.a);
                pigment=lerp(pigment,half3(1.3,.88,.39),i.lightBand*.78);
                return half4(pigment*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
