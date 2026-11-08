Shader "RoyalFortress/Forest Overview Backdrop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Map", 2D) = "white" {}
        _BlurPixels ("Blur", Float) = 16
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False" }
        Cull Off ZWrite Off
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
            float4 _MainTex_TexelSize;
            float _BlurPixels;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float2 d=_MainTex_TexelSize.xy*_BlurPixels;
                fixed4 c=tex2D(_MainTex,i.uv)*.2;
                c+=tex2D(_MainTex,saturate(i.uv+float2(d.x,0)))*.1;
                c+=tex2D(_MainTex,saturate(i.uv-float2(d.x,0)))*.1;
                c+=tex2D(_MainTex,saturate(i.uv+float2(0,d.y)))*.1;
                c+=tex2D(_MainTex,saturate(i.uv-float2(0,d.y)))*.1;
                c+=tex2D(_MainTex,saturate(i.uv+d))*.1;
                c+=tex2D(_MainTex,saturate(i.uv-d))*.1;
                c+=tex2D(_MainTex,saturate(i.uv+float2(d.x,-d.y)))*.1;
                c+=tex2D(_MainTex,saturate(i.uv+float2(-d.x,d.y)))*.1;
                return c*i.color;
            }
            ENDCG
        }
    }
}
