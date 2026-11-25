Shader "FloorGarden/Painted stream"
{
 Properties {_FloorSector("Floor sector",Float)=0 _GroundEyeHeight("Capture eye height",Float)=1.5 _CloudInterior("Cloud interior pigment",2D)="black"{} _GrassStrokes("Grass brushwork",2D)="gray"{} _MeadowOpacity("Garden blend",Range(0,1))=1 _OpeningBreak("Opening dissolve",Float)=-1 _MainTex("Panorama tile",2D)="white"{} _Flow("Water coverage and current",2D)="black"{} _FlowSpeed("Stream playback speed",Range(0,4))=2.4 _Region("Tile scale and offset",Vector)=(1,1,0,0)}
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "DisableBatching"="True"}
 Pass {
 Cull Off Blend One OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma multi_compile_local _ GARDEN_SURROUND
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
 TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);TEXTURE2D(_Flow);SAMPLER(sampler_Flow);
 TEXTURE2D(_CloudInterior); SAMPLER(sampler_CloudInterior);
 float _FloraWeatherTime;
 half _MeadowOpacity; float _GroundEyeHeight,_FloorSector;
 float _OpeningBreak; float4 _Region; float _FlowSpeed;
 struct A{float3 p:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V {
   float4 p:SV_POSITION; float2 uv:TEXCOORD0; float2 opening:TEXCOORD1;
   #if defined(GARDEN_SURROUND)
   float3 ground:TEXCOORD2;
   #endif
   UNITY_VERTEX_OUTPUT_STEREO
 };
 V vert(A a)
 {
   V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
   o.uv=a.uv;o.opening=a.p.xy;
   #if defined(GARDEN_SURROUND)
   o.ground=0;
   if(_FloorSector>.5 && a.uv.y<1)
   {
     // Project actual floor vertices. Homogeneous scaling keeps angular UVs well
     // conditioned at the horizon while preserving the physical depth of every point.
     float invRange=rsqrt(dot(a.p.xz,a.p.xz)+_GroundEyeHeight*_GroundEyeHeight);
     o.ground=float3(a.p.xz*invRange,invRange);
     o.p=TransformObjectToHClip(a.p)*invRange;
     #if UNITY_REVERSED_Z
     o.p.z=max(o.p.z,UNITY_RAW_FAR_CLIP_VALUE*o.p.w);
     #else
     o.p.z=min(o.p.z,UNITY_RAW_FAR_CLIP_VALUE*o.p.w);
     #endif
   }
   else
   {
     float3 dir=normalize(a.p);
     float3 view=TransformWorldToViewDir(TransformObjectToWorldDir(dir));
     o.p=mul(UNITY_MATRIX_P,float4(view,0));
     o.p.z=UNITY_RAW_FAR_CLIP_VALUE*o.p.w;
     o.ground=float3(dir.xz,0);
   }
   #else
   o.p=TransformObjectToHClip(a.p);
   #endif
   return o;
 }
 #if defined(GARDEN_SURROUND)
 TEXTURE2D(_GrassStrokes);SAMPLER(sampler_GrassStrokes);
 half3 GroundBrushwork(V i,float2 g,half3 background)
 {
   float strength=smoothstep(.69,.87,1-g.y);
   if(strength<=0)return background;
   float2 groundXZ=i.ground.xy/max(i.ground.z,.0001);
   half stroke=SAMPLE_TEXTURE2D(_GrassStrokes,sampler_GrassStrokes,groundXZ*.86).g-.5h;
   return background*exp2(.65h*strength*stroke);
 }
 #endif
 half4 frag(V i):SV_Target
 {
   float2 g=i.uv*_Region.xy+_Region.zw;
   half3 base=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgb;
   if(_Region.w>.25 && g.y>.60)
   {
     float cloud=SAMPLE_TEXTURE2D(_CloudInterior,sampler_CloudInterior,float2(g.x,2*g.y-1)).r;
     if(cloud>.02)
     {
       // Each painted curl keeps its outline. Only its interior brushwork and light breathe.
       float lon=g.x*TWO_PI;
       float curl=2.5*sin(lon)+1.4*cos(3*lon+g.y*14)+g.y*21;
       float breeze=sin(_FloraWeatherTime*.29+curl);
       float eddy=sin(_FloraWeatherTime*.19-curl*.8);
       float2 shift=float2(16*breeze+5*eddy,5*eddy)/float2(16384,8192);
       float2 tileEdge=min(i.uv,1-i.uv);
       float margin=smoothstep(0,.009,min(tileEdge.x,tileEdge.y));
       half3 pigment=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+shift*cloud*margin/_Region.xy).rgb;
       base=lerp(base,pigment*(1+.032*breeze+.012*eddy),cloud);
     }
   }
   if (_OpeningBreak>=0)
   {
     float cell=frac(sin(dot(floor(i.opening*91),float2(15.17,61.23)))*1863.17);
     float edge=saturate((i.opening.y+.8)/1.6)*.79+cell*.20-_OpeningBreak;
     clip(edge);
     base=lerp(base,half3(1,.76,.34),1-smoothstep(0,.04,edge));
   }
   // Flow map spans the full longitude and top-image rows 960..1535 of a 2048-high panorama.
   float2 uv=float2(g.x,(g.y-.25)/.28125);
   half3 flow=0;
   // The distant flower beds and water share pale pigments. Keep the horizon still,
   // and cap filtered coverage by the original interior mask at the shoreline.
   if(1-g.y>450./887. && uv.y>=0 && uv.y<=1)
   {
     flow=SAMPLE_TEXTURE2D(_Flow,sampler_Flow,uv).rgb;
     flow.r=min(flow.r,SAMPLE_TEXTURE2D_LOD(_Flow,sampler_Flow,uv,0).r);
     flow.r=smoothstep(.06h,.9h,flow.r);
   }
   #if defined(GARDEN_SURROUND)
   if(_FloorSector>.5 && flow.r<.004h)base=GroundBrushwork(i,g,base);
   #endif
   if(flow.r<.004)return half4(base*_MeadowOpacity,_MeadowOpacity);
   float2 dir=2*flow.gb-1;
   float seconds=_FloraWeatherTime*_FlowSpeed;
   float t=seconds*.49+g.x*3.2+g.y*1.7,phase=frac(t),other=frac(t+.5);
   float2 motion=dir*float2(20./16384.,20./8192.)/_Region.xy*flow.r;
   half3 a=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv-(phase-.5)*motion).rgb;
   half3 b=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv-(other-.5)*motion).rgb;
   half3 water=lerp(a,b,abs(2*phase-1));
   half ripple=saturate(sin(g.y*4300+g.x*430+sin(g.x*330)-seconds*2.8));
   water+=half3(.030,.028,.019)*pow(ripple,12)*flow.r;
   return half4(lerp(base,water,flow.r)*_MeadowOpacity,_MeadowOpacity);
 }
 ENDHLSL
 }}}
