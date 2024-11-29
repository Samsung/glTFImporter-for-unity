using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
public static class StringInListDrawerHelper
{
    public static string[] getFaceAnimationFilePathList()
    {
        var temp = new List<string>();
        string[] files = Directory.GetFiles("Assets/Resources/FaceAnimationFiles", "*.json");

        foreach (string file in files)
        {
            temp.Add(file);
        }
        return temp.ToArray();
    }

    public static string[] getFaceTrackingFilePathList()
    {
        var temp = new List<string>();
        string[] files = {"empty"};

        foreach (string file in files)
        {
            temp.Add(file);
        }
        return temp.ToArray();
    }

    public static string[] getFaceModeList()
    {
        var temp = new List<string>();
        temp.Add("Face Tracking");
        temp.Add("Face Animation");
        return temp.ToArray();
    }
}
#endif