// Off-screen drawing into the single-channel wipe mask. Pipeline-agnostic: only ever drawn through
// a CommandBuffer, never by the scene renderer. Every pass, init and fade included, goes through the
// same quad + projection so they all stay aligned by construction.
Shader "Hidden/RiseOn/Wipe2D/Stamp"
{
    Properties
    {
        _SpriteTex ("Sprite", 2D) = "white" {}
        _SpriteRect ("Sprite Rect (uv)", Vector) = (0, 0, 1, 1)
        _TexSize ("Mask Size (texels)", Vector) = (1, 1, 0, 0)
        _Segment ("Stroke Segment A.xy B.zw (texels)", Vector) = (0, 0, 0, 0)
        _Radius ("Radius (texels)", Float) = 1
        _Hardness ("Hardness", Range(0, 1)) = 0.8
        _Fade ("Fade To.x Delta.y", Vector) = (0, 0, 0, 0)
        _ClipTex ("Clip", 2D) = "white" {}
        _ClipRect ("Clip Rect (uv)", Vector) = (0, 0, 1, 1)
        _ClipParams ("Clip Cutoff.x Inside.y", Vector) = (0, 1, 0, 0)
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _SpriteTex;
    float4 _SpriteRect;
    float4 _TexSize;
    float4 _Segment;
    float _Radius;
    float _Hardness;
    float4 _Fade;
    sampler2D _ClipTex;
    float4 _ClipRect;
    float4 _ClipParams;
    float4x4 _ClipMatrix;

    struct appdata
    {
        float4 vertex : POSITION;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 maskUV : TEXCOORD0;
    };

    // The quad is placed in mask UV space by the object matrix; view is identity, projection is ortho 0..1,
    // so object-to-world directly yields the mask UV of each vertex.
    v2f vertStamp (appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.maskUV = mul(unity_ObjectToWorld, v.vertex).xy;
        return o;
    }

    // Capsule coverage: 1 inside the flat core, linear falloff to 0 at the rim; hardness widens the core.
    // A point stamp is a zero-length segment.
    float Coverage (float2 maskUV)
    {
        float2 p  = maskUV * _TexSize.xy;
        float2 a  = _Segment.xy;
        float2 ab = _Segment.zw - a;
        float  h  = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-6));
        float  t  = length(p - a - ab * h) / _Radius;
        return saturate((1 - t) / max(1 - _Hardness, 1e-4));
    }

    // Sprite alpha, further limited by the clip. Being off the clip sprite is its own answer, kept apart
    // from the cutoff: a cutoff of 0 passes every texel of the mask, which is what SpriteMask does, but it
    // must not turn the whole plane into mask. Outside the rect is always out, whatever the cutoff.
    float SpriteAlpha (float2 maskUV)
    {
        float alpha = tex2D(_SpriteTex, _SpriteRect.xy + maskUV * _SpriteRect.zw).a;

        float2 clipUV = mul(_ClipMatrix, float4(maskUV, 0, 1)).xy;
        float  inRect = (all(clipUV >= 0) && all(clipUV <= 1)) ? 1 : 0;
        float  clipA  = tex2D(_ClipTex, _ClipRect.xy + saturate(clipUV) * _ClipRect.zw).a;
        float  inside = inRect * step(_ClipParams.x, clipA);

        return alpha * lerp(1 - inside, inside, _ClipParams.y);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // 0: Erase. mask = min(mask, 1 - coverage)
        Pass
        {
            BlendOp Min
            Blend One One

            CGPROGRAM
            #pragma vertex vertStamp
            #pragma fragment frag

            fixed4 frag (v2f i) : SV_Target
            {
                return 1 - Coverage(i.maskUV);
            }
            ENDCG
        }

        // 1: Reveal. mask = max(mask, coverage * spriteAlpha), so the mask never exceeds the sprite's own alpha
        // and the visible ratio stays comparable to the baseline.
        Pass
        {
            BlendOp Max
            Blend One One

            CGPROGRAM
            #pragma vertex vertStamp
            #pragma fragment frag

            fixed4 frag (v2f i) : SV_Target
            {
                return Coverage(i.maskUV) * SpriteAlpha(i.maskUV);
            }
            ENDCG
        }

        // 2: Init, drawn as a quad covering the whole mask. mask = sprite alpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vertStamp
            #pragma fragment frag

            fixed4 frag (v2f i) : SV_Target
            {
                return SpriteAlpha(i.maskUV);
            }
            ENDCG
        }

        // 3: Fade, drawn as a quad covering the whole mask. mask = lerp(mask, to * spriteAlpha, delta)
        // through plain alpha blending, so nothing reads the mask back and the alpha ceiling still holds.
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vertStamp
            #pragma fragment frag

            fixed4 frag (v2f i) : SV_Target
            {
                float to = _Fade.x * SpriteAlpha(i.maskUV);
                return fixed4(to, to, to, _Fade.y);
            }
            ENDCG
        }
    }
}