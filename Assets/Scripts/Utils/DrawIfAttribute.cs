using UnityEditor;
using UnityEngine;
using System;
#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
public class DrawIfAttribute : PropertyAttribute
{
    #region Fields
    public string comparedPropertyName { get; private set; }
    public object comparedValue { get; private set; }
    public DisablingType disablingType { get; private set; }

    public enum DisablingType
    {
        ReadOnly = 2,
        DontDraw = 3,
        DontDrawExclude = 4
    }

    #endregion

    public DrawIfAttribute(string comparedPropertyName, object comparedValue, DisablingType disablingType = DisablingType.DontDraw)
    {
        this.comparedPropertyName = comparedPropertyName;
        this.comparedValue = comparedValue;
        this.disablingType = disablingType;
    }
}
#endif