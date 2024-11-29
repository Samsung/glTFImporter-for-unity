Shader "Custom/VisualSoftwarePBScg" {
    Properties{
		baseColorSampler("BaseColorTexture", 2D) ="black" {}
		metallicRoughnessSampler("MetallicRoughnessTexture", 2D) = "black" {}
		emissiveSampler("EmissiveTexture", 2D) = "black" {}
		normalSampler("NormalTexture", 2D) = "black" {}

		u_basecolor_factor("BaseColorFactor", Vector) = (1, 1, 1, 1)
		u_emissive_factor("EmissiveFactor", Vector) = (0, 0, 0, 0)
		u_metallic_roughness_factor("MetallicRoughnessFactor", Vector) = (0, 0, 0, 0)
			
		u_sampler_usage("SamplerUsage",Vector) = (0,0,0,0)
		u_blend_usage("BlendUsage",Vector) = (0,0,0,0)
		u_primitive_usage("PrimitiveUsage",Vector) = (0,0,0,0)

	}

    SubShader{
        Tags{ "Queue" = "Geometry" }

        Pass{
			Cull Off
			CGPROGRAM
			// #pragma debug // enable to see errors and warnings
            #pragma target 3.5
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			#define MAX_LIGHT_NUM 3 //if changed, change number of getDirectLight() function callings; line 211-213
			#define MAX_JOINT_NUM 70			
			#define MAX_LIGHT_INTENSITY_NUM 5
			#define IDX_LIGHT_IBL_IRRADIANCE 3
			#define IDX_LIGHT_IBL_SPECULAR 4

			uniform sampler2D baseColorSampler;
			uniform sampler2D metallicRoughnessSampler;
			uniform sampler2D emissiveSampler;
			uniform sampler2D normalSampler;
			uniform sampler2D alphaSampler;

			uniform samplerCUBE irradianceIBLSampler;
			uniform samplerCUBE specularIBLSampler;
			uniform sampler2D brdfLUTSampler;
					
			uniform float4 u_sampler_usage;
			uniform float4 u_blend_usage;

			uniform float u_light_intensity[MAX_LIGHT_INTENSITY_NUM];

			uniform float4 u_basecolor_factor;
			uniform float4 u_emissive_factor;
			uniform float4 u_metallic_roughness_factor;
			uniform float4x4 u_ReflMatrix;

			float4 baseColorSampler_ST;
			uniform float4 u_primitive_usage;

			uniform float4x4 u_NormalMatrix;
			uniform float2 u_texcoord_offset;
			uniform float3 u_light_pos[MAX_LIGHT_NUM];
			uniform float3 u_view_pos;

			static const float M_PI = 3.141592653589793;
			static const float M_F0 = 0.04;
			static const float M_GAMMA = 2.2;
			static const float3 M_GAMMA_3 = {M_GAMMA, M_GAMMA, M_GAMMA};

			struct v2f {
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 view : VEIW;
				float3 light[MAX_LIGHT_NUM] : LIGHT;
				float3 halfway[MAX_LIGHT_NUM] : HALFWAY;
#ifdef TBN_MATRIX
				float3x3 tbn;
#else
				float3 tangent : TANGENT;
				float3 bitangent : BITANGENT;
				float3 normal : NORMAL;
#endif
			};

            v2f vert(appdata_tan v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord + u_texcoord_offset, baseColorSampler);

#ifdef NORMAL_VS
                float3 local_model_pos = mul(u_NormalMatrix, o.pos).xyz;
                float3 normal = normalize(mul(u_NormalMatrix, float4(v.normal, 1.0))).xyz;   // N
                float3 tangent = normalize(mul(u_NormalMatrix, v.tangent)).xyz;            // T
#else
                float3 local_model_pos = o.pos.xyz;
                float3 normal = normalize(mul(u_NormalMatrix, float4(v.normal, 1.0))).xyz;   // N
                float3 tangent = normalize(mul(u_NormalMatrix, v.tangent)).xyz;            // T
#endif
                float3 bitangent = normalize(cross(normal, tangent));                  // B

#ifdef TBN_MATRIX
                o.tbn = float3x3(tangent, bitangent, normal);                            // TBN
#else
                o.tangent = tangent;
                o.bitangent = bitangent;
                o.normal = normal;
#endif

                o.light[0] = normalize(u_light_pos[0]);                  // L : directional light
                o.light[1] = normalize(u_light_pos[1]);
                o.light[2] = normalize(u_light_pos[2]);

                o.view = normalize(u_view_pos - local_model_pos);        // V
                o.halfway[0] = normalize(o.light[0] + o.view);           // H
                o.halfway[1] = normalize(o.light[1] + o.view);           // H
                o.halfway[2] = normalize(o.light[2] + o.view);           // H

                return o;
            }

			float3 lambertianDiffuse(float3 baseColor)
			{
				return baseColor / M_PI;
			}

			float3 fresnelSchlick(float3 reflectance0, float3 reflectance90, float VdotH)
			{
				return reflectance0 + (reflectance90 - reflectance0) * pow(clamp(1.0 - VdotH, 0.0, 1.0), 5.0);
			}

			float geometricOcclusionCookTorrance(float NdotV, float NdotH, float VdotH, float NdotL)
			{
				return min(min(2.0 * NdotV * NdotH / VdotH, 2.0 * NdotL * NdotH / VdotH), 1.0);
			}

			float microfacetDistribution(float roughnessBiq, float NdotH)
			{
				float f = (NdotH * roughnessBiq - NdotH) * NdotH + 1.0;
				return roughnessBiq / (M_PI * f * f);
			}

			float3 mix(float3 x, float3 y, float alpha) {
				return x * (1 - alpha) + y * alpha;
			}
            
            float3 SRGBtoLINEAR(float3 color)
            {
                return pow(color, float3(2.2, 2.2, 2.2));
            }

            float3 getDirectLight(v2f i, float3 normal, float3 specularEnvironmentR0, float3 specularEnvironmentR90, float NdotV, float roughnessBiq, float3 diffuseColor, int idx)
            {
                if (u_light_intensity[idx] > 0.0) {
                    float NdotL = clamp(dot(normal, i.light[idx]), 0.01, 1.0);
                    float NdotH = clamp(dot(normal, i.halfway[idx]), 0.0, 1.0);
                    float VdotH = clamp(dot(i.view, i.halfway[idx]), 0.0, 1.0);
                    float3 F = fresnelSchlick(specularEnvironmentR0, specularEnvironmentR90, VdotH);
                    float G = geometricOcclusionCookTorrance(NdotV, NdotH, VdotH, NdotL) * 2.0;
                    float D = microfacetDistribution(roughnessBiq, NdotH);
                    float3 diffuse_BRDF = (1.0 - F) * lambertianDiffuse(diffuseColor);
                        
                    float3 specular_BRDF = F * G * D / (4.0 * NdotL * NdotV);
                    return (diffuse_BRDF + specular_BRDF) * NdotL * u_light_intensity[idx];
                }
                return float3(0.0, 0.0, 0.0);
            }

			fixed4 frag(v2f i) : SV_Target
			{				
				float2 texcoord = float2(i.uv.x, 1.0 - i.uv.y);
				float metallic = u_metallic_roughness_factor[0];
				float roughness = u_metallic_roughness_factor[1];
                float occlusion = u_metallic_roughness_factor[2];
				if (u_sampler_usage[1] == 1.0)
				{
                    float3 metallicRoughnessColor = tex2D(metallicRoughnessSampler, texcoord).rgb;
                    occlusion = 1 + (metallicRoughnessColor.x - 1) * occlusion;
                    roughness = metallicRoughnessColor.y * roughness;
                    metallic = metallicRoughnessColor.z * metallic;
				}
				roughness = clamp(roughness, M_F0, 1.0);
				metallic = clamp(metallic, 0.0, 1.0);
				float roughnessSq = roughness * roughness;
				float roughnessBiq = roughnessSq * roughnessSq;
				float4 baseColor = u_basecolor_factor;
				if (u_sampler_usage[0] == 1.0)
				{
					float4 base_color = tex2D(baseColorSampler, texcoord);
					baseColor.rgb = baseColor.rgb * pow(base_color.rgb, M_GAMMA_3);
					baseColor.a = base_color.a;
				}
				float alpha = baseColor.a;
				if (u_blend_usage[2] == 1.0) {
					alpha = alpha * tex2D(alphaSampler, texcoord).r;
				}
				float3 diffuseColor = (baseColor.rgb * (1.0 - M_F0)) * (1.0 - metallic);
				float3 specularColor = mix(float3(M_F0, M_F0, M_F0), baseColor.rgb, metallic);
				float reflectance = max(max(specularColor.r, specularColor.g), specularColor.b);
				float reflectance90 = clamp(reflectance * 25.0, 0.0, 1.0);
				float3 specularEnvironmentR0 = specularColor.rgb;
				float3 specularEnvironmentR90 = float3(reflectance90, reflectance90, reflectance90);

#ifdef TBN_MATRIX
				float3x3 tbn = i.tbn;
#else
                float3x3 tbn = float3x3(normalize(i.tangent.xyz), normalize(i.bitangent.xyz), normalize(i.normal));
#endif
				float3 normal = tbn[2];
				if (u_sampler_usage[3] == 1.0) {
					float4 tex = tex2D(normalSampler, texcoord);
					tex.g = 1.0 - tex.g;
					float3 norm = normalize((tex.xyz - 0.5) * 2.0);
					normal = normalize(norm.x * tbn[0] + norm.y * tbn[1] + norm.z * tbn[2]);
				}
				normal = normalize(normal);

				float NdotV = abs(dot(normal, i.view)) + 0.01;
				float3 direct_light = float3(0.0, 0.0, 0.0);

                direct_light += getDirectLight(i, normal, specularEnvironmentR0, specularEnvironmentR90, NdotV, roughnessBiq, diffuseColor, 0);
                direct_light += getDirectLight(i, normal, specularEnvironmentR0, specularEnvironmentR90, NdotV, roughnessBiq, diffuseColor, 1);
                direct_light += getDirectLight(i, normal, specularEnvironmentR0, specularEnvironmentR90, NdotV, roughnessBiq, diffuseColor, 2);


                float3 reflection = -normalize(reflect(i.view, normal));

                float3 irradiance_IBL_factor = SRGBtoLINEAR(texCUBE(irradianceIBLSampler, normal).rgb);
                float3 brdfLUT = SRGBtoLINEAR(tex2D(brdfLUTSampler, float2(NdotV, 1.0 - roughness)).xyz);
                float3 specular_IBL_factor = SRGBtoLINEAR(texCUBElod(specularIBLSampler, float4(reflection, roughness * 8.0)).rgb);
				float3 diffuseIBL_BRDF = irradiance_IBL_factor * diffuseColor;
				float3 specularIBL_BRDF = specular_IBL_factor * (specularColor * brdfLUT.x + brdfLUT.y);

				float3 indirect_light = diffuseIBL_BRDF * u_light_intensity[IDX_LIGHT_IBL_IRRADIANCE] + specularIBL_BRDF * u_light_intensity[IDX_LIGHT_IBL_SPECULAR];
                indirect_light *= occlusion;

                float3 emissive = u_emissive_factor;

				if (u_sampler_usage[2] == 1.0) {
					emissive = emissive * pow(tex2D(emissiveSampler, texcoord).rgb, M_GAMMA_3);
				}
				float3 color = emissive + direct_light + indirect_light;

				color = pow(color, 1.0 / M_GAMMA_3);
				fixed4 colorResult = fixed4(color, alpha);
				return colorResult;
			}
			ENDCG
        }

    }
}
