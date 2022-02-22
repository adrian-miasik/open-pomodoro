using AdrianMiasik.Components.Base;
using AdrianMiasik.Components.Core;
using AdrianMiasik.Components.Core.Containers;
using AdrianMiasik.ScriptableObjects;
using TMPro;
using UnityEngine;

namespace AdrianMiasik.Components.Specific
{
    /// <summary>
    /// A <see cref="ThemeElement"/> page used to display information about our application.
    /// Includes a description, social buttons (<seealso cref="ThemeIconContainer"/>), version number, and a
    /// copyright disclaimer.
    /// </summary>
    public class AboutPanel : ThemeElement
    {
        [SerializeField] private TMP_Text m_title;
        [SerializeField] private TMP_Text m_description;
        [SerializeField] private ThemeIconContainer m_socials;
        [SerializeField] private WriteVersionNumber m_versionNumber;
        [SerializeField] private TMP_Text m_copyrightDisclaimer;
        
        private bool isAboutPageOpen;

        public override void Initialize(PomodoroTimer pomodoroTimer, bool updateColors = true)
        {
            base.Initialize(pomodoroTimer, updateColors);

            m_socials.Initialize(pomodoroTimer, updateColors);
        }

        /// <summary>
        /// Applies our <see cref="Theme"/> changes to our referenced components when necessary.
        /// </summary>
        /// <param name="theme">The theme to apply on our referenced components.</param>
        public override void ColorUpdate(Theme theme)
        {
            if (!isAboutPageOpen)
            {
                return;
            }
            
            ColorScheme currentColors = theme.GetCurrentColorScheme();
            m_title.color = currentColors.m_foreground;
            m_description.color = currentColors.m_foreground;
            m_socials.ColorUpdate(theme);
            m_versionNumber.SetTextColor(currentColors.m_foreground);
            m_copyrightDisclaimer.color = currentColors.m_foreground;
        }

        /// <summary>
        /// Is this <see cref="AboutPanel"/> currently open and visible?
        /// </summary>
        /// <returns></returns>
        public bool IsAboutPageOpen()
        {
            return isAboutPageOpen;
        }

        /// <summary>
        /// Displays this panel to the user.
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            isAboutPageOpen = true;
            
            ColorUpdate(Timer.GetTheme());
        }

        /// <summary>
        /// Hides this panel away from the user.
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            isAboutPageOpen = false;
        }
    }
}