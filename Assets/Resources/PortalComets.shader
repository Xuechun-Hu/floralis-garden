Shader "FloorGarden/Portal comets"
{
    Properties { _Age("Seconds since scattering", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+25" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _Age;
            struct Input { float4 position:POSITION; float2 uv:TEXCOORD0; float2 id:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Vary { float4 position:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            float hash(float id) { return frac(sin(id*19.731+3.14)*3216.23); }
            float3 path(float t, float id, float seed)
            {
                float angle=id*2.39996;
                float reach=lerp(.55,1.16,seed);
                if (id < 24)
                {
                    float ascending=angle+(.35+t*1.05)*3.4*(fmod(id,2)<1?-1:1);
                    return float3((.25+.40*t)*cos(ascending),-.52+1.64*t,
                                  -.25+.18*sin(ascending)-.46*t);
                }
                float a=angle+sin(2.9*t+angle)*.55+t*(seed-.5);
                return float3(cos(a)*(.13+t*reach),.02+sin(a)*(.22+t*reach)+.14*t,
                              -.22-t*(.12+.42*seed));
            }
            Vary Vert(Input i)
            {
                Vary o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float id=i.id.x, seed=hash(id);
                float age=(_Age-.08-.58*seed)/(1.14+.59*hash(id+5));
                float t=saturate(age-(1-i.uv.x)*lerp(.07,.23,seed));
                float3 p=path(t,id,seed);
                float3 tangent=TransformObjectToWorldDir(path(t+.003,id,seed)-p);
                float3 world=TransformObjectToWorld(p);
                float3 lateral=cross(GetWorldSpaceNormalizeViewDir(world),tangent);
                lateral*=rsqrt(max(dot(lateral,lateral),.00001));
                float head=exp(-pow((i.uv.x-.89)*10,2));
                float width=(.0025+.004*seed)*(.17+.83*i.uv.x+1.65*head);
                world+=lateral*width*i.uv.y;
                o.position=TransformWorldToHClip(world);
                o.uv=i.uv;
                float3 pigment=id<54 ? float3(1,.51,.12) : (id<70 ? float3(.92,.45,.61) : float3(.32,.78,.73));
                o.color=half4(lerp(pigment,float3(1,.94,.73),head*.83),
                              smoothstep(0,.12,age)*(1-smoothstep(.81,1.05,age)));
                return o;
            }
            half4 Frag(Vary i):SV_Target
            {
                float crossFade=saturate(1-abs(i.uv.y));
                float endFade=smoothstep(0,.35,i.uv.x)*(1-smoothstep(.9,1,i.uv.x));
                return half4(i.color.rgb*crossFade*endFade*i.color.a*1.7,0);
            }
            ENDHLSL
        }
    }
}
