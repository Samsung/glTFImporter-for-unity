Shader "Custom/VisualSoftwarePBS" {
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
            GLSLPROGRAM
			#include "UnityCG.glslinc"

            #ifdef VERTEX
			

            #define MAX_LIGHT_NUM 3
            #define MAX_JOINT_NUM 70

			//#define TBN_MATRIX
			//#define NORMAL_VS

            precision highp float;		

			attribute highp vec3 a_position;
			//attribute highp vec3 a_morph_position_0;
			//attribute vec3 a_normal;
			//attribute vec3 a_morph_normal_0;
			//attribute vec4 a_tangent;
			//attribute vec2 a_texcoord;
			//attribute vec4 a_joint;
			//attribute vec4 a_weight;
			attribute vec4 Tangent;

			uniform vec4 u_primitive_usage;

			//uniform mat4 u_MVPMatrix;

			uniform mat4 u_NormalMatrix;
			uniform vec2 u_texcoord_offset;
			//uniform mat4 u_JointMatrix[MAX_JOINT_NUM];

			uniform vec3 u_light_pos[MAX_LIGHT_NUM];
			uniform vec3 u_view_pos;

			//uniform vec2 u_texcoord_offset;

			varying vec3 v_normal_refl;
			varying vec3 v_view;
			varying vec3 v_light[MAX_LIGHT_NUM];
			varying vec3 v_halfway[MAX_LIGHT_NUM];
			

#ifdef TBN_MATRIX
			varying mat3 v_tbn;
#else
			varying vec3 v_tangent;
			varying vec3 v_bitangent;
			varying vec3 v_normal;
#endif
			
			varying vec2 v_texcoord;

			void main() {

				vec3 transformed_position = gl_Vertex.xyz;
				vec3 transformed_normal = gl_Normal.xyz;

				/*if (u_primitive_usage.s == 1.0) {
					transformed_position = transformed_position + a_morph_position_0;
				}

				if (u_primitive_usage.t == 1.0) {
					transformed_normal = normalize(transformed_normal + a_morph_normal_0);
				}

				if (u_primitive_usage.p == 1.0) {
					mat4 skinMatrix = a_weight.x * u_JointMatrix[int(a_joint.x)]
						+ a_weight.y * u_JointMatrix[int(a_joint.y)]
						+ a_weight.z * u_JointMatrix[int(a_joint.z)]
						+ a_weight.w * u_JointMatrix[int(a_joint.w)];
					gl_Position = u_MVPMatrix * skinMatrix * vec4(transformed_position, 1.0);
					transformed_normal = mat3(skinMatrix) * transformed_normal;
				}
				else {*/
					gl_Position = gl_ModelViewProjectionMatrix * vec4(transformed_position, 1.0);
				//}

					v_texcoord = gl_MultiTexCoord0.xy + u_texcoord_offset;

				/*vec3 local_model_pos = vec3(gl_NormalMatrix * transformed_position);

				vec3 normal = normalize(gl_NormalMatrix * transformed_normal).xyz;   // N
				vec3 tangent = normalize(gl_NormalMatrix * Tangent.xyz).xyz;            // T*/

#ifdef NORMAL_VS
				vec3 local_model_pos = vec3(u_NormalMatrix * vec4(transformed_position, 1.0) );
				vec3 normal = normalize(u_NormalMatrix * vec4(transformed_normal, 1.0 )).xyz;   // N
				vec3 tangent = normalize(u_NormalMatrix * Tangent).xyz;            // T
#else
				vec3 local_model_pos = vec3(unity_ObjectToWorld * vec4(transformed_position, 1.0));
				vec3 normal = normalize(u_NormalMatrix * vec4(transformed_normal, 1.0)).xyz;   // N
				vec3 tangent = normalize(u_NormalMatrix * Tangent).xyz;            // T
#endif
				vec3 bitangent = normalize(cross(normal, tangent));                  // B

#ifdef TBN_MATRIX
				v_tbn = mat3(tangent, bitangent, normal);                            // TBN
#else
				v_tangent = tangent;
				v_bitangent = bitangent;
				v_normal = normal;
#endif

				v_light[0] = normalize(u_light_pos[0]);                  // L : directional light
				v_light[1] = normalize(u_light_pos[1]);
				v_light[2] = normalize(u_light_pos[2]);

				v_view = normalize(u_view_pos - local_model_pos);        // V
				v_halfway[0] = normalize(v_light[0] + v_view);           // H
				v_halfway[1] = normalize(v_light[1] + v_view);           // H
				v_halfway[2] = normalize(v_light[2] + v_view);           // H

				/*mat4 convNormalMatrix;
				convNormalMatrix[0] = vec4(gl_NormalMatrix[0], 0.0);
				convNormalMatrix[1] = vec4(gl_NormalMatrix[1], 0.0);
				convNormalMatrix[2] = vec4(gl_NormalMatrix[2], 0.0);
				convNormalMatrix[3] = vec4(0.0,0.0,0.0,1.0);
				mat4 refl_mat = u_ReflMatrix * convNormalMatrix;*/
				//mat4 refl_mat = u_ReflMatrix *u_NormalMatrix;
				//v_normal_refl = (refl_mat * vec4(transformed_normal, 1.0)).xyz;
				//v_normal_refl = ( vec4(normal.xyz, 1.0)).xyz;
			}


            #endif

            #ifdef FRAGMENT

            #define MAX_LIGHT_NUM 3
            #define MAX_LIGHT_INTENSITY_NUM 5
            #define IDX_LIGHT_IBL_IRRADIANCE 3
            #define IDX_LIGHT_IBL_SPECULAR 4

			//#define TBN_MATRIX

            precision mediump float;
			uniform lowp sampler2D baseColorSampler;
			uniform lowp sampler2D metallicRoughnessSampler;
			uniform lowp sampler2D emissiveSampler;
			uniform lowp sampler2D normalSampler;
			uniform lowp sampler2D alphaSampler;

			uniform lowp samplerCube irradianceIBLSampler;
			uniform lowp samplerCube specularIBLSampler;
			uniform lowp sampler2D brdfLUTSampler;

			uniform vec4 u_sampler_usage;
			uniform vec4 u_blend_usage;

			//uniform vec2 u_viewSize;

			uniform float u_light_intensity[MAX_LIGHT_INTENSITY_NUM];

			uniform vec4 u_basecolor_factor;
			uniform vec3 u_emissive_factor;
			uniform vec2 u_metallic_roughness_factor;
			uniform mat4 u_ReflMatrix;

			//varying vec3 v_normal_refl;
			varying vec3 v_view;
			varying vec3 v_light[MAX_LIGHT_NUM];
			varying vec3 v_halfway[MAX_LIGHT_NUM];

#ifdef TBN_MATRIX
			varying mat3 v_tbn;
#else
			varying vec3 v_tangent;
			varying vec3 v_bitangent;
			varying vec3 v_normal;
#endif

			varying vec2 v_texcoord;

			const float M_PI = 3.141592653589793;
			const float M_F0 = 0.04;
			const float M_GAMMA = 2.2;

			vec3 lambertianDiffuse(vec3 baseColor)
			{
				return baseColor / M_PI;
			}

			vec3 fresnelSchlick(vec3 reflectance0, vec3 reflectance90, float VdotH)
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
			void main()
			{
				vec2 texcoord = vec2(v_texcoord.s, 1.0 - v_texcoord.t);
				float metallic = u_metallic_roughness_factor.r;
				float roughness = u_metallic_roughness_factor.g;
				if (u_sampler_usage.t == 1.0)
				{
					vec2 metallicRoughnessColor = texture(metallicRoughnessSampler, texcoord).gb;
					roughness = metallicRoughnessColor.x * roughness;
					metallic = metallicRoughnessColor.y * metallic;
				}
				roughness = clamp(roughness, M_F0, 1.0);
				metallic = clamp(metallic, 0.0, 1.0);
				float roughnessSq = roughness * roughness;
				float roughnessBiq = roughnessSq * roughnessSq;
				vec4 baseColor = u_basecolor_factor;
				if (u_sampler_usage.s == 1.0)
				{
					vec4 base_color = texture(baseColorSampler, texcoord);
					baseColor.rgb = baseColor.rgb * pow(base_color.rgb, vec3(M_GAMMA));
					baseColor.a = base_color.a;
				}
				float alpha = baseColor.a;
				if (u_blend_usage.s == 1.0) {
					alpha = alpha * texture(alphaSampler, texcoord).r;
				}
				/*if (u_faceClipping == 1.0 && v_clipplane < 1.0) {
					vec2 facePos = vec2(gl_FragCoord.x / u_viewSize.x, gl_FragCoord.y / u_viewSize.y);
					float faceClippingFactor = texture(faceSampler, facePos).r;
					if (u_faceClipping == 0.0 || faceClippingFactor == 1.0) {
						discard;
					}
					else if (faceClippingFactor > 0.0) {
						alpha *= 1.0 - faceClippingFactor;
					}
				} if (alpha < u_blend_usage.p) {
					discard;
				}*/
				vec3 diffuseColor = (baseColor.rgb * (1.0 - M_F0)) * (1.0 - metallic);
				vec3 specularColor = mix(vec3(M_F0), baseColor.rgb, metallic);
				float reflectance = max(max(specularColor.r, specularColor.g), specularColor.b);
				float reflectance90 = clamp(reflectance * 25.0, 0.0, 1.0);
				vec3 specularEnvironmentR0 = specularColor.rgb;
				vec3 specularEnvironmentR90 = vec3(reflectance90);

#ifdef TBN_MATRIX
				mat3 tbn = v_tbn;
#else
				mat3 tbn = mat3(v_tangent, v_bitangent, v_normal);
#endif
				

				vec3 normal = tbn[2];
				if (u_sampler_usage.q == 1.0) {
					normal = normalize(tbn * (texture(normalSampler, texcoord).xyz * 2.0 - 1.0)) * vec3(1.5, 1.5, 1.0);
				}
				normal = normalize(normal);

				float NdotV = abs(dot(normal, v_view)) + 0.01;
				vec3 direct_light = vec3(0.0);
				for (int idx = 0; idx < MAX_LIGHT_NUM; idx++) {
					if (u_light_intensity[idx] != 0.0) {
						float NdotL = clamp(dot(normal, v_light[idx]), 0.01, 1.0);
						float NdotH = clamp(dot(normal, v_halfway[idx]), 0.0, 1.0);
						float VdotH = clamp(dot(v_view, v_halfway[idx]), 0.0, 1.0);
						vec3 F = fresnelSchlick(specularEnvironmentR0, specularEnvironmentR90, VdotH);
						float G = geometricOcclusionCookTorrance(NdotV, NdotH, VdotH, NdotL) * 2.0;
						float D = microfacetDistribution(roughnessBiq, NdotH);
						vec3 diffuse_BRDF = (1.0 - F) * lambertianDiffuse(diffuseColor);
						vec3 specular_BRDF = F * G * D / (4.0 * NdotL * NdotV);
						direct_light = direct_light + NdotL * (diffuse_BRDF + specular_BRDF) * u_light_intensity[idx];
					}
				}

				


				vec3 refl_normal = (u_ReflMatrix * vec4(normal, 1.0)).xyz;
				vec3 reflection = normalize(reflect(-v_view, refl_normal));
				vec3 irradiance_IBL_factor = texture(irradianceIBLSampler, refl_normal).rgb;

				vec3 brdfLUTtest = texture(brdfLUTSampler, vec2(NdotV, 1.0 - roughness)).xyz;
				vec2 brdfLUT = pow(brdfLUTtest.xyz, vec3(M_GAMMA)).rg;
				//brdfLUT = pow(brdfLUT.xy, vec2(M_GAMMA));//
				vec3 specular_IBL_factor = textureLod(specularIBLSampler, reflection, roughness * 8.0).rgb * (1.0 - roughness * 0.9);
				specular_IBL_factor = pow(specular_IBL_factor.xyz, vec3(M_GAMMA)).rgb;
				vec3 diffuseIBL_BRDF = irradiance_IBL_factor * diffuseColor;
				vec3 specularIBL_BRDF = specular_IBL_factor * (specularColor * brdfLUT.x + brdfLUT.y );

				vec3 indirect_light = diffuseIBL_BRDF * u_light_intensity[IDX_LIGHT_IBL_IRRADIANCE] + specularIBL_BRDF * u_light_intensity[IDX_LIGHT_IBL_SPECULAR];
				vec3 emissive = u_emissive_factor;

				if (u_sampler_usage.p == 1.0) {
					emissive = emissive * pow(texture(emissiveSampler, texcoord).rgb, vec3(M_GAMMA));
				}
				vec3 color = emissive + direct_light + indirect_light;

				color = pow(color, vec3(1.0 / M_GAMMA));

				/*if (u_blend_usage.t == 1.0) {
					vec2 preview_texcoord = vec2(gl_FragCoord.x / u_viewSize.x, gl_FragCoord.y / u_viewSize.y);
					vec3 preview_color = texture(texSampler, preview_texcoord).rgb;
					gl_FragColor = vec4(mix(color, preview_color, 1.0 - alpha), 1.0);
				}
				else {*/
				//gl_FragColor = vec4(0.0,0.0,color.b, alpha);
				gl_FragColor = vec4(color, alpha);
				//}
			}


            #endif

            ENDGLSL
        }

    }
}
/*


#define MAX_LIGHT_NUM 3 

#define MAX_LIGHT_INTENSITY_NUM 5 

#define IDX_LIGHT_IBL_IRRADIANCE 3 

#define IDX_LIGHT_IBL_SPECULAR 4 
precision mediump float;

uniform lowp sampler2D baseColorSampler;
uniform lowp sampler2D metallicRoughnessSampler;
uniform lowp sampler2D emissiveSampler;
uniform lowp sampler2D normalSampler;
uniform lowp sampler2D alphaSampler;
uniform lowp sampler2D faceSampler;
uniform lowp sampler2D texSampler;
uniform lowp samplerCube irradianceIBLSampler;
uniform lowp samplerCube specularIBLSampler;
uniform lowp sampler2D brdfLUTSampler;
uniform vec4 u_sampler_usage;
uniform vec4 u_blend_usage;
uniform vec2 u_viewSize;
uniform float u_faceClipping;
varying float v_clipplane;
uniform float u_light_intensity[MAX_LIGHT_INTENSITY_NUM];
uniform vec4 u_basecolor_factor;
uniform vec3 u_emissive_factor;
uniform vec2 u_metallic_roughness_factor;
varying vec3 v_normal_refl;
varying vec3 v_view;
varying vec3 v_light[MAX_LIGHT_NUM];
varying vec3 v_halfway[MAX_LIGHT_NUM];
varying mat3 v_tbn;
varying vec2 v_texcoord;
const float M_PI = 3.141592653589793;
const float M_F0 = 0.04;
const float M_GAMMA = 2.2;

vec3 lambertianDiffuse(vec3 baseColor)
{
	return baseColor / M_PI;
}

vec3 fresnelSchlick(vec3 reflectance0, vec3 reflectance90, float VdotH)
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
void main()
{
	float metallic = u_metallic_roughness_factor.r;
	float roughness = u_metallic_roughness_factor.g;
	if (u_sampler_usage.t == 1.0)
	{
		vec2 metallicRoughnessColor = texture2D(metallicRoughnessSampler, v_texcoord).gb;
		roughness = metallicRoughnessColor.x * roughness;
		metallic = metallicRoughnessColor.y * metallic;
	} 
	roughness = clamp(roughness, M_F0, 1.0);
	metallic = clamp(metallic, 0.0, 1.0);
	float roughnessSq = roughness * roughness;
	float roughnessBiq = roughnessSq * roughnessSq;
	vec4 baseColor = u_basecolor_factor;
	if (u_sampler_usage.s == 1.0)
	{
		vec4 base_color = texture2D(baseColorSampler, v_texcoord);
		baseColor.rgb = baseColor.rgb * pow(base_color.rgb, vec3(M_GAMMA));
		baseColor.a = base_color.a;
	}
	float alpha = baseColor.a;
	if (u_blend_usage.s == 1.0) {
		alpha = alpha * texture2D(alphaSampler, v_texcoord).r;
	} 
	if (u_faceClipping == 1.0 && v_clipplane < 1.0) {
		vec2 facePos = vec2(gl_FragCoord.x / u_viewSize.x, gl_FragCoord.y / u_viewSize.y);
		float faceClippingFactor = texture2D(faceSampler, facePos).r;
		if (u_faceClipping == 0.0 || faceClippingFactor == 1.0) {
			discard;
		}
		else if (faceClippingFactor > 0.0) {
			alpha *= 1.0 - faceClippingFactor;
		}
	} if (alpha < u_blend_usage.p) {
		discard;
	} 
	vec3 diffuseColor = (baseColor.rgb * (1.0 - M_F0)) * (1.0 - metallic);
	vec3 specularColor = mix(vec3(M_F0), baseColor.rgb, metallic);
	float reflectance = max(max(specularColor.r, specularColor.g), specularColor.b);
	float reflectance90 = clamp(reflectance * 25.0, 0.0, 1.0);
	vec3 specularEnvironmentR0 = specularColor.rgb;
	vec3 specularEnvironmentR90 = vec3(reflectance90);
	vec3 normal = v_tbn[2];
	if (u_sampler_usage.q == 1.0) {
		normal = normalize(v_tbn * (texture2D(normalSampler, v_texcoord).xyz * 2.0 - 1.0)) * vec3(1.5, 1.5, 1.0);
	} 
	normal = normalize(normal);
	float NdotV = abs(dot(normal, v_view)) + 0.01;
	vec3 direct_light = vec3(0.0);
	for (int idx = 0;idx < MAX_LIGHT_NUM;idx++) {
		if (u_light_intensity[idx] != 0.0) {
			float NdotL = clamp(dot(normal, v_light[idx]), 0.01, 1.0);
			float NdotH = clamp(dot(normal, v_halfway[idx]), 0.0, 1.0);
			float VdotH = clamp(dot(v_view, v_halfway[idx]), 0.0, 1.0);
			vec3 F = fresnelSchlick(specularEnvironmentR0, specularEnvironmentR90, VdotH);
			float G = geometricOcclusionCookTorrance(NdotV, NdotH, VdotH, NdotL) * 2.0;
			float D = microfacetDistribution(roughnessBiq, NdotH);
			vec3 diffuse_BRDF = (1.0 - F) * lambertianDiffuse(diffuseColor);
			vec3 specular_BRDF = F * G * D / (4.0 * NdotL * NdotV);
			direct_light = direct_light + NdotL * (diffuse_BRDF + specular_BRDF) * u_light_intensity[idx];
		}
	} 
	vec3 reflection = normalize(reflect(-v_view, v_normal_refl));

	vec3 irradiance_IBL_factor = textureCube(irradianceIBLSampler, v_normal_refl).rgb;

	vec2 brdfLUT = texture2D(brdfLUTSampler, vec2(NdotV, 1.0 - roughness)).rg;
	vec3 specular_IBL_factor = textureCube(specularIBLSampler, reflection, roughness * 8.0).rgb * (1.0 - roughness * 0.9);

	vec3 diffuseIBL_BRDF = irradiance_IBL_factor * diffuseColor;
	vec3 specularIBL_BRDF = specular_IBL_factor * (specularColor * brdfLUT.x + brdfLUT.y);

	vec3 indirect_light = diffuseIBL_BRDF * u_light_intensity[IDX_LIGHT_IBL_IRRADIANCE] + specularIBL_BRDF * u_light_intensity[IDX_LIGHT_IBL_SPECULAR];
	vec3 emissive = u_emissive_factor;

	if (u_sampler_usage.p == 1.0) {
		emissive = emissive * pow(texture2D(emissiveSampler, v_texcoord).rgb, vec3(M_GAMMA));
	} 
	vec3 color = emissive + direct_light + indirect_light;
	color = pow(color, vec3(1.0 / M_GAMMA));

	if (u_blend_usage.t == 1.0) {
		vec2 preview_texcoord = vec2(gl_FragCoord.x / u_viewSize.x, gl_FragCoord.y / u_viewSize.y);
		vec3 preview_color = texture2D(texSampler, preview_texcoord).rgb;
		gl_FragColor = vec4(mix(color, preview_color, 1.0 - alpha), 1.0);
	}
	else {
		gl_FragColor = vec4(color, alpha);
	}
}*/