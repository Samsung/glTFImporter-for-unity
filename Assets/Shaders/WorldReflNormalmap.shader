Shader "Custom/WorldReflNormalmap"
{
    Properties{
      _Color("Color", Color) = (1,1,1,1)
      _MainTex("Texture", 2D) = "white" {}
      _BumpMap("Bumpmap", 2D) = "bump" {}
      _Cube("Cubemap", CUBE) = "" {}
    }
        SubShader{
          Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
          blend SrcAlpha OneMinusSrcAlpha    //! Blending 옵션 설정
          LOD 100
          CGPROGRAM
          #pragma surface surf Lambert keepalpha
          struct Input {
              float2 uv_MainTex;
              float2 uv_BumpMap;
              float3 worldRefl;
              INTERNAL_DATA
          };
          sampler2D _MainTex;
          sampler2D _BumpMap;
          samplerCUBE _Cube;
          fixed4 _Color;
          void surf(Input IN, inout SurfaceOutput o) {

              fixed4 c = tex2D(_MainTex, IN.uv_MainTex.xyy) * _Color;
              o.Albedo = c.rgb * 0.5;
              o.Alpha = c.a;
              o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
              o.Emission = texCUBE(_Cube, WorldReflectionVector(IN, o.Normal)).rgb;
          }
          ENDCG
    }
        Fallback "Diffuse"
}