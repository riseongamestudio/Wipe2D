// URP Sprite-Unlit-Default plus a mask that caps the sprite's alpha. Keeps SRP Batcher compatibility.
Shader "RiseOn/Wipe2D/Sprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [NoScaleOffset] _MaskTex ("Wipe Mask", 2D) = "white" {}
        [HideInInspector] _MaskST ("Mask ST", Vector) = (1, 1, 0, 0)
        [MaterialToggle] _ZWrite ("ZWrite", Float) = 0

        // Legacy properties, kept so the material can fall back to the legacy sprite shader.
        [HideInInspector] _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _MaskST;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                half4 col = CommonUnlitFragment(input, input.color);

                // Mask UV is the sprite's sub-rect remapped to 0..1. The mask starts as the sprite's alpha,
                // so min() leaves untouched areas exactly as authored. LOD 0 on purpose: the mask's mips exist
                // only for the progress readback and are regenerated lazily, so any other level may be stale.
                half mask = SAMPLE_TEXTURE2D_LOD(_MaskTex, sampler_MaskTex, input.uv * _MaskST.xy + _MaskST.zw, 0).r;
                col.a = min(col.a, mask);

                return col;
            }
            ENDHLSL
        }
    }
}