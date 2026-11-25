Shader "FloorGarden/Lily carving to gold flecks"
{
    Properties { _MainTex("Carving",2D)="white"{} _Age("Crossing age",Float)=-1 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest"}
        Pass
        {
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);float _Age;
            struct Input {float3 p:POSITION;float2 uv:TEXCOORD0;float2 center:TEXCOORD1;float4 landing:TEXCOORD2;float3 normal:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Vary {float4 p:SV_POSITION;float2 uv:TEXCOORD0;float2 life:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            Vary Vert(Input i)
            {
                Vary o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float seed=frac(sin(dot(i.center,float2(71.53,243.91)))*2347.89);
                float delay=.45+1.72*i.center.y+.42*seed;
                float life=max(0,_Age-delay);
                float flight=.83+.63*seed;
                float t=saturate(life/flight);
                float2 delta=(i.uv-i.center)*float2(1.34,1.67);
                float3 origin=i.p-float3(delta,0);
                float phase=6.283*seed;
                float3 drift=i.p+t*float3(.19*sin(phase+t*3),.37+.29*seed,-.15-.24*t);
                drift.xy-=delta*.88*smoothstep(0,.7,t);
                float alpha=1-smoothstep(.45,1,t);
                if(i.landing.w>.5)
                {
                    float3 center=lerp(origin,i.landing.xyz,float3(t,t*t,t));
                    center.y+=(.23+.21*seed)*sin(3.14159*t);
                    center.x+=.10*sin(phase+t*3)*sin(3.14159*t);
                    float size=lerp(1,.25,smoothstep(0,.7,t));
                    float3 tangent=normalize(float3(1,0,0)-i.normal*i.normal.x);
                    float3 bitangent=cross(tangent,i.normal);
                    float3 resting=delta.x*tangent+delta.y*bitangent;
                    drift=center+size*lerp(float3(delta,0),resting,smoothstep(.72,1,t));
                    alpha=1-smoothstep(flight+.54,flight+1.33,life);
                }
                o.p=TransformObjectToHClip(drift);o.uv=i.uv;
                o.life=float2(smoothstep(delay-.16,delay+.22,_Age),alpha);
                return o;
            }
            half4 Frag(Vary i):SV_Target
            {
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);clip(c.a-.35);clip(i.life.y-.009);
                return half4(lerp(c.rgb,half3(1.12,.77,.32),i.life.x*.88),i.life.y);
            }
            ENDHLSL
        }
    }
}
