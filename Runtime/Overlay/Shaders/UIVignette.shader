Shader "TutorialKit/UIVignette"
{
    // Full-screen UGUI overlay that dims the screen and cuts one OR MORE soft-edged holes (circle or
    // rounded rectangle) around targets. Works under any render pipeline because it renders through
    // the Canvas, not the SRP. Hole parameters are in screen pixels, driven from VignetteView.
    // Up to 8 holes: _Centers[i].xy = centre, _Sizes[i].xy = half-size, _Sizes[i].z = shape (0=circle,
    // 1=rect), _Sizes[i].w = corner radius. The final alpha is the union of all holes (min).
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Overlay Color", Color) = (0,0,0,0.78)
        _Softness ("Softness", Float) = 0.15
        _ScreenSize ("Screen Size", Vector) = (1920,1080,0,0)
        _HoleCount ("Hole Count", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGBA

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define TK_MAX_HOLES 8

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float4 color     : COLOR;
                float4 screenPos : TEXCOORD0;
            };

            fixed4 _Color;
            float  _Softness;
            float4 _ScreenSize;
            float  _HoleCount;
            float4 _Centers[TK_MAX_HOLES];
            float4 _Sizes[TK_MAX_HOLES];

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.pos);
                o.color = v.color * _Color;
                return o;
            }

            float HoleAlpha(float2 pix, int i)
            {
                float2 c = _Centers[i].xy;
                float2 size = _Sizes[i].xy;
                float shape = _Sizes[i].z;
                float corner = _Sizes[i].w;
                float2 p = pix - c;
                float d;
                if (shape < 0.5)
                {
                    float radius = max(size.x, size.y);
                    d = length(p) - radius;
                }
                else
                {
                    float r = min(corner, min(size.x, size.y));
                    float2 q = abs(p) - (size - r);
                    d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
                }
                float soft = max(1.0, _Softness * min(size.x, size.y));
                return smoothstep(-soft, soft, d);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 pix = i.screenPos.xy / max(i.screenPos.w, 1e-5) * _ScreenSize.xy;
                float a = 1.0;

                for (int h = 0; h < TK_MAX_HOLES; h++)
                {
                    if (float(h) >= _HoleCount) break;
                    a = min(a, HoleAlpha(pix, h));
                }

                fixed4 col = i.color;
                col.a *= a;
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
