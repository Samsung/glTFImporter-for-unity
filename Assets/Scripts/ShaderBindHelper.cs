using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShaderBindHelper : MonoBehaviour {

    Camera mainCamera;
    Material[] materials;
    Vector2 ViewSize = new Vector2(1440, 1080);


    public float FaceClipping;
    public Vector3 reflAxis = new Vector3(0.0f, 0.0f, 1.0f);
    [Range(-180.0f, 180.0f)]
    public float ReflAngle;

    [Range(-180.0f, 180.0f)]
    public float ReflAngleX;
    [Range(-180.0f, 180.0f)]
    public float ReflAngleY;
    [Range(-180.0f, 180.0f)]
    public float ReflAngleZ;

    public Vector2 TexCoordOffset = new Vector2(0, 0);

    public Vector4 GazeAxisAngleParam = new Vector4(1, 1, 1, 1);

    // light position
    public Vector4[] Lights = new Vector4[MAX_LIGHT_NUM] {
        new Vector4(0.0f, 0.0f, 0.0f, 1.0f),
        new Vector4(0.0f, 0.0f, 0.0f, 1.0f),
        new Vector4(0.0f, 0.0f, 0.0f, 1.0f)
    };
    // intensity of MAX_LIGHT_NUM light sources + IBL.irradiance + IBL.specular
    public float[] LightIntensity = new float[LIGHT_INTENSITY_NUM] { 1.0f, 1.0f, 1.0f, 0.95f, 0.1f };
    // 0 - directional, 1 - point light
    public float[] LightSourceType = new float[MAX_LIGHT_NUM] { 0.0f, 0.0f, 0.0f };
    // light color
    public Vector4[] LightColor = new Vector4[MAX_LIGHT_NUM] {
        new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
        new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
        new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
    };
    // x,y,z - multipliers of intensity decrease, w - range of point light
    /*public Vector4[] LightAttenuation = new Vector4[MAX_LIGHT_NUM] {
        new Vector4(LightSource.ATTENUATION.x, LightSource.ATTENUATION.y, LightSource.ATTENUATION.z, 1.0f),
        new Vector4(LightSource.ATTENUATION.x, LightSource.ATTENUATION.y, LightSource.ATTENUATION.z, 1.0f),
        new Vector4(LightSource.ATTENUATION.x, LightSource.ATTENUATION.y, LightSource.ATTENUATION.z, 1.0f)
    }; */
   public Vector4[] LightAttenuation = new Vector4[MAX_LIGHT_NUM] {
       new Vector4(25.0f, 0.0f, 1.0f, 1.0f),
       new Vector4(25.0f, 0.0f, 1.0f, 1.0f),
       new Vector4(25.0f, 0.0f, 1.0f, 1.0f)
   };

    Matrix4x4 normalMatrix;
    Matrix4x4 reflMatrix;
    //Vector3 z_axis;
    const int MAX_LIGHT_NUM = 3;
    const int LIGHT_INTENSITY_NUM = 5;

    //Bind for GUI
    /*public Vector4 BaseColorFactor = new Vector4(1, 1, 1, 1);
    public Vector2 MetallicRouphnessFactor = new Vector2(1.0f, 1.0f);
    public Vector3 EmissiveFactor = new Vector3(0, 0, 0);
    public Vector4 SamplerUsage = new Vector4(0, 0, 0, 0);

    public Texture Texture_BaseColor;
    public Texture Texture_MetallicRoughness;
    public Texture Texture_Normal;
    public Texture Texture_Emissive;


    //to do
    public Texture Texture_BrdfLUT;
    public Texture Texture_IBL_Irradiance;
    public Texture Texture_IBL_Specular;*/

    void Start()
    {
        materials = GetComponent<Renderer>().materials;
        mainCamera = GameObject.FindObjectOfType<Camera>();

        //z_axis = new Vector3(0, 0, 1);
        
        setUniforms();
    }

    // Update is called once per frame
    void Update()
    {

        findLights();
        setUniforms();
    }

    private void findLights()
    {
        Light[] sceneLights = GameObject.FindObjectsOfType<Light>();
        int i = 0;
        foreach (Light light in sceneLights)
        {
            if (light.type == LightType.Point)
            {
                Lights[i] = light.transform.position;
                LightIntensity[i] = light.intensity;
                i++;
            }
            if (i >= 3) break;
        }

    }

    private void setUniforms()
    {
        Quaternion rotation = Quaternion.Euler(ReflAngleX, ReflAngleY, ReflAngleZ);
        reflMatrix = Matrix4x4.Rotate(rotation);

        normalMatrix = GetComponent<Transform>().localToWorldMatrix.inverse.transpose;
        //print(normalMatrix);
        /*normalMatrix.m03 = 0.0f;
        normalMatrix.m13 = 0.0f;
        normalMatrix.m23 = 0.0f;
        normalMatrix.m30 = 0.0f;
        normalMatrix.m31 = 0.0f;
        normalMatrix.m32 = 0.0f;
        normalMatrix.m33 = 1.0f;*/

        //normalMatrix = GetComponent<Transform>().localToWorldMatrix.inverse.transpose;

        foreach (var material in materials)
        {
            material.SetVector(Shader.PropertyToID("u_texcoord_offset"), TexCoordOffset);
            material.SetVector(Shader.PropertyToID("u_viewSize"), ViewSize);
            material.SetFloat(Shader.PropertyToID("u_faceClipping"), FaceClipping);
            material.SetFloatArray(Shader.PropertyToID("u_light_intensity"), LightIntensity);
            material.SetVectorArray(Shader.PropertyToID("u_light_pos"), Lights);
            material.SetMatrix(Shader.PropertyToID("u_NormalMatrix"), normalMatrix);
            material.SetMatrix(Shader.PropertyToID("u_ReflMatrix"), reflMatrix);
            material.SetVector(Shader.PropertyToID("u_view_pos"), mainCamera.transform.position);

            material.SetVectorArray(Shader.PropertyToID("u_light_attenuation"), LightAttenuation);
            material.SetVectorArray(Shader.PropertyToID("u_light_color"), LightColor);
            material.SetFloatArray(Shader.PropertyToID("u_light_point"), LightSourceType);
        }

        //binded data
        /*material.SetVector(Shader.PropertyToID("u_basecolor_factor"), BaseColorFactor);
        material.SetVector(Shader.PropertyToID("u_metallic_roughness_factor"), MetallicRouphnessFactor);
        material.SetVector(Shader.PropertyToID("u_emissive_factor"), EmissiveFactor);
        material.SetVector(Shader.PropertyToID("u_primitive_usage"), new Vector4(0, 0, 0, 0));
        material.SetVector(Shader.PropertyToID("u_sampler_usage"), SamplerUsage);
        material.SetVector(Shader.PropertyToID("u_blend_usage"), new Vector4(0, 0, 0, 0));

        material.SetTexture(Shader.PropertyToID("baseColorSampler"), Texture_BaseColor);
        material.SetTexture(Shader.PropertyToID("metallicRoughnessSampler"), Texture_MetallicRoughness);
        material.SetTexture(Shader.PropertyToID("normalSampler"), Texture_Normal);
        material.SetTexture(Shader.PropertyToID("emissiveSampler"), Texture_Emissive);*/
    }
}
