Shader "LightSouls/VFX/Arcane Glyph"
{
    Properties
    {
        [HDR] _Accent("Element", Color) = (1,0.3,0.1,1)
        _Progress("Charge", Range(0,1)) = 0
        _Opacity("Opacity", Range(0,1)) = 1
        _Shape("Shape (circle / lane)", Float) = 0
        _Style("Style (danger / blessing / aim / pulse)", Float) = 0
        _Element("Element pattern", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+8" "RenderType"="Transparent" }
        Pass
        {
            Name "ArcaneGround"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Accent;
                float _Progress, _Opacity, _Shape, _Style, _Element;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings vert(Attributes i)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; return o;
            }
            float band(float value, float center, float width)
            {
                return 1.0-smoothstep(width*0.48,width,abs(value-center));
            }
            half4 frag(Varyings i):SV_Target
            {
                float p=saturate(_Progress);
                float2 xy=i.uv*2.0-1.0;
                float r=length(xy);
                float pulse=0.70+0.30*sin(p*10.0);
                bool blessing=_Style>0.5 && _Style<1.5;
                bool aim=_Style>1.5 && _Style<2.5;
                bool impact=_Style>2.5;
                float3 danger=float3(1.0,0.20,0.075);
                float3 hot=blessing?_Accent.rgb:lerp(danger,_Accent.rgb,0.28);
                if(aim||impact)hot=_Accent.rgb;
                float edge=0, ornament=0, charge=0, fill=0;
                if(_Shape<0.5)
                {
                    clip(1.0-r);
                    float polar=(atan2(xy.y,xy.x)+PI)/(2.0*PI);
                    float tick=1.0-smoothstep(0.045,0.14,abs(frac(polar*12.0+0.5)-0.5));
                    float major=1.0-smoothstep(0.06,0.15,abs(frac(polar*6.0+0.5)-0.5));
                    float arcDash=smoothstep(0.17,0.30,frac(polar*8.0));
                    edge=band(r,0.973,0.027);
                    ornament=band(r,0.865,0.021)*arcDash+tick*smoothstep(0.77,0.82,r)*(1.0-smoothstep(0.91,0.94,r));
                    // Six inlaid diamonds read as a runic seal, not as damage geometry.
                    ornament+=major*band(r,0.81,0.045)*(0.45+0.55*p);
                    // Element-specific inscriptions live inside the exact same warning boundary.
                    if(_Element>0.5 && _Element<1.5) // ember: serrated tongues
                    {
                        float tooth=abs(frac(polar*12.0)-0.5)*2.0;
                        ornament+=band(r,0.72+tooth*0.07,0.018)*0.75;
                    }
                    else if(_Element>1.5 && _Element<2.5) // frost: six crystalline facets
                    {
                        float hex=cos((frac(polar*6.0)-0.5)*PI/3.0);
                        ornament+=band(r,0.71/max(0.87,hex),0.020)*0.7;
                        ornament+=major*smoothstep(0.37,0.48,r)*(1.0-smoothstep(0.61,0.72,r))*0.42;
                    }
                    else if((_Element>2.5 && _Element<3.5)||_Element>6.5) // spectral / blood magic
                        ornament+=band(r,0.53+sin(polar*6.0*PI+p*1.6)*0.055,0.025)*arcDash*0.66;
                    else if(_Element>4.5 && _Element<5.5) // stone: fracture spokes
                        ornament+=major*band(r,0.56+sin(polar*17.0)*0.085,0.075)*0.58;
                    charge=band(r,lerp(0.17,0.947,p),0.033)*(0.45+0.55*p);
                    fill=(0.08+0.13*p)*(0.72+0.28*pow(r,1.5));
                    if(blessing)
                    {
                        float crossS=min(abs(xy.x),abs(xy.y));
                        ornament+= (1.0-smoothstep(0.028,0.055,crossS))*(1.0-smoothstep(0.22,0.29,r))*0.75;
                        fill*=0.8;
                    }
                    if(aim)
                    {
                        fill=0.012;edge*=0.55;ornament*=0.52;charge=0;
                    }
                    if(impact)
                    {
                        fill=0;edge=band(r,0.90,0.085);charge=band(r,0.66,0.058)*0.55;
                        ornament=(band(r,0.80,0.025)*arcDash+major*band(r,0.81,0.07))*0.62;
                    }
                }
                else
                {
                    // Lanes keep their exact rectangular width but use arrows, light rails and a travelling charge line.
                    float x=abs(xy.x);
                    float y=i.uv.y;
                    edge=band(x,0.94,0.065)+band(y,0.965,0.022)*0.7+band(y,0.035,0.022)*0.45;
                    float stepY=frac(y*5.0-p*0.7);
                    float chevron=band(stepY,0.34+x*0.26,0.083)*(1.0-smoothstep(0.69,0.85,x));
                    ornament=chevron*0.68+band(x,0.69,0.045)*smoothstep(0.25,0.46,frac(y*9.0))*0.45;
                    charge=band(y,lerp(0.04,0.96,p),0.05)*(1.0-smoothstep(0.86,1.0,x));
                    fill=(0.09+0.12*p)*(0.72+0.28*x);
                }
                float3 body=lerp(hot,_Accent.rgb,0.42)*0.86;
                float marks=saturate(ornament+charge);
                float luminous=saturate(edge+marks*0.72);
                float3 rgb=lerp(body,lerp(hot*1.28,_Accent.rgb*1.36,saturate(marks)),luminous);
                if(!aim && !impact)rgb=lerp(rgb,float3(1.65,0.75,0.32),charge*p*p*0.45);
                if(blessing||aim||impact)rgb=lerp(rgb,_Accent.rgb*1.32,luminous);
                float a=saturate(fill+edge*(aim?0.62:0.80)+ornament*(aim?0.42:0.45)+charge*0.58);
                return half4(rgb,a*saturate(_Opacity)*saturate(_Accent.a)*(impact?1.0:pulse));
            }
            ENDHLSL
        }
    }
}
