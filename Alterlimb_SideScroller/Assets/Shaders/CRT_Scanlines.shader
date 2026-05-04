Shader "UI/CRT_Scanlines"
{
    Properties
    {
        [Header(Scanlines)]
        _ScanlineCount("Nombre de lignes", Range(50, 500)) = 200
        _ScanlineIntensity("Intensite des lignes", Range(0, 1)) = 0.4
        _ScanlineSpeed("Vitesse de defilement", Range(-5, 5)) = 0.5
        
        [Header(Vignette)]
        _VignetteStrength("Force du vignettage", Range(0, 2)) = 0.8
        _VignetteSoftness("Douceur du vignettage", Range(0.1, 1)) = 0.5
        
        [Header(Flicker)]
        _FlickerIntensity("Intensite du flicker", Range(0, 0.3)) = 0.05
        _FlickerSpeed("Vitesse du flicker", Range(0, 50)) = 15
        
        [Header(Color)]
        _TintColor("Teinte (vert classique CRT)", Color) = (0.6, 1, 0.7, 1)
        _BaseAlpha("Alpha de base", Range(0, 1)) = 0.5
    }
    
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            float _ScanlineCount;
            float _ScanlineIntensity;
            float _ScanlineSpeed;
            float _VignetteStrength;
            float _VignetteSoftness;
            float _FlickerIntensity;
            float _FlickerSpeed;
            float4 _TintColor;
            float _BaseAlpha;
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }
            
            // Hash simple pour générer du bruit pseudo-aléatoire
            float hash(float n)
            {
                return frac(sin(n) * 43758.5453);
            }
            
            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                
                // ── 1. Scanlines horizontales ─────────────
                // Décalage temporel pour que les lignes défilent légèrement
                float scanlineOffset = _Time.y * _ScanlineSpeed;
                float scanline = sin((uv.y + scanlineOffset) * _ScanlineCount * 3.14159);
                // Convertit [-1, 1] en [0, 1] et applique l'intensité
                scanline = 1.0 - (1.0 - (scanline * 0.5 + 0.5)) * _ScanlineIntensity;
                
                // ── 2. Vignette (assombrissement des coins) ────
                float2 center = uv - 0.5;
                float vignette = 1.0 - dot(center, center) * _VignetteStrength;
                vignette = saturate(vignette);
                vignette = pow(vignette, 1.0 / _VignetteSoftness);
                
                // ── 3. Flicker (variation aléatoire d'intensité) ───
                float flicker = 1.0 - hash(floor(_Time.y * _FlickerSpeed)) * _FlickerIntensity;
                
                // ── Combine tout ──────────────────────────
                float darkening = scanline * vignette * flicker;
                
                // L'effet final est UN OVERLAY SOMBRE :
                // - là où darkening = 1 → totalement transparent (image en dessous visible)
                // - là où darkening < 1 → on assombrit avec la teinte CRT
                float effectStrength = (1.0 - darkening) * _BaseAlpha;
                
                half4 col = _TintColor;
                col.a = effectStrength;
                
                // Inverse la couleur pour que l'effet ASSOMBRISSE plutôt qu'éclaire
                col.rgb = lerp(half3(0, 0, 0), col.rgb, 0.3);
                
                return col;
            }
            ENDHLSL
        }
    }
}
