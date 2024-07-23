using System;
using AdrianMiasik.Components.Specific.Platforms.UWP;

namespace AdrianMiasik.Components.Core.Settings
{
    /// <summary>
    /// Global settings for the application.
    /// </summary>
    [Serializable]
    public class SystemSettings
    {
        /// <summary>
        /// Do you want to use the dark mode variation of the current active theme?
        /// If `True`, the application will use darker colors from the active theme.
        /// If `False`, the application will use lighter colors from the active theme.
        /// </summary>
        public bool m_darkMode = true;        
        
        /// <summary>
        /// Should we mute the sound when the application is not in focus?
        /// If `True`, the application audio will not be played when it's not in focus.
        /// If `False`, the application audio will emit audio regardless of focus state. 
        /// <remarks> We want this to be `True` by default for the Universal Windows Platform since we
        /// pull focus back via UWP notification. (See <see cref="UWPNotifications"/>)</remarks>
        /// </summary>
        public bool m_muteSoundWhenOutOfFocus;

        /// <summary>
        /// Do you want to enable the Unity Analytics service?
        /// If `True`, the Unity Analytics service will track user data.
        /// If `False`, the Unity Analytics service will no longer run and track user data.
        /// </summary>
        public bool m_enableUnityAnalytics = true;
        
        /// <summary>
        /// Do you want to enable the Steam Rich Presence functionality?
        /// If `True`, users state and custom labels will be shared with friends via Steam chat client.
        /// If `False`, users state and custom labels will not be shared on Steam.
        /// </summary>
        public bool m_enableSteamRichPresence = true;
    }
}