using AdrianMiasik.Components.Base;
using AdrianMiasik.Components.Core.Containers;
using AdrianMiasik.ScriptableObjects;
using TMPro;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdrianMiasik.Components.Core.Items
{
    /// <summary>
    /// A <see cref="ThemeElement"/> <see cref="ClickButton"/> used to display a SVG image, and a text label.
    /// Includes a spawn animation.
    /// (Also see <see cref="Sidebar"/>)
    /// </summary>
    public class SidebarRow : ThemeElement, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Animation m_spawn;
        [SerializeField] private RectTransform m_container;
        [SerializeField] private ClickButton m_button;
        [SerializeField] private Image m_accent;
        [SerializeField] private RectTransform m_contentContainer;
        [SerializeField] private AnimationCurve m_contentContainerOffsetCurve = 
            AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField] private Image m_background;
        [SerializeField] private SVGImage m_icon;
        [SerializeField] private SVGImage m_iconBackground;
        [SerializeField] private TMP_Text m_label;
        [SerializeField] private RectTransform m_labelRectTransform;
        [SerializeField] private bool m_isSelectable = true;
        
        // Cache
        private Sidebar sidebar;
        private bool isSelected;
        private bool isAnimating;
        private float accumulatedAnimationTime;
        private float startingOffsetInPixels;
        private float targetOffsetInPixels;
        private float startingMaxFontSize;

        public void Initialize(PomodoroTimer pomodoroTimer, Sidebar parentSidebar, bool selected = false)
        {
            base.Initialize(pomodoroTimer);
            sidebar = parentSidebar;
            isSelected = selected;
            if (isSelected)
            {
                Select();
            }
            
            startingMaxFontSize = m_label.fontSizeMax;
            m_button.m_onClick.AddListener(OnClick);
        }

        private void Update()
        {
            if (!IsInitialized())
            {
                return; // Early exit
            }

            if (isAnimating)
            {
                // Calculate time and offset
                accumulatedAnimationTime += Time.deltaTime;
                float currentOffset = Mathf.Lerp(startingOffsetInPixels, targetOffsetInPixels,
                    m_contentContainerOffsetCurve.Evaluate(accumulatedAnimationTime));

                // Apply offset
                m_contentContainer.offsetMin = new Vector2(currentOffset, m_contentContainer.offsetMin.y); // Left
                m_contentContainer.offsetMax = new Vector2(currentOffset, m_contentContainer.offsetMax.y); // Right
                
                // If completed...
                if (accumulatedAnimationTime >= 
                    m_contentContainerOffsetCurve.keys[m_contentContainerOffsetCurve.length - 1].time)
                {
                    // Move to final position
                    m_contentContainer.offsetMin = new Vector2(targetOffsetInPixels, m_contentContainer.offsetMin.y);
                    m_contentContainer.offsetMax = new Vector2(targetOffsetInPixels, m_contentContainer.offsetMax.y);
                    
                    // Stop animating
                    isAnimating = false;
                }
            }
        }
        
        /// <summary>
        /// Move content aside
        /// </summary>
        private void OffsetContent()
        {
            startingOffsetInPixels = m_contentContainer.offsetMin.x;
            targetOffsetInPixels = 6;
            accumulatedAnimationTime = 0;
            isAnimating = true;
        }

        /// <summary>
        /// Move content back to original location
        /// </summary>
        private void ResetContentOffset()
        {
            startingOffsetInPixels = m_contentContainer.offsetMin.x;
            targetOffsetInPixels = 0;
            accumulatedAnimationTime = 0;
            isAnimating = true;
        }

        public void Hide()
        {
            m_container.gameObject.SetActive(false);
        }
        
        public void PlaySpawnAnimation()
        {
            Show();
            m_spawn.Play();
        }

        public void Show()
        {
            m_container.gameObject.SetActive(true);
        }

        // UnityEvent
        public void OnClick()
        {
            sidebar.SelectRow(this, m_button.m_clickSound.clip);
        }
        
        [ContextMenu("Select")]
        public void Select()
        {
            ResetContentOffset();            
            
            // Set width of accent
            m_accent.rectTransform.sizeDelta = new Vector2(6f, m_accent.rectTransform.sizeDelta.y);
            
            m_background.color = Timer.GetTheme().GetCurrentColorScheme().m_backgroundHighlight;

            isSelected = true;
        }

        [ContextMenu("Deselect")]
        public void Deselect()
        {
            ResetContentOffset();
            
            // Remove accent
            m_accent.rectTransform.sizeDelta = new Vector2(0, m_accent.rectTransform.sizeDelta.y);
            
            m_background.color = Timer.GetTheme().GetCurrentColorScheme().m_background;

            isSelected = false;
        }

        public void CancelHold()
        {
            m_button.CancelHold();
        }

        public override void ColorUpdate(Theme theme)
        {
            // Backgrounds
            m_background.color = isSelected ? theme.GetCurrentColorScheme().m_backgroundHighlight : 
                theme.GetCurrentColorScheme().m_background;
            m_iconBackground.color = theme.GetCurrentColorScheme().m_background;
            
            // Foreground
            m_icon.color = theme.GetCurrentColorScheme().m_foreground;
            m_label.color = theme.GetCurrentColorScheme().m_foreground;
        }

        public bool IsSelected()
        {
            return isSelected;
        }

        public bool IsSelectable()
        {
            return m_isSelectable;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_background.color = Timer.GetTheme().GetCurrentColorScheme().m_backgroundHighlight;
            OffsetContent();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ResetContentOffset();
            
            if (!IsSelected())
            {
                m_background.color = Timer.GetTheme().GetCurrentColorScheme().m_background;
            }
        }

        public ClickButton GetClickButton()
        {
            return m_button;
        }

        public void ResetMaxFontSize()
        {
            m_label.fontSizeMax = startingMaxFontSize;
        }

        public float GetLabelFontSize()
        {
            return m_label.fontSize;
        }

        public void SetMaxFontSize(float desiredMaxSize)
        {
            m_label.fontSizeMax = desiredMaxSize;
        }
    }
}