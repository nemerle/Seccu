using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
 
[CustomPropertyDrawer(typeof(EnumFlagAttribute))]
public class EnumFlagDrawer : PropertyDrawer {
    public override void OnGUI(Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        uint a;
        EditorGUI.BeginChangeCheck();
        if (property.enumNames.Length == 32)
        {
            //TODO: unity has a bug with full width uint enums.
            var vals = new List<string>(property.enumNames);
            string entry = vals[0];
            vals.RemoveAt(0);
            vals.Add(entry);
            uint flgs = (uint)property.intValue;
            List<string> res=new List<string>();
            for (uint idx = 0; idx < 32; ++idx)
            {
                if(0!=(flgs &(1<<(int)idx)))
                    res.Add(vals[(int)idx]);
            }

            string reported_list = String.Join(",", res);
            
            string result_list = EditorGUI.TextField(position, label, reported_list);
            if (result_list != reported_list)
            {
                Debug.LogErrorFormat("Unit bug encountered: full 32 bit fields are not supported well");
            }

            a = flgs;
        }
        else
        {
            a = (uint)(EditorGUI.MaskField(position, label, property.intValue, property.enumNames));
        }

        if (EditorGUI.EndChangeCheck())
        {
            property.intValue = (int)a;
        }
    }
 }