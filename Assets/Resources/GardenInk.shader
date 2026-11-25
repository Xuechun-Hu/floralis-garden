Shader "FloorGarden/Painted foliage"
{
    Properties { [HideInInspector] _Cull ("Cull",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct Attributes { float4 pos:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 pos:SV_POSITION; float3 world:TEXCOORD0; half3 n:TEXCOORD1; float2 uv:TEXCOORD2; half3 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(v.pos.xyz); o.pos=TransformWorldToHClip(o.world);
                o.n=TransformObjectToWorldNormal(v.normal); o.uv=v.uv; o.color=v.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                o.color=SRGBToLinear(o.color);
                #endif
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n=normalize(i.n), view=GetWorldSpaceNormalizeViewDir(i.world);
                if(dot(n,view)<0) n=-n;
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half diffuse=saturate(0.27h+0.73h*dot(n,sun.direction));
                half3 baseColor=i.color;
                if(i.uv.x>1)
                {
                    float s=i.uv.x-2, y=i.uv.y;
                    float edge=min(min(s,1-s),1-y);
                    half edgeInk=1-smoothstep(0.004,0.012+fwidth(edge),edge);
                    float pleat=abs(frac(s*5+sin(y*2)*0.24)-0.5);
                    half fold=(1-smoothstep(0.02,0.08+fwidth(pleat),pleat))*(1-y)*0.18;
                    baseColor=lerp(baseColor,half3(0.24,0.14,0.085),max(0.70*edgeInk,fold));
                }
                else if(i.uv.x>=0)
                {
                    float x=abs(i.uv.x-0.5)*2, y=i.uv.y;
                    float aa=max(fwidth(x),0.015);
                    half spine=1-smoothstep(0.015,0.037+aa,x);
                    float lateral=abs(frac(y*5.5-x*1.3)-0.5);
                    half veins=(1-smoothstep(0.012,0.055+fwidth(lateral),lateral))*saturate(1-x);
                    half contour=smoothstep(0.90-aa,0.975+aa,x)*smoothstep(0.025,0.13,y);
                    baseColor=lerp(baseColor,half3(0.53,0.34,0.11),0.52*spine+0.22*veins);
                    baseColor=lerp(baseColor,half3(0.082,0.056,0.029),0.69*contour);
                }
                half shine=pow(saturate(dot(n,SafeNormalize(view+sun.direction))),18)*0.045;
                half band=lerp(diffuse,smoothstep(0.26,0.47,diffuse),0.32);
                half3 lit=baseColor*(half3(0.64,0.68,0.59)+sun.color*half3(0.55,0.48,0.42)*band*sun.shadowAttenuation);
                return half4(lit+shine*sun.color*sun.shadowAttenuation,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
