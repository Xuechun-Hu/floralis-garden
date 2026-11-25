Shader "FloorGarden/Portal light"
{
 Properties {_Glow("Glow",Range(0,1))=0}
 SubShader {
 Tags {"Queue"="Transparent+2" "RenderPipeline"="UniversalPipeline"}
 Pass {
 Cull Off ZWrite Off Blend One OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float _Glow;
 struct A{float3 p:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(a.p);o.uv=a.uv;return o;}
 half4 frag(V i):SV_Target{float2 p=2*i.uv-1;float r=length(p);half halo=saturate(1-r)*.13+exp(-19*abs(r-.72))*.55;half a=halo*_Glow;return half4(half3(1,.81,.39)*a,a);}
 ENDHLSL
 }}}
