using System.Collections.Generic;
using AdrianMiasik.Components.Core;
using AdrianMiasik.ScriptableObjects;
using TMPro;
using UnityEngine;

namespace AdrianMiasik.Components
{
    public class CompletionLabel : ThemeElement
    {
        [SerializeField] private List<TMP_Text> m_labels = new List<TMP_Text>();
        
        public override void ColorUpdate(Theme theme)
        {
            foreach (TMP_Text text in m_labels)
            {
                text.color = theme.GetCurrentColorScheme().m_complete;
            }
        }
    }
}
