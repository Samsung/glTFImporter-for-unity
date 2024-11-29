Shader "Custom/AssetPBR" {
	Properties{
		baseColorSampler("BaseColorTexture", 2D) = "black" {}
		metallicRoughnessSampler("MetallicRoughnessTexture", 2D) = "black" {}
		emissiveSampler("EmissiveTexture", 2D) = "black" {}
		normalSampler("NormalTexture", 2D) = "black" {}
        irradianceIBLSampler("IrradianceBRDF", Cube) = "black" {}
        specularIBLSampler("SpecularBRDF", Cube) = "black" {}
        brdfLUTSampler("brdfLUT", 2D) = "black" {}

		u_basecolor_factor("BaseColorFactor", Vector) = (1, 1, 1, 1)
		u_emissive_factor("EmissiveFactor", Vector) = (0, 0, 0, 0)
		u_metallic_roughness_factor("MetallicRoughnessFactor", Vector) = (0, 0, 0, 0)
		u_alpha_cutoff("AlphaCutoff", Float) = 0.1

		u_sampler_usage("SamplerUsage",Vector) = (0,0,0,0) // TODO change set from 4 to 9 (write set because there is none)
		u_blend_usage("BlendUsage",Vector) = (0,0,0,0)
		u_primitive_usage("PrimitiveUsage",Vector) = (0,0,1,0)
        //u_light_color("LightColor",Vector) = (1,1,1,1) // default works for single light source but generates warnings in case of more
	}

		SubShader{
		Tags { 
            "LightMode" = "ForwardBase" 
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

		Pass{
		Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
		CGPROGRAM

        // #pragma debug
		#pragma target 3.5
		#pragma vertex vert
		#pragma fragment frag
		#include "UnityCG.cginc"
        #include "Lighting.cginc"
        
        #pragma multi_compile_fwdbase
        #include "AutoLight.cginc"

		#define MAX_JOINT_NUM 70
		#define MAX_LIGHT_NUM 3
		#define MAX_LIGHT_INTENSITY_NUM 5
		#define IDX_LIGHT_IBL_IRRADIANCE 3
		#define IDX_LIGHT_IBL_SPECULAR 4

        #define IDX_MORPH_TARGET_POSITION    x
        #define IDX_MORPH_TARGET_NORMAL      y
        #define IDX_NORMAL_MAP_VERTEX        z
        #define IDX_SKINNING                 w

		#define IDX_BASE_COLOR               x  //0
		#define IDX_NORMAL_MAP_FRAGMENT      w  //2
		// TODO IDX_METAL_ROUGHNESS and IDX_METAL_ROUGHNESS united as OMR_ID in our code
		#define IDX_METAL_ROUGHNESS          y  //3
		// IDX_OCCLUSION_MAP not supported always off as per UI v0.95
		// TODO implement following logic
	    // only active of IDX_METAL_ROUGHNESS is >=1 so use values 1, 2, 3 to switch between
		// 1 - zero occlusion, 2 - from separate texture 3 - from same ORM texture as metalRoughness
		#define IDX_OCCLUSION_MAP            y  //4
		#define IDX_EMISSIVE_MAP             z  //5

		uniform sampler2D baseColorSampler;
		uniform sampler2D metallicRoughnessSampler;
		uniform sampler2D emissiveSampler;
		uniform sampler2D normalSampler;
		uniform sampler2D alphaSampler;
        uniform sampler2D occlusionSampler; // added TODO remove because UIv0.95 doesn't support separate occlusion texture

		uniform samplerCUBE irradianceIBLSampler;
		uniform samplerCUBE specularIBLSampler;
		uniform sampler2D brdfLUTSampler;
        
        float4 baseColorSampler_ST; // apparently this is needed for TRANSFORM_TEX macro
        
		uniform float4 u_sampler_usage;
		uniform float4 u_blend_usage;		// legacy, has no effect

		uniform float u_light_point[MAX_LIGHT_NUM];
		uniform float3 u_light_pos[MAX_LIGHT_NUM];
		uniform float u_light_intensity[MAX_LIGHT_INTENSITY_NUM];
		uniform float4 u_light_attenuation[MAX_LIGHT_NUM]; // added
		uniform float3 u_light_color[MAX_LIGHT_NUM]; // added

		uniform float u_alpha_cutoff; // added
		uniform float4 u_basecolor_factor;
		uniform float4 u_emissive_factor;
		uniform float4 u_metallic_roughness_factor; // supposed to be u_occlusion_roughness_metallic_factor
		uniform float4x4 u_ReflMatrix;

		uniform float4 u_primitive_usage;

		uniform float4x4 u_NormalMatrix;
		uniform float2 u_texcoord_offset;
		uniform float3 u_view_pos;

		static const float M_PI = 3.141592653589793;
		static const float M_F0 = 0.04;
		static const float3 M_F03 = { M_F0, M_F0, M_F0 };
		static const float M_GAMMA = 2.2;
		static const float3 M_GAMMA_3 = { M_GAMMA, M_GAMMA, M_GAMMA };


		float3 invertY(float3 pos)
		{
			return float3(pos.x, 1.0 - pos.y, pos.z);
		}

		struct v2f {
			float4 pos : SV_POSITION;
			float3 v_model_pos : POSITION1;
			float2 v_texcoord : TEXCOORD0;
			float3 v_normal_refl : NORMAL1;
			float3 tbn_tangent : TANGENT;
			float3 tbn_bitangent : BITANGENT;
			float3 tbn_normal : NORMAL;
            SHADOW_COORDS(6)
		};

	v2f vert(appdata_tan input)
	{
		v2f output;
		// initialize
		output.pos   = float4(0, 0, 0, 0);
		output.v_model_pos   = float3(0, 0, 0);
		output.v_texcoord    = float2(0, 0);
		output.v_normal_refl = float3(0, 0, 0);
		output.tbn_tangent   = float3(0, 0, 0);
		output.tbn_bitangent = float3(0, 0, 0);
		output.tbn_normal    = float3(0, 0, 0);

		float3 transformed_position = input.vertex.xyz;
		float3 transformed_normal = normalize(input.normal);

		// Morph Target (off by default)
		if (1 == u_primitive_usage.IDX_MORPH_TARGET_POSITION) {
			float3 a_morph_position_0 = 0; // TODO if we ever wan't to use morping we need to send this value
			transformed_position = transformed_position + a_morph_position_0;
		}
		// off by default
		if (1 == u_primitive_usage.IDX_MORPH_TARGET_NORMAL) {
			float3 a_morph_normal_0 = 1; // TODO if we ever wan't to use morping we need to send this value
			//transformed_normal = normalize(a_normal + a_morph_normal_0);  // pre-computed morph-target normal
			transformed_normal = normalize(a_morph_normal_0);               // generate morph-target normal
		}

		float4x4 normal_matrix = u_NormalMatrix;
		float4x4 modelMatrix = unity_ObjectToWorld;

		// Skinning (off by default)
		if (1 == u_primitive_usage.IDX_SKINNING) {
			float4 a_joint = 0;  // TODO if we ever wan't to use skinning we need to send this value
			float4 a_weight = 0; // TODO if we ever wan't to use skinning we need to send this value
			float4x4 u_JointMatrix[MAX_JOINT_NUM];
			u_JointMatrix [0] = float4x4(
				0, 0, 0, 0,
				0, 0, 0, 0,
				0, 0, 0, 0,
				0, 0, 0, 0); // TODO if we ever wan't to use skinning we need to send this value
			float4x4 skinMatrix = a_weight.x * u_JointMatrix[int(a_joint.x)]
				+ a_weight.y * u_JointMatrix[int(a_joint.y)]
				+ a_weight.z * u_JointMatrix[int(a_joint.z)]
				+ a_weight.w * u_JointMatrix[int(a_joint.w)];
			output.pos = UnityObjectToClipPos(mul(skinMatrix, transformed_position));
			normal_matrix = mul(u_NormalMatrix, skinMatrix);
			modelMatrix = mul(unity_ObjectToWorld, skinMatrix);
		}
		else {
			output.pos = UnityObjectToClipPos(transformed_position);
		}
		float4 model_pos = mul(modelMatrix, float4(transformed_position, 1.0));
		output.v_model_pos = model_pos.xyz / model_pos.w;

		// Eye - EYE_PLANE
		output.v_texcoord = TRANSFORM_TEX(input.texcoord + u_texcoord_offset, baseColorSampler);

		// Normal and Tangent Space
		//normal_matrix = transpose(inverse(normal_matrix));
		float3 normal = normalize(mul(modelMatrix, float4(transformed_normal, 0.0))).xyz;   // N
		output.tbn_normal = normal;
		// on by default
		if (1 == u_primitive_usage.IDX_NORMAL_MAP_VERTEX) {
			float3 tangent = normalize(mul(modelMatrix, float4(input.tangent.xyz, 0.0))).xyz;   // T
            //tangent = normalize(tangent - dot(tangent, normal) * normal);
			float3 bitangent = cross(normal, tangent) * input.tangent.w;                    // B
			output.tbn_tangent = tangent;
			output.tbn_bitangent = bitangent;
		}

		// Environment Reflection
		output.v_normal_refl = normalize(mul(mul(u_ReflMatrix, modelMatrix), float4(transformed_normal, 0.0))).xyz;

		// TODO if anything can switch inverting off
		// invert Y now instead of doing it in Fragment
		output.v_texcoord = invertY(output.v_texcoord.xyy);
		//output.v_normal_refl = invertY(output.v_normal_refl);
		output.v_model_pos = invertY(output.v_model_pos);

        //TRANSFER_SHADOW(output);

		return output;
	}

	// Lambertian Diffuse
	float3 lambertianDiffuse(float3 baseColor)
	{
		return baseColor / M_PI;
	}
	// Fresnel Equation(F)
	float3 fresnelSchlick(float3 reflectance0, float3 reflectance90, float VdotH)
	{
		return reflectance0 + (reflectance90 - reflectance0) * pow(clamp(1.0 - VdotH, 0.0, 1.0), 5.0);
	}
	// Geometry Function(G)
	float geometricOcclusionCookTorrance(float NdotV, float NdotH, float VdotH, float NdotL)
	{
		return min(min(2.0 * NdotV * NdotH / VdotH, 2.0 * NdotL * NdotH / VdotH), 1.0);
	}
	// Normal Distribution Function(D)
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
	
	// V - vertex position
	// N - Normals
	float3 getDirectLight(float3 V, float3 N, float3 v_model_pos, float3 specularEnvironmentR0, float3 specularEnvironmentR90, float NdotV, float roughnessBiq, float3 diffuseColor, int idx)
	{
		if (u_light_intensity[idx] != 0.0) {

			float3 L = float3(0.0, 0.0, 0.0); // light pos
			float3 H = float3(0.0, 0.0, 0.0); // halfway

			float NdotL = 0.0;

			float intensity = 1.0;

			//if (u_light_intensity[idx] < 20.0) {
			if (u_light_point[idx] == 0)
			{
				// Directional Light
				L = normalize(u_light_pos[idx]);
				H = normalize(L + V);

				NdotL = dot(N, L);
			}
			else {
				// Point Light
				L = (u_light_pos[idx] - v_model_pos);
				float distance = length(L) / u_light_attenuation[idx].w;
				L = normalize(L);
				H = normalize(L + V);

				NdotL = dot(N, L);
				intensity = (1.0 / dot(u_light_attenuation[idx].xyz, float3(1.0, distance, distance * distance)));
			}

            
			if (NdotL > 0.0) {
				NdotL = clamp(NdotL, 0.01, 1.0);

				float NdotH = clamp(dot(N, H), 0.0, 1.0);
				float VdotH = clamp(dot(V, H), 0.0, 1.0);

				float3 F = fresnelSchlick(specularEnvironmentR0, specularEnvironmentR90, VdotH); // Fresnel equation
				float G = geometricOcclusionCookTorrance(NdotV, NdotH, VdotH, NdotL);          // Geometry function
				float D = microfacetDistribution(roughnessBiq, NdotH);                         // Normal distribution function
				float3 diffuse_BRDF = (1.0 - F) * lambertianDiffuse(diffuseColor);  // Diffuse BRDF : Lambert
				float3 specular_BRDF = (F * G * D / (4.0 * NdotL * NdotV)) / 10;           // Specular BRDF : Cook-Torrance
				return (intensity * NdotL) * (diffuse_BRDF + specular_BRDF) * u_light_color[idx] * u_light_intensity[idx];   // intensity : light color * light scale;
			}
		}
		return float3(0.0, 0.0, 0.0);
	}


	float4 frag(v2f input) : SV_Target
	{
		float2 texcoord = input.v_texcoord;// TODO consider float2(input.v_texcoord.x, 1.0 - input.v_texcoord.y);
		// Material - Base Color
		float4 baseColor = u_basecolor_factor;
		if (1 == u_sampler_usage.IDX_BASE_COLOR) {
			float4 base_color = tex2D(baseColorSampler, texcoord);
			baseColor.rgb = baseColor.rgb * pow(base_color.rgb, float3(M_GAMMA_3));
			baseColor.a *= base_color.a;
		}


		float alpha = baseColor.a;
		// Alpha Test
		if (alpha <= u_alpha_cutoff) {
			discard;
			return baseColor;
		}

		// Material - PBR Factor
		float metallic = u_metallic_roughness_factor.x;
		float roughness = u_metallic_roughness_factor.y;
		float occlusion_intensity = u_metallic_roughness_factor.z; // TODO occlusion factor is absent in UI v0.95
		float occlusion_color = 1.0;

		if (u_sampler_usage.IDX_METAL_ROUGHNESS >= 1.0) {
			float3 occlusionRoughnessMetallicColor = tex2D(metallicRoughnessSampler, texcoord).rgb;
			// MR stopped being multipliers because of phone behaviour AVATAR-684
			roughness = occlusionRoughnessMetallicColor.g;// *roughness;
			metallic = occlusionRoughnessMetallicColor.b;// *metallic;
			// becase IDX_OCCLUSION_MAP shares values with IDX_METAL_ROUGHNESS 1 => off, 2 => separate sampler 3 => omr texture (3 channels)
//			switch (u_sampler_usage.IDX_OCCLUSION_MAP) {
//			case 1:
//				occlusion_color = 1.0;
//				occlusion_intensity = 0.0;
//				break;
//			case 2:
//				occlusion_color = tex2D(occlusionSampler, texcoord).r;
//				break;
//			case 3:
				occlusion_color = occlusionRoughnessMetallicColor.r;
//				break;
//			}
		}

		// min Roughness : 0.04
		roughness = clamp(roughness, M_F0, 1.0) * 2.0;
		metallic = clamp(metallic, 0.0, 1.0);
//		return fixed4(metallic, metallic, metallic, 1);

		float roughnessSq = roughness * roughness;
		float roughnessBiq = roughnessSq * roughnessSq;

		float3 diffuseColor = (baseColor.rgb * (1.0 - M_F0)) * (1.0 - metallic);
		float3 specularColor = mix(float3(M_F03), baseColor.rgb, metallic);

		float reflectance = max(max(specularColor.r, specularColor.g), specularColor.b);
		// fresnel GrazingReflectance
		float reflectance90 = clamp(reflectance * 25.0, 0.0, 1.0);
		float3 specularEnvironmentR0 = specularColor.rgb;
		float3 specularEnvironmentR90 = float3(reflectance90, reflectance90, reflectance90);

		// Normal and Tangent Space
		float3 N = input.tbn_normal;
		if (1 == u_sampler_usage.IDX_NORMAL_MAP_FRAGMENT) {
			// TODO try tbn without normalize()
			// TODO try invering tex2D(normalSampler, texcoord).g = 1.0 - tex2D(normalSampler, texcoord);
			float3 tex = (tex2D(normalSampler, texcoord).xyz);
            tex = (tex - 0.5) * 2.0;
            N = normalize(tex.x * input.tbn_tangent - tex.y * input.tbn_bitangent + tex.z * input.tbn_normal);
		}
		N = normalize(N);

		float3 V = normalize(u_view_pos - input.v_model_pos);

		float NdotV = clamp(abs(dot(N, V)), 0.01, 1.0);

		// Direct Light : per-light
		float3 direct_light = float3(0.0, 0.0, 0.0);
		for (int idx = 0; idx < MAX_LIGHT_NUM; idx++) {
            float shadow = SHADOW_ATTENUATION(input);
            direct_light += shadow * getDirectLight(V, N, input.v_model_pos,
                specularEnvironmentR0, specularEnvironmentR90, NdotV,
                roughnessBiq, diffuseColor, idx) * 4;
		}

		// Indirect Light : IBL
		float3 reflection = normalize(reflect(-V, input.v_normal_refl));
		// TODO try inverse v_normal_refl.y = 1-v_normal_refl.y
		// TODO try tex2D
		// TODO try SRGBtoLINEAR
		float3 irradiance_IBL_factor = SRGBtoLINEAR(texCUBE(irradianceIBLSampler, input.v_normal_refl).rgb);               // Diffuse Irradiance Map(pre-computed)

		float2 brdfLUT = SRGBtoLINEAR(tex2D(brdfLUTSampler, float2(NdotV, 1.0 - roughness)).rgb).rg;                     // BRDF LUT(BRDF integration map)
		float3 specular_IBL_factor = SRGBtoLINEAR(texCUBElod(specularIBLSampler, float4(reflection, roughness * 8.0)).rgb);  // Specular pre-filter map
																									 // MipMap Level = 8 : resolution of 256x256

		float3 diffuseIBL_BRDF = irradiance_IBL_factor * diffuseColor;
		float3 specularIBL_BRDF = specular_IBL_factor * (specularColor * brdfLUT.x + brdfLUT.y);

        float3 indirect_light = diffuseIBL_BRDF * u_light_intensity[IDX_LIGHT_IBL_IRRADIANCE]
            + specularIBL_BRDF * u_light_intensity[IDX_LIGHT_IBL_SPECULAR];   // intensity : ibl scale;


		float occlusion = 1.0 + (occlusion_color - 1.0) * occlusion_intensity;
		indirect_light = indirect_light * occlusion;
//		return fixed4(indirect_light, 1);
			
		// Ambient Occlusion
		float3 color = direct_light + indirect_light;
//		color = lerp(color, color * occlusion_color, occlusion_intensity);

		// Emissive Light
		float3 emissive = u_emissive_factor;
		if (1 == u_sampler_usage.IDX_EMISSIVE_MAP) {
			emissive = emissive * pow(tex2D(emissiveSampler, texcoord).rgb, float3(M_GAMMA_3));
		}

		// Total Light
		color = color + emissive;

		// Gamma Correct
		color = pow(color, float3(1.0 / M_GAMMA, 1.0 / M_GAMMA, 1.0 / M_GAMMA));
		float4 colorResult = fixed4(color, alpha);

//        return fixed4(SRGBtoLINEAR(texCUBElod(specularIBLSampler, float4(reflection, roughness * 8.0)).rgb) * 0.2, 1);
		return colorResult;
	}
		ENDCG
	} // pass

	} // subshader
    FallBack "Diffuse"
} // shader

