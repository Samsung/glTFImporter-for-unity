using System;
using UnityEngine;
using UnityGLTF;

namespace GLTFComponentFeature
{
    [Serializable]
    public class GLTFLoadFeature
    {
        public enum AssetLocation
        {
            StreamingAsset,
            Server,
            Else
        }

        public const string alertMessage = "When you are using Android platform, use url load type";
        [SerializeField] public bool loadOnStartUp;
        [SerializeField] public AssetLocation assetLocation;

    #if UNITY_EDITOR || UNITY_STANDALONE_WIN
            [DrawIf("assetLocation", AssetLocation.Server, DrawIfAttribute.DisablingType.DontDrawExclude)]
    #endif
        [SerializeField] public GLTFSceneImporter.LoadType loadType;
        [SerializeField] public string Url;
        [SerializeField] public bool Multithreaded = true;
    #if UNITY_EDITOR || UNITY_STANDALONE_WIN
            [HelpBox("assetLocation", AssetLocation.StreamingAsset, alertMessage, HelpBoxAttribute.HelpBoxType.Info)] public int box = 0;
    #endif
    }
}