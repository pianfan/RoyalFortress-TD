Shader "RoyalFortress/Registered Stone Engraving Glow"
{
    Properties { [PerRendererData] _MainTex ("Map texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            float box(float2 p, float4 bounds) { return step(bounds.x,p.x)*step(bounds.y,p.y)*step(p.x,bounds.z)*step(p.y,bounds.w); }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 stone=tex2D(_MainTex,i.uv);
                float2 p=float2(i.uv.x,1-i.uv.y)*float2(1585,992);
                float region=saturate(box(p,float4(734,720,769,779))+box(p,float4(880,720,920,779))
                    +box(p,float4(712,798,754,868))+box(p,float4(897,798,938,868)));
                // Select the turquoise engraving already present in the map, never draw a replacement glyph.
                float cyan=smoothstep(.025,.12,min(stone.g,stone.b)-stone.r);
                return fixed4(saturate(stone.rgb*1.65),region*cyan*i.color.a);
            }
            ENDCG
        }
    }
}
