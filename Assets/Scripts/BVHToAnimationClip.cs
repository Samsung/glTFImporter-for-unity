using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class BVHToAnimationClip : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
        FileInfo[] files = GetFiles();


        for(int idx = 0;idx< files.Length; idx++)
        {
            string path = "Assets/StreamingAssets/BVHFiles/"+ files[idx].Name;

            BvhLoader test = new BvhLoader(path);
            AnimationClip Animation = test.CreateAnimationClip();

            Animation.legacy = false;
            AssetDatabase.CreateAsset(Animation, "Assets/Resources/Animations/BodyAnimationClips/" + files[idx].Name.Split('.')[0] + ".anim");
            AssetDatabase.SaveAssets();
        }
#endif
    }


    public FileInfo[] GetFiles()
    {
        string path = string.Format("{0}", Application.streamingAssetsPath+ "/BVHFiles");
        //string path = string.Format("{0}", @"C:\Users\USER\Desktop\JXBWG\Assets\StreamingAssets");

        if (Directory.Exists(path))
        {
            DirectoryInfo direction = new DirectoryInfo(path);
            FileInfo[] files = direction.GetFiles("*.bvh");
            for (int i = 0; i < files.Length; i++)
            {
                //      
                if (files[i].Name.EndsWith(".meta"))
                {
                    continue;
                }
                Debug.Log("   :" + files[i].Name);
                Debug.Log("      :" + files[i].FullName);
                Debug.Log("      :" + files[i].DirectoryName);
            }
            return files;
        }
        return null;
    }



}
