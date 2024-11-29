using System;
using UnityEngine;
using UnityEditor;

#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
[AttributeUsage( AttributeTargets.Field, Inherited = true, AllowMultiple = true )]
public class HelpBoxAttribute : PropertyAttribute
{
    #region Fields
    public string message;
    public HelpBoxType type;
    public string comparedPropertyName;
    public object comparedValue;

    public enum HelpBoxType
    { 
        None,
        Info,
        Warning,
        Error,
    }
    #endregion

    public HelpBoxAttribute(string comparedPropertyName, object comparedValue, string message, HelpBoxType type = HelpBoxType.None)
    {
        this.comparedPropertyName = comparedPropertyName;
        this.comparedValue = comparedValue;
        this.message = message;
        this.type = type;
        this.order  = order;
    }
}
#endif
