﻿using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
 
[CustomPropertyDrawer(typeof(EnumFlagAttribute))]
public class EnumFlagDrawer : PropertyDrawer {
    public override void OnGUI(Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        EditorGUI.BeginChangeCheck();
        uint a = (uint)(EditorGUI.MaskField(position, label, property.intValue, property.enumNames));
        if (EditorGUI.EndChangeCheck())
        {
            property.intValue = (int)a;
        }
    }
 }