using System;
using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class StringInListAttribute : PropertyAttribute
{
    public delegate string[] GetStringList();

    public StringInListAttribute(params string[] list)
    {
        pathList = list;
    }

    public StringInListAttribute(Type type, string methodName)
    {
        var method = type.GetMethod(methodName);
        if (method != null)
        {
            pathList = method.Invoke(null, null) as string[];
        }
        else
        {
            Debug.LogError("NO SUCH METHOD " + methodName + " FOR " + type);
        }
    }

    public string[] pathList
    {
        get;
        private set;
    }
}
#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
namespace UnityEditor{
[CustomPropertyDrawer(typeof(StringInListAttribute))]
    public class StringInListDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label);
        }

        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var stringInList = attribute as StringInListAttribute;
            var pathList = stringInList.pathList;
            var temp = new List<string>();

            foreach(string path in pathList)
                temp.Add(Path.GetFileNameWithoutExtension(path));
            var fields = temp.ToArray();

            if (property.propertyType == SerializedPropertyType.String)
            {
                int index = Mathf.Max(0, Array.IndexOf(pathList, property.stringValue));
                index = EditorGUI.Popup(position, property.displayName, index, fields);
                property.stringValue = pathList[index];

            }
            else if (property.propertyType == SerializedPropertyType.Integer)
            {
                property.intValue = EditorGUI.Popup(position, property.displayName, property.intValue, fields);
            }
            else
            {
                base.OnGUI(position, property, label);
            }
            EditorGUI.EndProperty();
        }
    }
}
#endif