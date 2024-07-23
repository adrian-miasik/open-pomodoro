using System;
using AdrianMiasik.Components.Base;
using AdrianMiasik.Interfaces;
using AdrianMiasik.ScriptableObjects;
using Steamworks;
using TMPro;
using UnityEngine;

namespace AdrianMiasik.Components.Core
{
    public class InputLabelText : ThemeElement, ITimerState
    {
        [SerializeField] private TMP_InputField m_inputText;

        // A bool tracking if a timer state label is overridden (Use custom user text/default fallback?)
        private bool isOverridingDefaultWorkText;
        private bool isOverridingDefaultBreakText;
        private bool isOverridingDefaultLongBreakText;

        // Custom user text for each timer state/context
        private string workText;
        private string breakText;
        private string longBreakText;

        public override void Initialize(PomodoroTimer pomodoroTimer, bool updateColors = true)
        {
            base.Initialize(pomodoroTimer, updateColors);

            // Register deselect event -> Submit text on deselection
            m_inputText.onDeselect.AddListener(OnDeselect);

            // Use default text
            SetUserText(string.Empty);

            // Register event
            m_inputText.onSubmit.AddListener(SetUserText);

            // Set default themes
            SetTextColor(Timer.GetTheme().GetCurrentColorScheme().m_backgroundHighlight);
            m_inputText.placeholder.color = Timer.GetTheme().GetCurrentColorScheme().m_backgroundHighlight;
        }

        private void OnDeselect(string inputFieldString)
        {
            // Submit on deselection
            m_inputText.onSubmit.Invoke(m_inputText.text);

            // Debug.Log("The input label text has been deselected: " + inputFieldString);
        }
        
        public void UpdateSteamRichPresenceLabel()
        {
            if (!Timer.IsOnBreak())
            {
                SetUserText("Set a work time");
            }
            else if (Timer.IsOnLongBreak())
            {
                SetUserText("Set a long break time");
            }
            else
            {
                SetUserText("Set a break time");
            }
        }

        /// <summary>
        /// Sets the label based on the current timer state.
        /// </summary>
        /// <param name="text"></param>
        private void SetUserText(string text)
        {
            if (text == String.Empty ||
                text == "Set a work time" ||
                text == "Set a break time" ||
                text == "Set a long break time")
            {
                SetUserStateText(string.Empty, false);
            }
            else
            {
                SetUserStateText(text, true);
                SetTextColor(Timer.GetTheme().GetCurrentColorScheme().m_foreground);
            }

            // Apply state context
            StateUpdate(Timer.m_state, Timer.GetTheme());
        }

        /// <summary>
        /// Set's the internal string cache based on the current timer state/context.
        /// </summary>
        /// <param name="desiredText"></param>
        /// <param name="isOverridingDefaultText"></param>
        private void SetUserStateText(string desiredText, bool isOverridingDefaultText)
        {
            // Steam rich presence
            bool isTextEmpty = desiredText == String.Empty;

            if (!Timer.IsOnBreak())
            {
                isOverridingDefaultWorkText = isOverridingDefaultText;
                workText = desiredText;
                
                Timer.SteamTrySetRichPresence("label", isTextEmpty ? "Work Timer" : desiredText);
            }
            else
            {
                if (!Timer.IsOnLongBreak())
                {
                    isOverridingDefaultBreakText = isOverridingDefaultText;
                    breakText = desiredText;

                    Timer.SteamTrySetRichPresence("label", isTextEmpty ? "Break Timer" : desiredText);
                }
                else
                {
                    isOverridingDefaultLongBreakText = isOverridingDefaultText;
                    longBreakText = desiredText;
                    
                    Timer.SteamTrySetRichPresence("label", isTextEmpty ? "Long Break Timer" : desiredText);
                }
            }
        }

        /// <summary>
        /// Return's the custom user text for the current timer state/context.
        /// </summary>
        /// <returns></returns>
        private string GetUserStateText()
        {
            if (!Timer.IsOnBreak())
            {
                return workText;
            }

            return !Timer.IsOnLongBreak() ? breakText : longBreakText;
        }

        /// <summary>
        /// Is the current timer state using custom user text?
        /// </summary>
        /// <returns></returns>
        private bool IsOverridingDefaultStateText()
        {
            if (!Timer.IsOnBreak())
            {
                return isOverridingDefaultWorkText;
            }

            return !Timer.IsOnLongBreak() ? isOverridingDefaultBreakText : isOverridingDefaultLongBreakText;
        }

        /// <summary>
        /// Sets the font color to the provided desired color.
        /// </summary>
        /// <param name="desiredColor"></param>
        public void SetTextColor(Color desiredColor)
        {
            m_inputText.textComponent.color = desiredColor;
        }

        /// <summary>
        /// Interface: Invoked everytime the timer changes state + when submitting text
        /// </summary>
        public void StateUpdate(PomodoroTimer.States state, Theme theme)
        {
            switch (state)
            {
                case PomodoroTimer.States.SETUP:
                    // If we are using defaults...Use pre-defined text
                    if (!IsOverridingDefaultStateText())
                    {
                        if (!Timer.IsOnBreak())
                        {
                            m_inputText.text = "Set a work time";
                        }
                        else
                        {
                            m_inputText.text = !Timer.IsOnLongBreak() ? "Set a break time" : "Set a long break time";
                        }

                        // Use subtle text coloring
                        SetTextColor(theme.GetCurrentColorScheme().m_backgroundHighlight);
                    }
                    // Otherwise, using custom user text
                    else
                    {
                        // Use user custom text
                        m_inputText.text = GetUserStateText();

                        // Set color to foreground indicating custom label in-use.
                        SetTextColor(theme.GetCurrentColorScheme().m_foreground);
                    }
                    m_inputText.interactable = true;
                    break;

                case PomodoroTimer.States.RUNNING:
                    if (string.IsNullOrEmpty(GetUserStateText()))
                    {
                        // Only show suffix
                        m_inputText.text = "Running";
                    }
                    m_inputText.interactable = false;
                    // Use subtle text coloring
                    SetTextColor(theme.GetCurrentColorScheme().m_backgroundHighlight);
                    break;

                case PomodoroTimer.States.PAUSED:
                    if (string.IsNullOrEmpty(GetUserStateText()))
                    {
                        // Only show suffix
                        m_inputText.text = "Paused";
                    }
                    m_inputText.interactable = false;
                    // Use subtle text coloring
                    SetTextColor(theme.GetCurrentColorScheme().m_backgroundHighlight);
                    break;

                case PomodoroTimer.States.COMPLETE:
                    break;

                default:
                    Debug.LogWarning("This timer state is not currently supported.");
                    break;
            }
        }

        public override void ColorUpdate(Theme theme)
        {
            if (IsOverridingDefaultStateText())
            {
                // Set color to foreground indicating custom label in-use.
                SetTextColor(theme.GetCurrentColorScheme().m_foreground);
            }
            else
            {
                // Use subtle text coloring
                SetTextColor(theme.GetCurrentColorScheme().m_backgroundHighlight);
            }

            m_inputText.placeholder.color = Timer.GetTheme().GetCurrentColorScheme().m_backgroundHighlight;
        }
    }
}