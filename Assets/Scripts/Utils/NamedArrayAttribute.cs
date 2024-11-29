using System.Linq;
using UnityEditor;
using UnityEngine;
#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)

public class NamedArrayAttribute : PropertyAttribute
    {
        public readonly string[] names;
        public NamedArrayAttribute(string[] names) { this.names = names; }

    }

[CustomPropertyDrawer(typeof(NamedArrayAttribute))]
public class StringInListDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label);
    }

    public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(rect, label, property);
        try
        {
            var path = property.propertyPath;
            int pos = int.Parse(path.Split('[').LastOrDefault().TrimEnd(']'));
            EditorGUI.PropertyField(rect, property, new GUIContent(ObjectNames.NicifyVariableName(((NamedArrayAttribute)attribute).names[pos])), true);
        }
        catch
        {
            EditorGUI.PropertyField(rect, property, label, true);
        }
        EditorGUI.EndProperty();
    }
}
#endif


