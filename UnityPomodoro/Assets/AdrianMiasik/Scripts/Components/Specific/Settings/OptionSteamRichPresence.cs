using System;
using AdrianMiasik.Components.Base;
using AdrianMiasik.Components.Core.Containers;
using AdrianMiasik.Components.Core.Items.Pages;
using AdrianMiasik.Components.Core.Settings;
#if !UNITY_ANDROID && !UNITY_WSA
using Steamworks;
#endif

namespace AdrianMiasik.Components.Specific.Settings
{
    /// <summary>
    /// A <see cref="SettingsOptionDropdown"/> that toggles our Steam rich presence functionality.
    /// (See <see cref="DigitFormat"/>, also see <seealso cref="SettingsPage"/>)
    /// </summary>
    public class OptionSteamRichPresence : SettingsOptionToggleSlider
    {
        /// <summary>
        /// Sets the dropdown value to the <see cref="PomodoroTimer"/>'s current digit format.
        /// </summary>
        /// <param name="pomodoroTimer"></param>
        public override void Initialize(PomodoroTimer pomodoroTimer)
        {
            base.Initialize(pomodoroTimer);
            
#if !UNITY_ANDROID && !UNITY_WSA
            m_toggleSlider.m_onSetToTrueClick.AddListener(() =>
            {
                SetSteamRichPresence(true);
            });
            
            m_toggleSlider.m_onSetToFalseClick.AddListener(() =>
            {
                SetSteamRichPresence(false);
            });
#endif

            m_toggleSlider.Initialize(pomodoroTimer, Timer.GetSystemSettings().m_enableSteamRichPresence);
        }
        
#if !UNITY_ANDROID && !UNITY_WSA
        private void SetSteamRichPresence(bool enableRichPresence)
        {
            if (!enableRichPresence)
            {
                // Turn off rich presence
                
                // Set and apply
                Timer.GetSystemSettings().m_enableSteamRichPresence = false;
                UserSettingsSerializer.SaveSettingsFile(Timer.GetSystemSettings().m_enableSteamRichPresence, "system-settings");
                
                // Clear current state if present
                if (Timer.IsSteamworksRichPresenceInitialized())
                {
                    Timer.ClearSteamRichPresence();
                    // TODO: hide settings option?
                }
            }
            else
            {
                // Otherwise...Turn on rich presence
                
                // Set and apply
                Timer.GetSystemSettings().m_enableSteamRichPresence = true;
                UserSettingsSerializer.SaveSettingsFile(Timer.GetSystemSettings().m_enableSteamRichPresence, "system-settings");
                
                if (!Timer.IsSteamworksRichPresenceInitialized())
                {
                    Timer.InitializeSteamRichPresence();
                    // TODO: show settings options?
                }
                
                // Update rich presence state
                Timer.UpdateSteamRichPresence();
            }
        }
#endif
    }
}