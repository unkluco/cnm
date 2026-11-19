Shader "LightSouls/VFX/Arcane Particle"
{
    Properties
    {
        _Pattern("Pattern",Float)=0
        _Glow("Radiance",Float)=1.2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source blend",Float)=5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination blend",Float)=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+14" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Pattern,_Glow;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR; };
            Varyings vert(Attributes i){Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color;return o;}
            half4 frag(Varyings i):SV_Target
            {
                float2 d=i.uv*2.0-1.0;
                float r=length(d);
                float a=0,core=0;
                float3 rgb=i.color.rgb;
                if(_Pattern<0.5) // Soft magic mote
                {a=pow(saturate(1.0-r),1.6);core=pow(saturate(1.0-r),4.0);}
                else if(_Pattern<1.5) // Tapered sparks / streaks
                {
                    float diamond=abs(d.x)+abs(d.y)*0.63;
                    a=(1.0-smoothstep(0.35,0.82,diamond))*(1.0-smoothstep(0.78,1.0,abs(d.y)));
                    core=1.0-smoothstep(0.04,0.28,diamond);
                }
                else if(_Pattern<2.5) // A curved, two-tone flame tongue
                {
                    float y=saturate(i.uv.y);
                    float bend=0.16*sin(y*5.2)+0.16*y;
                    float wid=(0.88*pow(saturate(1.0-y),0.72)+0.04)*smoothstep(0.0,0.16,y);
                    float w=abs(d.x-bend)/max(0.035,wid);
                    a=(1.0-smoothstep(0.67,1.05,w))*(1.0-smoothstep(0.92,1.0,y));
                    core=(1.0-smoothstep(0.12,0.64,w))*(1.0-smoothstep(0.40,0.82,y));
                    rgb=lerp(rgb,float3(1.0,0.23,0.045),smoothstep(0.38,0.98,y)*0.52);
                    rgb=lerp(rgb,float3(1.0,0.85,0.36),core*0.88);
                }
                else if(_Pattern<3.5) // Matte smoke and dust (alpha blend)
                {
                    float wisps=0.12*sin(d.x*12.0+d.y*9.0)*sin(d.y*13.0-d.x*8.0);
                    a=pow(saturate(1.0-r+wisps),1.5)*0.72;
                }
                else if(_Pattern<4.5) // Faceted ice shard / rune
                {
                    float crystal=abs(d.x)*1.8+abs(d.y)*0.83;
                    a=1.0-smoothstep(0.82,0.98,crystal);
                    core=(1.0-smoothstep(0.02,0.17,abs(d.x-0.08)))*a;
                    rgb*=d.x<0.0?0.80:1.12;
                }
                else if(_Pattern<5.5) // A short star-shaped impact / muzzle flash
                {
                    float h=pow(saturate(1.0-abs(d.y)*9.0),2.0)*pow(saturate(1.0-abs(d.x)),1.6);
                    float v=pow(saturate(1.0-abs(d.x)*9.0),2.0)*pow(saturate(1.0-abs(d.y)),1.6);
                    core=pow(saturate(1.0-r*2.0),2.0);
                    a=saturate(h+v+pow(saturate(1.0-r),3.0)*0.65);
                }
                else { a=1.0; }
                rgb=lerp(rgb,float3(1.0,0.93,0.77),core*0.65)*_Glow;
                return half4(rgb,a*i.color.a);
            }
            ENDHLSL
        }
    }
}
