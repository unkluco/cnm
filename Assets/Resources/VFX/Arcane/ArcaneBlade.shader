Shader "LightSouls/VFX/Arcane Blade"
{
    Properties
    {
        [HDR] _Tint("Element", Color) = (1,0.9,0.7,1)
        _Fade("Fade", Range(0,1)) = 1
        _Progress("Sweep", Range(0,1)) = 0
        _TrailMode("Blade ribbon / projectile trail", Float) = 0
        _Element("Element", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+12" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Fade,_Progress,_TrailMode,_Element;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR; };
            Varyings vert(Attributes i){Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color;return o;}
            half4 frag(Varyings i):SV_Target
            {
                float u=saturate(i.uv.x),v=saturate(i.uv.y),p=saturate(_Progress);
                float body=smoothstep(0.015,0.29,v)*(1.0-smoothstep(0.91,1.0,v));
                float core=1.0-smoothstep(0.025,0.14,abs(v-0.78));
                float filament=1.0-smoothstep(0.016,0.052,abs(v-(0.51+0.055*sin(u*23.0-p*12.0))));
                float sweep=smoothstep(-0.14,0.06,p*2.2+0.22-u);
                float endFade=smoothstep(0.0,0.10,u)*(1.0-smoothstep(0.9,1.0,u));
                if(_TrailMode>0.5)
                {
                    sweep=1.0;endFade=1.0;
                    body=smoothstep(0.02,0.35,v)*(1.0-smoothstep(0.80,1.0,v));
                    core=1.0-smoothstep(0.035,0.21,abs(v-0.71));
                }
                float3 tint=_Tint.rgb*i.color.rgb;
                // The outer edge is a thin, hot silver line; the interior is a feathered elemental wake.
                float ember=(_Element>0.5 && _Element<1.5)?1.0:0.0;
                float3 edgeTint=lerp(tint,float3(1.0,0.92,0.75),0.67);
                edgeTint=lerp(edgeTint,float3(1.0,0.84,0.38),ember*0.75);
                float3 rgb=tint*(0.78+filament*0.34)+edgeTint*core*1.24;
                float alpha=(body*0.70+core*0.54+filament*0.20)*endFade*sweep*_Fade*_Tint.a*i.color.a;
                return half4(rgb,saturate(alpha));
            }
            ENDHLSL
        }
    }
}
