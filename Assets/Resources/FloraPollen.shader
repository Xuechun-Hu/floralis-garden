Shader "FloorGarden/Flora pollen"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float3 p:POSITION;float2 uv:TEXCOORD0;half4 c:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;half alpha:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A v){V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(v.p);o.uv=v.uv;o.alpha=v.c.a;return o;}
            float2 hash(float2 id){
                float3 q=frac(id.xyx*float3(.1031,.1030,.0973));
                q+=dot(q,q.yzx+33.33);return frac((q.xx+q.yz)*q.zy);
            }
            float flakes(float2 p){
                float2 id=floor(p),h=hash(id);
                float2 delta=(frac(p)-lerp(.18,.82,h))*float2(1,1.6);
                float r=length(delta), w=max(.045,min(.35,fwidth(r)));
                float radius=.09+.19*h.y;
                return (1-smoothstep(radius-w,radius+w,r))*(.4+.6*h.x);
            }
            half4 frag(V i):SV_Target{
                float2 p=i.uv*2-1;
                p.y+=.21*sin(p.x*4.6+.45);
                float halo=saturate(1-dot(p*float2(1.06,1.25),p*float2(1.06,1.25)));
                float filament=.5+.5*sin(p.y*11+p.x*3+.9*sin(p.x*5));
                float grains=flakes(i.uv*11)+.48*flakes(i.uv.yx*18+3.7);
                float mist=halo*halo*(.07+.18*filament);
                float alpha=saturate((mist+grains*halo*.95)*i.alpha);
                return half4(lerp(half3(.79,.45,.12),half3(1,.89,.54),saturate(grains+.25*filament)),alpha);
            }
            ENDHLSL
        }
    }
}
