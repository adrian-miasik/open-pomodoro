﻿using AdrianMiasik.Components.Core;
using UnityEngine;

namespace AdrianMiasik.Components.Specific.Settings
{
    /// <summary>
    /// A settings option intended be used for 'Enable long break' settings option. (See <see cref="SettingsPanel"/>)
    /// </summary>
    public class OptionEnableLongBreaks : SettingsOptionToggleSlider
    {
        public override void Initialize(PomodoroTimer pomodoroTimer, SystemSettings systemSettings)
        {
            base.Initialize(pomodoroTimer, systemSettings);
            if (Timer.GetTimerSettings().m_longBreaks)
            {
                m_toggleSlider.Initialize(pomodoroTimer, Timer.GetTimerSettings().m_longBreaks);
            }
        }

        /// <summary>
        /// <remarks>Used as a UnityEvent by toggle.</remarks>
        /// </summary>
        /// <param name="state"></param>
        public void SetSettingLongBreak(bool state)
        {
            // Apply and save
            Timer.GetTimerSettings().m_longBreaks = state;
            UserSettingsSerializer.SaveTimerSettings(Timer.GetTimerSettings());
            Debug.Log("Timer Settings Saved.");
            
            if (state == false)
            {
                if (Timer.HasTomatoProgression())
                {
                    // Prompt for user permission first
                    Timer.GetConfirmDialogManager().SpawnConfirmationDialog(() =>
                    {
                        Timer.SetSettingLongBreaks(false);
                    }, () =>
                    {
                        // Cancel visuals if they don't agree, similar how we do the work/break slider
                        m_toggleSlider.Initialize(Timer, true);
                    }, "This action will delete your current pomodoro/tomato progress.");
                }
                else
                {
                    // Set immediately
                    Timer.SetSettingLongBreaks(false);
                }
            }
            else
            {
                // Set immediately
                Timer.SetSettingLongBreaks();
            }
        }
    }
}