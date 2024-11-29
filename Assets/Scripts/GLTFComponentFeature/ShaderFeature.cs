using System;
using UnityEngine;

namespace GLTFComponentFeature
{
    [Serializable]
    public class ShaderFeature
    {
        public TextureFormat TextureCompressionType;
        public bool UseTextureQuarterResolution = false;

        public bool UseCustomShader = false;
#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
        [DrawIf("UseCustomShader", true, DrawIfAttribute.DisablingType.ReadOnly)]
        [NamedArrayAttribute(new string[] { "BaseColorTexture", "MetallicRoughnessTexture", "NormalTexture", "BaseColorFactor", "MetallicRoughnessFactor", "IBLTexture" })]
#endif
        public string[] CustomShaderPropertyNames = new string[6];
        // public List<string> CustomShaderPropertyNames = new List<string>();
#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
        [DrawIf("UseCustomShader", true, DrawIfAttribute.DisablingType.DontDraw)]
#endif
        public Shader CustomShader;
#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
        [DrawIf("UseCustomShader", true, DrawIfAttribute.DisablingType.DontDraw)]
#endif
        public bool UseTextureReverse = true;
#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
        [DrawIf("UseCustomShader", false, DrawIfAttribute.DisablingType.DontDraw)]
#endif
        public Shader CharacterStandardShader;
#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
        [DrawIf("UseCustomShader", false, DrawIfAttribute.DisablingType.DontDraw)]
#endif
        public Shader AssetStandardShader;
#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
        [DrawIf("UseCustomShader", false, DrawIfAttribute.DisablingType.DontDraw)]
#endif
        public Shader GLTFStandard = null; // one of [CharacterStandartShader, AssetStandartShader] depending on prjectType
#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
        [DrawIf("UseCustomShader", false, DrawIfAttribute.DisablingType.DontDraw)]
#endif
        public Shader GLTFStandardSpecular;
#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
        [DrawIf("UseCustomShader", false, DrawIfAttribute.DisablingType.DontDraw)]
#endif
        public Shader GLTFConstant;
#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
        [DrawIf("UseCustomShader", false, DrawIfAttribute.DisablingType.DontDraw)]
#endif
        public UnityEngine.Texture brdf;
#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
        [DrawIf("UseCustomShader", false, DrawIfAttribute.DisablingType.DontDraw)]
#endif
        public UnityEngine.Texture IBLIrradiance;
        [Space(5)]
        public UnityEngine.Texture IBLSpecular;
        public int MaximumLod = 300;
    }
}