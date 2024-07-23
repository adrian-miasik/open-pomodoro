#if !UNITY_ANDROID && !UNITY_WSA
using System;
using System.Threading;
using AdrianMiasik.Interfaces;
using AdrianMiasik.ScriptableObjects;
using Steamworks;
using UnityEngine;

namespace AdrianMiasik.Components.Specific.Platforms.Steam
{
    public class SteamRichPresence : MonoBehaviour, ITimerState
    {
        private PomodoroTimer pomodoroTimer;
        private bool isInitialized;
        
        public void Initialize(PomodoroTimer timer)
        {
            pomodoroTimer = timer;
            isInitialized = true;
        }

        private void Update()
        {
            if (!isInitialized)
            {
                // Early exit
                return;
            }
            
            SetRichPresence("time_left", pomodoroTimer.GetTimerString(true));
            SetRichPresence("time_suffix", " left.");
        }

        public bool IsInitialized()
        {
            return isInitialized;
        }

        public void StateUpdate(PomodoroTimer.States state, Theme theme)
        {
            if (!isInitialized)
            {
                return;
            }
            
            switch (state)
            {
                case PomodoroTimer.States.SETUP:
                    SetRichPresence("steam_display", "#Setup");
                    break;
                case PomodoroTimer.States.RUNNING:
                    SetRichPresence("steam_display", "#Running");
                    break;
                case PomodoroTimer.States.PAUSED:
                    SetRichPresence("steam_display", "#Paused");
                    break;
                case PomodoroTimer.States.COMPLETE:
                    SetRichPresence("steam_display", "#Completed");
                    break;
            }
        }
        
        public void SetRichPresence(string key, string value)
        {
            if (!isInitialized)
            {
                // Debug.LogWarning("Unable to set steam rich presence, class is not currently initialized.");
                return;
            }
            
            SteamFriends.SetRichPresence(key, value);
        }
        
        public void Clear()
        {
            if (isInitialized)
            {
                SteamFriends.ClearRichPresence();
            }

            pomodoroTimer = null;
            isInitialized = false;
        }
    }
}
#endif
