Shader "Custom/ParticleFun"
{
    Properties
    {
        _Color_Center("Center Color (Hot)", Color) = (1, 0.8, 0.6, 1)
        _Color_Outer("Outer Color (Cool)", Color) = (0.3, 0.6, 1.0, 1)
        _PointSize("Point Size", Float) = 5.0
        _TwinkleIntensity("Twinkle Intensity", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Particle
            {
                float3 position;
                float3 velocity;
                float life;
                float temp;
                float mass;
            };

            StructuredBuffer<Particle> particleBuffer;

            CBUFFER_START(UnityPerMaterial)
                float _PointSize;
                half4 _Color_Center;
                half4 _Color_Outer;
                float _TwinkleIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                uint instanceID   : SV_InstanceID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color        : COLOR;
                float size         : PSIZE;
            };

            // Simple hash function for pseudo-randomness
            float rand(uint seed)
            {
                seed = (seed << 13u) ^ seed;
                return frac((seed * (seed * seed * 15731u + 789221u) + 1376312589u) * 0.00000001);
            }

            // Simulate flickering using sine waves
            float twinkle(uint id, float life, float intensity)
            {
                float flicker = sin((life + id) * 5.0) * 0.5 + 0.5; // range [0,1]
                return lerp(1.0, flicker, intensity);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                Particle particle = particleBuffer[IN.instanceID];

                float temp = saturate(particle.temp * particle.temp);
                float life = saturate(particle.life);

                // Color based on distance with smooth transition
                half4 baseColor = lerp(_Color_Outer, _Color_Center, temp);

                // Additional color variation using particle mass (simulate star "temperature")
                float tempFactor = temp;
                float3 tempColor = lerp(baseColor.rgb, float3(1.0, 0.95, 0.8), tempFactor); // brighter for massive stars

                // Add subtle random tint for diversity
                float randVal = rand(IN.instanceID);
                float3 randomTint = lerp(float3(0.8, 0.9, 1.0), float3(1.0, 0.8, 0.6), randVal);

                // Twinkling effect (flickering)
                float flicker = twinkle(IN.instanceID, particle.life, _TwinkleIntensity);

                // Final color with life & flicker affecting brightness
                //OUT.color.rgb = tempColor * randomTint * flicker * life;
                OUT.color.rgb = baseColor;
                OUT.color.a   = life;

                OUT.size = _PointSize;

                // Position in clip space
                OUT.positionHCS = TransformObjectToHClip(particle.position);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return IN.color;
            }

            ENDHLSL
        }
    }
}
