using System;
using AdrianMiasik.Components.Base;
using AdrianMiasik.Components.Core.Containers;
using AdrianMiasik.Components.Core.Settings;
using AdrianMiasik.Components.Specific.Settings;
using AdrianMiasik.ScriptableObjects;
using Steamworks;
using UnityEngine;

namespace AdrianMiasik.Components.Core.Items.Pages
{
    /// <summary>
    /// A <see cref="Page"/> used to display a set of interactable <see cref="SystemSettings"/> and
    /// <see cref="TimerSettings"/>.
    /// </summary>
    public class SettingsPage : Page
    {
        [Header("Dropdowns")]
        [SerializeField] private OptionDigitFormat m_optionDigitFormat;
        [SerializeField] private OptionPomodoroCount m_optionPomodoroCount;
        [SerializeField] private OptionSetAlarmSound m_optionSetAlarmSound;

        [Header("Toggle Sliders")]
        [SerializeField] private OptionEnableLongBreaks m_optionEnableLongBreak;
        [SerializeField] private OptionMuteAudioOutOfFocus m_optionMuteSoundOutOfFocusToggle;
        [SerializeField] private OptionUnityAnalytics m_optionUnityAnalytics;
        [SerializeField] private OptionSteamRichPresence m_optionSteamRichPresence;
        
        public void Initialize(PomodoroTimer pomodoroTimer)
        {
            base.Initialize(pomodoroTimer, false);
            
            // Init
            m_optionDigitFormat.Initialize(Timer);
            m_optionPomodoroCount.Initialize(Timer);
            m_optionSetAlarmSound.Initialize(Timer);
            m_optionEnableLongBreak.Initialize(Timer);
#if UNITY_ANDROID
            HideMuteSoundOutOfFocusOption();
#elif UNITY_IOS
            HideMuteSoundOutOfFocusOption();
#else
            m_optionMuteSoundOutOfFocusToggle.Initialize(Timer);
#endif
            
            if (pomodoroTimer.IsSteamworksInitialized())
            {
                // Init settings option
                m_optionSteamRichPresence.Initialize(Timer);
            }
            else
            {
                // Hide settings option
                m_optionSteamRichPresence.gameObject.SetActive(false);
                m_optionSteamRichPresence.m_spacer.gameObject.SetActive(false);
            }

#if ENABLE_CLOUD_SERVICES_ANALYTICS
            m_optionUnityAnalytics.Initialize(Timer);
#else
            // Hide settings option if Unity Analytics not enabled on this platform.
            m_optionUnityAnalytics.gameObject.SetActive(false);
            m_optionUnityAnalytics.m_spacer.gameObject.SetActive(false);
#endif
        }

        /// <summary>
        /// Updates all elements present on this page using the current loaded System and Timer settings.
        /// </summary>
        public override void Refresh()
        {
            base.Refresh();
            
            // Gets triggered via dropdown on value changed event
            m_optionDigitFormat.SetDropdownValue((int)Timer.GetTimerSettings().m_format);
            m_optionPomodoroCount.SetDropdownValue(Timer.GetTimerSettings().m_pomodoroCount - 1);
            m_optionSetAlarmSound.SetDropdownValue(Timer.GetTimerSettings().m_alarmSoundIndex);
            
            m_optionEnableLongBreak.Refresh(Timer.GetTimerSettings().m_longBreaks);
            m_optionMuteSoundOutOfFocusToggle.Refresh(Timer.GetSystemSettings().m_muteSoundWhenOutOfFocus);
            m_optionUnityAnalytics.Refresh(Timer.GetSystemSettings().m_enableUnityAnalytics);
            m_optionSteamRichPresence.Refresh(Timer.GetSystemSettings().m_enableSteamRichPresence);
        }

        /// <summary>
        /// Applies our <see cref="Theme"/> changes to our referenced components when necessary.
        /// </summary>
        /// <param name="theme">The theme to apply on our referenced components.</param>
        public override void ColorUpdate(Theme theme)
        {
            base.ColorUpdate(theme);
            
            m_optionDigitFormat.ColorUpdate(theme);
            m_optionPomodoroCount.ColorUpdate(theme);
            m_optionSetAlarmSound.ColorUpdate(theme);

            m_optionEnableLongBreak.ColorUpdate(theme);
            m_optionMuteSoundOutOfFocusToggle.ColorUpdate(theme);
            m_optionUnityAnalytics.ColorUpdate(theme);
        }

        /// <summary>
        /// Updates the switch digit layout dropdown to use the current timer's digit format.
        /// </summary>
        public void UpdateDigitFormatDropdown()
        {
            m_optionDigitFormat.SetDropdownValue(Timer.GetDigitFormatIndex());
        }
        
        /// <summary>
        /// Sets the switch digit layout dropdown to the provided digit format index.
        /// (See: <see cref="DigitFormat.SupportedFormats"/>)
        /// </summary>
        /// <param name="value">The index value you want the dropdown to be set at.</param>
        public void SetDigitFormatDropdown(int value)
        {
            m_optionDigitFormat.SetDropdownValue(value);
        }

        /// <summary>
        /// Displays this panel to the user.
        /// </summary>
        public override void Show(Action onAnimationCompletion)
        {
            m_optionDigitFormat.SetDropdownValue(Timer.GetDigitFormatIndex());
            
            base.Show(onAnimationCompletion);
            
            Timer.SteamTrySetRichPresence("steam_display", "#Settings");
        }

        public override void Hide(Action onAnimationCompletion)
        {
            base.Hide(onAnimationCompletion);
            
            Timer.UpdateSteamRichPresence();
        }

        /// <summary>
        /// Shows the 'sound mute when application is out of focus' option to the user.
        /// <remarks>Intended to be shown for desktop users, not mobile.</remarks>
        /// </summary>
        public void ShowMuteSoundOutOfFocusOption()
        {
            m_optionMuteSoundOutOfFocusToggle.gameObject.SetActive(true);
            m_optionMuteSoundOutOfFocusToggle.m_spacer.gameObject.SetActive(true);
        }

        /// <summary>
        /// Hides the 'sound mute when application is out of focus' option from the user.
        /// <remarks>Intended to be hidden for mobile users, not desktop.</remarks>
        /// </summary>
        public void HideMuteSoundOutOfFocusOption()
        {
            m_optionMuteSoundOutOfFocusToggle.gameObject.SetActive(false);
            m_optionMuteSoundOutOfFocusToggle.m_spacer.gameObject.SetActive(false);
        }

        // Piper
        public void AddCustomDropdownSoundOption(AudioClip audioClip, string audioFile)
        {
            m_optionSetAlarmSound.AddCustomDropdownSoundOption(audioClip, audioFile);
        }

        public void RemoveCustomDropdownSoundOption(string audioFile)
        {
            m_optionSetAlarmSound.RemoveCustomDropdownSoundOption(audioFile);
        }

        public void ValidateCustomSoundChoice()
        {
            m_optionSetAlarmSound.ValidateAlarmSoundIndex();
        }
    }
}