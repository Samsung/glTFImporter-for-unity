Shader "Custom/Cartoon"{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo (RGB)", 2D) = "white" {}
		_BumpMap("Bumpmap", 2D) = "bump" {}
	}
	SubShader
	{
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }    //! 쉐이더 타입을 Transparent로 변경, Render Queue도 같이
		blend SrcAlpha OneMinusSrcAlpha    //! Blending 옵션 설정
		LOD 100
		//1Pass
		Cull Front
		CGPROGRAM
		#pragma surface surf Lambert vertex:vert

		struct Input
		{
			float _Blank;
		};
		void vert(inout appdata_full v)
		{
			v.vertex.xyz += v.normal.xyz * 0.003;
		}

		void surf(Input IN, inout SurfaceOutput o)
		{
			o.Albedo = 0;
		}

		ENDCG

		//2Pass
		Cull Back
		CGPROGRAM
		//#pragma Lambert keepalpha
		#pragma surface surf _CustomCell Lambert keepalpha

		sampler2D _MainTex;
		sampler2D _BumpMap;
		fixed4 _Color;
		struct Input
		{
			float2 uv_MainTex;
			float2 uv_BumpMap;
		};
		float3 invertY(float3 pos)
		{
			return float3(pos.x, 1.0 - pos.y, pos.z);
		}
		void surf(Input IN, inout SurfaceOutput o)
		{

			float3 tex = normalize(tex2D(_BumpMap, IN.uv_BumpMap));
			o.Normal = tex;

			fixed4 c = tex2D(_MainTex, IN.uv_MainTex.xyy) * _Color;

			o.Albedo = c.rgb;
			o.Alpha = c.a;
		}
		float4 Lighting_CustomCell(SurfaceOutput o, float3 lightDir, float atten)
		{
			float fNDotl = dot(o.Normal, lightDir) * 0.7 + 0.3;

			if (fNDotl > 0.5)
				fNDotl = 1;
			else if (fNDotl > 0.3)
				fNDotl = 0.3;
			else
				fNDotl = 0.1;

			float4 fResult;
			fResult.rgb = fNDotl * o.Albedo * _LightColor0.rgb * atten;
			fResult.a = o.Alpha;

			return fResult;
		}


		ENDCG
	}
		FallBack "Diffuse"
}

