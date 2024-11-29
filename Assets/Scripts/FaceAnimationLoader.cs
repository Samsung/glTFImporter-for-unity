using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using GLTF.Extensions;
using UnityEditor;
using UnityEngine.Networking;

public class FaceAnimationLoader
{

    private BlendShapAnimation mAnimation;
    private List<string> existTargetMeshs = new List<string>();
    public bool IsLoaded { get; private set; }

    private enum Status
    {
        Looping,
        Proceeding,         //#Default
        Stop,
    }

    public IEnumerator FaceAnimationLoaderFromWeb(string path)
    {
        IsLoaded = false;
        path = path.Replace("Assets/Resources/", "");
        path = Application.streamingAssetsPath + "/" + path;
        Debug.Log("[profiling] path : " + path);
        UnityWebRequest www = UnityWebRequest.Get(path);
        yield return www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log("[profiling] Failed to load face animation file : " + www.error);
        }
        string jsonText = new StringReader(Encoding.UTF8.GetString(www.downloadHandler.data)).ReadToEnd();
        JsonTextReader jsonReader = new JsonTextReader(new StringReader(jsonText));
        mAnimation = BlendShapAnimation.Deserialize(jsonReader);
        foreach (BlendShape blendShape in mAnimation.BlendShapes)
        {
            existTargetMeshs.Add(blendShape.name);
        }
        IsLoaded = true;
    }
    public FaceAnimationLoader(string path)
    {

        JsonTextReader jsonReader;
        if (Application.platform == RuntimePlatform.Android) //Need to extract file from apk first
        {
            TextAsset asset = Resources.Load<TextAsset>(Path.GetFileNameWithoutExtension(path));
            StringReader streader = new StringReader(asset.text);
            jsonReader = new JsonTextReader(streader);
        }
        else if (Application.platform == RuntimePlatform.OSXEditor)
        {
            //MacOS
            jsonReader = new JsonTextReader(File.OpenText(path));
        }
        else if (Application.platform == RuntimePlatform.VisionOS || Application.platform == RuntimePlatform.IPhonePlayer)
        {
            //VisionOS Simulator
            //IOS Device
            path = path.Replace("Assets/Resources/", "");
            path = Application.streamingAssetsPath + "/" + path;
            jsonReader = new JsonTextReader(File.OpenText(path));
        }
        else if (Application.platform == RuntimePlatform.WindowsEditor)
        {
            path = Path.GetDirectoryName(path).Equals("FaceAnimationFiles") ? "Assets/Resources/" + path : path;
            path = Path.GetExtension(path).Equals("") ? path + ".json" : path;
            jsonReader = new JsonTextReader(File.OpenText(path));
        }
        else if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            return;
        }
        else
        {
            jsonReader = new JsonTextReader(File.OpenText(Application.dataPath + "/Raw/Motion_Face_Excited.json"));
        }
        mAnimation = BlendShapAnimation.Deserialize(jsonReader);


        foreach (BlendShape blendShape in mAnimation.BlendShapes)
            existTargetMeshs.Add(blendShape.name);

    }

    public AnimationClip CreateAnimationClip()
    {
        AnimationClip mAnimationClip = new AnimationClip();
        mAnimationClip.legacy = true;


        Dictionary<string, AnimationCurve> headMorphDic = new Dictionary<string, AnimationCurve>();

        List<float> mtime = mAnimation.time;
        for (int i = 0; i < mAnimation.BlendShapes.Count; i++)
        {
            BlendShape mBlendShape = mAnimation.BlendShapes[i];
            if (!mBlendShape.name.Equals("head_GEO"))
                continue;

            for (int j = 0; j < mBlendShape.morphtarget; j++)
            {
                var morph = mBlendShape.Morphs[j];
                var curve = new AnimationCurve();
                for (int k = 0; k < mtime.Count; k++)
                {
                    var time = mtime[k];
                    curve.AddKey(time, morph.keys[k]);
                }

                string morphname = morph.morphname;
                mAnimationClip.SetCurve("", typeof(SkinnedMeshRenderer), "blendShape." + morphname, curve);
            }
        }

        mAnimationClip.EnsureQuaternionContinuity();
        mAnimationClip.name = "BlendAnimation";
        mAnimationClip.legacy = true;
        mAnimationClip.wrapMode = WrapMode.Loop;
        return mAnimationClip;
    }

    private class Morph
    {
        public String morphname;
        public List<float> keys;
        public static Morph Deserialize(String name, List<List<float>> key, int index)
        {
            var mMorph = new Morph();

            mMorph.morphname = name;
            mMorph.keys = new List<float>();
            for (int i = 0; i < key.Count; i++) mMorph.keys.Add(key[i][index]);
            return mMorph;
        }
    }
    private class BlendShape
    {
        public String name;
        public String fullName;
        public String blendShapeVersion;
        public int morphtarget;
        public List<Morph> Morphs = new List<Morph>();


        public static List<float> ReadFloatList(JsonTextReader reader)
        {
            var list = new List<float>();

            while (reader.Read() && reader.TokenType == JsonToken.Float)
            {
                list.Add(float.Parse(reader.Value.ToString()));
            }
            return list;
        }
        public static List<List<float>> ReadFloatListOfList(JsonTextReader reader)
        {
            if (reader.Read() && reader.TokenType != JsonToken.StartArray)
            {
                throw new Exception("json must be an StartArray");
            }

            List<List<float>> key = new List<List<float>>();

            while (reader.Read() && reader.TokenType == JsonToken.StartArray)
            {
                key.Add(ReadFloatList(reader));
            }
            return key;
        }

        public static BlendShape Deserialize(JsonTextReader JsonReader)
        {
            var mBlendShape = new BlendShape();
            List<string> morphname = null;
            List<List<float>> key = null;
            while (JsonReader.Read() && JsonReader.TokenType == JsonToken.PropertyName)
            {
                var curProp = JsonReader.Value.ToString();
                switch (curProp)
                {
                    case "name":
                        mBlendShape.name = JsonReader.ReadAsString();
                        break;
                    case "fullName":
                        mBlendShape.fullName = JsonReader.ReadAsString();
                        break;
                    case "blendShapeVersion":
                        mBlendShape.blendShapeVersion = JsonReader.ReadAsString();
                        break;
                    case "morphtarget":
                        mBlendShape.morphtarget = (int)JsonReader.ReadAsInt32();
                        break;
                    case "morphname":
                        morphname = JsonReader.ReadStringList();
                        break;
                    case "key":
                        key = ReadFloatListOfList(JsonReader);
                        break;
                    default:
                        break;
                }
            }
            for (int i = 0; i < mBlendShape.morphtarget; i++)
                mBlendShape.Morphs.Add(Morph.Deserialize(morphname[i], key, i));

            return mBlendShape;

        }
    }
    private class BlendShapAnimation
    {
        public String name;
        public String Version;
        public List<BlendShape> BlendShapes;
        public int shapesAmount;
        public List<float> time = new List<float>();
        public int frames;

        public static BlendShapAnimation Deserialize(JsonTextReader JsonReader)
        {

            BlendShapAnimation mBlendAni = new BlendShapAnimation();
            List<double> timetmp;
            if (JsonReader.Read() && JsonReader.TokenType != JsonToken.StartObject)
            {
                throw new Exception("gltf json must be an object");
            }
            while (JsonReader.Read() && JsonReader.TokenType == JsonToken.PropertyName)
            {

                var curProp = JsonReader.Value.ToString();

                switch (curProp)
                {
                    case "name":
                        mBlendAni.name = JsonReader.ReadAsString();
                        break;
                    case "version":
                        mBlendAni.Version = JsonReader.ReadAsString();
                        break;
                    case "blendShapes":
                        mBlendAni.BlendShapes = JsonReader.ReadList(() => BlendShape.Deserialize(JsonReader));
                        break;
                    case "shapesAmount":
                        mBlendAni.shapesAmount = (int)JsonReader.ReadAsInt32();
                        break;
                    case "time":
                        timetmp = JsonReader.ReadDoubleList();
                        for (int i = 0; i < timetmp.Count; i++) mBlendAni.time.Add((float)timetmp[i] / 1000.0f);
                        break;
                    case "frames":
                        mBlendAni.frames = (int)JsonReader.ReadAsInt32();
                        break;
                    default:
                        break;
                }
            }
            return mBlendAni;
        }
    }

}
