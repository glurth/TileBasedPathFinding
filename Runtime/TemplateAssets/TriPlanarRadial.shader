Shader "Custom/TriplanerTexRadial"
{
    Properties
    {
        _BaseMapXY("Texture", 2D) = "white" {}
        _BaseMapXZ("Texture", 2D) = "white" {}
        _BaseMapYZ("Texture", 2D) = "white" {}

    }

        SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 TransformObjectToWorldInstanced(float3 posOS)
            {
                return mul(UNITY_MATRIX_M, float4(posOS, 1.0)).xyz;
                //return mul(unity_ObjectToWorld, float4(posOS, 1.0)).xyz;
            }

            float4 TransformWorldToHClipInstanced(float3 posWS)
            {
                return mul(UNITY_MATRIX_VP, float4(posWS, 1.0));
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
               // float4 debugColor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };



            TEXTURE2D(_BaseMapXY);
            SAMPLER(sampler_BaseMapXY);
            float4 _BaseMapXY_ST;

            TEXTURE2D(_BaseMapXZ);
            float4 _BaseMapXZ_ST;

            TEXTURE2D(_BaseMapYZ);
            float4 _BaseMapYZ_ST;

            // float4 _TexScaleXY, _TexScaleXZ, _TexScaleYZ;


             Varyings vert(Attributes v)
             {
                 Varyings o;
                 UNITY_SETUP_INSTANCE_ID(v);
                 UNITY_TRANSFER_INSTANCE_ID(v, o);
//                 float centerOffset = v.positionOS.y;

                 float3 posnormalized = v.positionOS / v.positionOS.Length;
                 o.worldPos = mul(unity_ObjectToWorld, posnormalized);
                 o.positionCS = mul(UNITY_MATRIX_VP, o.worldPos);

                // o.positionCS = mul(UNITY_MATRIX_MVP, v.positionOS);
               //  o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                 o.normalWS = TransformObjectToWorldNormal(posnormalized);
                 posnormalized
                 //o.debugColor = float4(pos.x*0.5 + 0.5, 0, 0, 1);
                 return o;
             }

             half4 frag(Varyings i) : SV_Target
             {
                 float3 n = normalize(i.normalWS);
                 float3 w = abs(n);
                 float total = w.x + w.y + w.z;
                 w /= total; // Normalize weights

                 float2 uvXY = i.worldPos.xy * _BaseMapXY_ST;
                 float2 uvXZ = i.worldPos.xz * _BaseMapXZ_ST;
                 float2 uvYZ = i.worldPos.yz * _BaseMapYZ_ST;
                 //new
                 half4 texColorXY = SAMPLE_TEXTURE2D(_BaseMapXY, sampler_BaseMapXY, uvXY);
                 half4 texColorXZ = SAMPLE_TEXTURE2D(_BaseMapXZ, sampler_BaseMapXY, uvXZ);
                 half4 texColorYZ = SAMPLE_TEXTURE2D(_BaseMapYZ, sampler_BaseMapXY, uvYZ);
                 half4 texColor = texColorXY * w.z + texColorXZ * w.y + texColorYZ * w.x;

                 //old
                 //float2 uv = uvXY * w.z + uvXZ * w.y + uvYZ * w.x;
                 //half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);

                 Light light = GetMainLight();
                 half3 normal = normalize(i.normalWS);
                 half3 lightDir = normalize(light.direction);
                 half NdotL = saturate(dot(normal, -lightDir));
                 half3 color = texColor.rgb * (NdotL * light.color + 0.1); // ambient fudge
                // return half4(i.debugColor);
                 return half4(color, 1);
             }
             ENDHLSL
         }

         UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }

        FallBack "Universal Render Pipeline/Lit"
}
