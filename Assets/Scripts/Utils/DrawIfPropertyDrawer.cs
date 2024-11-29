using UnityEditor;
using UnityEngine;

#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
[CustomPropertyDrawer(typeof(DrawIfAttribute))]
public class DrawIfPropertyDrawer : PropertyDrawer
{
    #region Fields
    DrawIfAttribute drawIf;
    SerializedProperty comparedField;

    #endregion

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!ShowMe(property) && drawIf.disablingType == DrawIfAttribute.DisablingType.DontDraw){
            return 0f;
        }
        if (ShowMe(property) && drawIf.disablingType == DrawIfAttribute.DisablingType.DontDrawExclude){
            return 0f;
        }
        return base.GetPropertyHeight(property, label);
    }

    private int CountDot(string path)
    {
        if (!path.Contains("."))
        {
            return 0;
        }
        else if (path.IndexOf(".") != path.LastIndexOf("."))
        {
            return 2;
        }
        else
        {
            return 1;
        }
    }

    private string setPropertyPath(SerializedProperty property)
    {
        int count = CountDot(property.propertyPath);
        if (count == 0)
        {
            return drawIf.comparedPropertyName;
        }
        else if (count == 1)
        {
            return System.IO.Path.ChangeExtension(property.propertyPath, drawIf.comparedPropertyName);
        }
        else
        {
            return property.propertyPath.Remove(property.propertyPath.IndexOf(".")) + "." + drawIf.comparedPropertyName;
        }
    }

    private bool ShowMe(SerializedProperty property)
    {
        drawIf = attribute as DrawIfAttribute;
        string path = setPropertyPath(property);
        comparedField = property.serializedObject.FindProperty(path);

        if (comparedField == null)
        {
            Debug.LogError("Cannot find property with name: " + path);
            return true;
        }

        switch (comparedField.type)
        {
            case "bool":
                return comparedField.boolValue.Equals(drawIf.comparedValue);
            case "Enum":
                return comparedField.enumValueIndex.Equals((int)drawIf.comparedValue);
            default:
                Debug.LogError("Error: " + comparedField.type + " is not supported of " + path);
                return true;
        }
    }
    
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (ShowMe(property) && drawIf.disablingType != DrawIfAttribute.DisablingType.DontDrawExclude){
            EditorGUI.PropertyField(position, property, label);
        } 
        else if(!ShowMe(property) && drawIf.disablingType == DrawIfAttribute.DisablingType.DontDrawExclude){
            EditorGUI.PropertyField(position, property, label);
        }
        else if (drawIf.disablingType == DrawIfAttribute.DisablingType.ReadOnly)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label);
            GUI.enabled = true;
        }
    }
}
#endif