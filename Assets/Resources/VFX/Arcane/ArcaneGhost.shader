Shader "LightSouls/VFX/Arcane Ghost"
{
    Properties { [HDR] _Tint("Tint",Color)=(0.28,0.79,0.85,0.5) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Back ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float3 positionWS:TEXCOORD1;};
            Varyings vert(Attributes i){Varyings o;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(i.normalOS);return o;}
            half4 frag(Varyings i):SV_Target
            {
                float grazing=1.0-saturate(abs(dot(normalize(i.normalWS),normalize(GetWorldSpaceViewDir(i.positionWS)))));
                float rim=grazing*grazing;
                return half4(_Tint.rgb*(0.60+rim*0.9),_Tint.a*(0.50+rim*0.50));
            }
            ENDHLSL
        }
    }
}
