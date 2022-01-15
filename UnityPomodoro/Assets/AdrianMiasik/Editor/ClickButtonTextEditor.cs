﻿using AdrianMiasik.Components.Base;
using AdrianMiasik.Components.Core;
using TMPro;
using UnityEditor;

namespace AdrianMiasik.Editor
{
    [CustomEditor(typeof(ClickButtonText))]
    public class ClickButtonTextEditor: ClickButtonEditor
    {
        protected override void DrawInheritorFields(ClickButton _clickButton)
        {
            // Fetch target script
            ClickButtonText textButton = (ClickButtonText) target;
            
            textButton.m_text = (TMP_Text) EditorGUILayout.ObjectField("Text", textButton.m_text, typeof(TMP_Text), true);
        }
    }
}