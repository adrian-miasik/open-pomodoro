using System;
using System.Collections.Generic;
using AdrianMiasik.Components.Base;
using AdrianMiasik.Components.Core.Helpers;
using AdrianMiasik.Components.Core.Items;
using AdrianMiasik.Components.Specific;
using AdrianMiasik.ScriptableObjects;
#if !UNITY_ANDROID && !UNITY_WSA
using Steamworks;
using Steamworks.Data;
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;
using Color = UnityEngine.Color;

namespace AdrianMiasik.Components.Core.Containers
{
    /// <summary>
    /// A <see cref="ThemeElement"/> used primarily for generating and managing our <see cref="DoubleDigit"/>'s and
    /// <see cref="DigitSeparator"/>'s components. Includes support for multiple layouts:
    /// See <see cref="SupportedFormats"/>.
    /// </summary>
    public class DigitFormat : ThemeElement
    {
        /// <summary>
        /// The type any <see cref="DoubleDigit"/> could be.
        /// </summary>
        public enum Digits
        {
            DAYS,
            HOURS,
            MINUTES,
            SECONDS,
            MILLISECONDS
        }

        /// <summary>
        /// The formats we currently support. See digit format doc for details.
        /// </summary>
        public enum SupportedFormats
        {
            DD_HH_MM_SS_MS, // Full
            HH_MM_SS_MS,    // Detail
            HH_MM_SS,       // Default / Standard
            MM_SS,          // Simple
            SS              // Bare
        }

        // Current format
        [SerializeField] private RectTransform m_self; // Height modifier
        [SerializeField] private RectTransform m_digitFormatRect; // Width modifier

        [Header("Source Prefabs")]
        [SerializeField] private DoubleDigit m_digitSource;
        [SerializeField] private DigitSeparator m_separatorSource;
        
        /// <summary>
        /// Represents data for DD_HH_MM_SS_MS
        /// </summary>
        [SerializeField] private int[] m_workTime = {0,0,25,0,0};
        
        /// <summary>
        /// Represents data for DD_HH_MM_SS_MS
        /// </summary>
        [SerializeField] private int[] m_breakTime = {0, 0, 5, 0, 0};
        
        /// <summary>
        /// Represents data for DD_HH_MM_SS_MS
        /// </summary>
        [SerializeField] private int[] m_longBreakTime = {0, 0, 20, 0, 0};
        
        /// <summary>
        /// Is this digit format on break?
        /// <remarks>If `False` then this digit format is in work mode. If `True` this digit format is either
        /// on break / or on a long break.</remarks>
        /// </summary>
        public bool m_isOnBreak;

        /// <summary>
        /// Is this digit format on a long break?
        /// </summary>
        public bool m_isOnLongBreak;

        // Cache
        private SupportedFormats format;
        private List<DoubleDigit> generatedDigits;
        private List<DigitSeparator> generatedSeparators;
        private int previousFormatSelection = -1;

        private void OnValidate()
        {
            // Prevent values from going over their limit
            if (!m_isOnBreak)
            {
                for (int i = 0; i < m_workTime.Length; i++)
                {
                    m_workTime[i] = Mathf.Clamp(m_workTime[i], GetDigitMin(), GetDigitMax((Digits)i));
                }
            }
            else
            {
                for (int i = 0; i < m_breakTime.Length; i++)
                {
                    m_breakTime[i] = Mathf.Clamp(m_breakTime[i], GetDigitMin(), GetDigitMax((Digits)i));
                }
            }
        }

        /// <summary>
        /// Switches then generates our preferred digit format and updating the relevant components using the
        /// current active <see cref="Theme"/>.
        /// </summary>
        /// <param name="pomodoroTimer"></param>
        /// <param name="updateColors"></param>
        public override void Initialize(PomodoroTimer pomodoroTimer, bool updateColors = true)
        {
            base.Initialize(pomodoroTimer, false);
            
            SwitchFormat(format);
            GenerateFormat();
            
            ColorUpdate(Timer.GetTheme());
        }

        /// <summary>
        /// Switches then generates the provided digit format and updates all relevant ThemeElement components.
        /// </summary>
        /// <param name="pomodoroTimer"></param>
        /// <param name="startingFormat"></param>
        /// <param name="updateColors"></param>
        public void Initialize(PomodoroTimer pomodoroTimer, SupportedFormats startingFormat, bool updateColors = true)
        {
            format = startingFormat;
            Initialize(pomodoroTimer, updateColors);
        }

        /// <summary>
        /// Generates/creates our digit format using <see cref="DoubleDigit"/>'s and <see cref="DigitSeparator"/>'s.
        /// See <see cref="SwitchFormat"/> if you'd like to change your preferred <see cref="SupportedFormats"/>, then
        /// invoke this function again to generate your new preferred format.
        /// </summary>
        [ContextMenu("Generate Format")]
        public void GenerateFormat()
        {
            if (generatedDigits != null)
            {
                foreach (DoubleDigit dd in generatedDigits)
                {
                    dd.OnDestroy();
                }
            }

            generatedDigits?.Clear();
            generatedSeparators?.Clear();

            // Clear pre-generated digits / transforms from pre-built scene
            foreach (Transform t in m_digitFormatRect.GetComponentsInChildren<Transform>())
            {
                if (t == m_digitFormatRect.transform)
                {
                    continue;
                }

                Destroy(t.gameObject);
            }

            // Generate our digits and separators (format)
            char[] separatorChar = { '_' };
            GenerateFormat(GetDoubleDigitSet(format, separatorChar[0]));

            // Improve visuals based on number of generated elements
            ImproveLayoutVisuals();

            // Calculate time
            SetTime(GetTime());
                
            // Apply
            RefreshDigitVisuals();

            ColorUpdate(Timer.GetTheme());
        }

        /// <summary>
        /// Updates our generated <see cref="DoubleDigit"/>'s using our cached values and hides our increment and
        /// decrement arrows.
        /// </summary>
        public void RefreshDigitVisuals()
        {
            foreach (DoubleDigit digit in generatedDigits)
            {
                digit.UpdateVisuals(GetDigitValue(digit.m_digit));
                digit.HideArrows();
            }
        }
        
        public void InitializeRuntimeCaret()
        {
            foreach (DoubleDigit digit in generatedDigits)
            {
                digit.InitializeRuntimeCaret();
            }
        }

        // Specific to our anchored layouts
        private void ImproveLayoutVisuals()
        {
            switch (generatedDigits.Count)
            {
                case 1:
                    // Width
                    m_digitFormatRect.anchorMin = new Vector2(0.38f, m_digitFormatRect.anchorMin.y);
                    m_digitFormatRect.anchorMax = new Vector2(0.62f, m_digitFormatRect.anchorMax.y);
                    m_digitFormatRect.anchoredPosition = Vector2.zero;
                    
                    // Height
                    ResetHeight();
                    
                    break;
                
                case 2:
                    // Width
                    m_digitFormatRect.anchorMin = new Vector2(0.225f, m_digitFormatRect.anchorMin.y);
                    m_digitFormatRect.anchorMax = new Vector2(0.775f, m_digitFormatRect.anchorMax.y);
                    m_digitFormatRect.anchoredPosition = Vector2.zero;
                    
                    // Height
                    ResetHeight();
                    
                    break;
                
                case 4:
                    // Width
                    ResetWidth();
                    
                    // Height
                    m_self.anchorMin = new Vector2(m_self.anchorMin.x, 0.4f);
                    m_self.anchorMax = new Vector2(m_self.anchorMax.x, 0.7f);
                    m_self.anchoredPosition = Vector2.zero;
                    
                    break;
                
                case 5:
                    // Width
                    ResetWidth();
                    
                    // Height
                    m_self.anchorMin = new Vector2(m_self.anchorMin.x, 0.425f);
                    m_self.anchorMax = new Vector2(m_self.anchorMax.x, 0.675f);
                    m_self.anchoredPosition = Vector2.zero;
                    
                    break;
                
                default:
                    ResetWidth();
                    ResetHeight();
                    
                    break;
            }
        }

        private void ResetWidth()
        {
            // Reset Width
            m_digitFormatRect.anchorMin = new Vector2(0.05f, m_digitFormatRect.anchorMin.y);
            m_digitFormatRect.anchorMax = new Vector2(0.95f, m_digitFormatRect.anchorMax.y);
            m_digitFormatRect.anchoredPosition = Vector2.zero;
        }

        private void ResetHeight()
        {
            // Reset Height
            m_self.anchorMin = new Vector2(m_self.anchorMin.x, 0.35f);
            m_self.anchorMax = new Vector2(m_self.anchorMax.x, 0.75f);
            m_self.anchoredPosition = Vector2.zero;
        }
        
        /// <summary>
        /// Flips our break boolean from True to False and vice versa.
        /// </summary>
        public void FlipIsOnBreakBool()
        {
            m_isOnBreak = !m_isOnBreak;
        }

        /// <summary>
        /// Returns the users own set times (depending on the state, you could get one of three datasets: work /
        /// break / long break)
        /// </summary>
        /// <returns></returns>
        public TimeSpan GetTime()
        {
            // Work time
            if (!m_isOnBreak)
            {
                return GetTimeFromFormat(m_workTime);
            }

            // Break time
            if (!m_isOnLongBreak)
            {
                return GetTimeFromFormat(m_breakTime);
            }

            // Long break time
            return GetTimeFromFormat(m_longBreakTime);
        }
        
        /// <summary>
        /// Returns a single TimeSpan using the current digit format, while pulling from the provided time list array
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        private TimeSpan GetTimeFromFormat(IReadOnlyList<int> time)
        {
            TimeSpan ts = new TimeSpan();
            
            switch (format)
            {
                case SupportedFormats.DD_HH_MM_SS_MS:
                    ts = ts.Add(TimeSpan.FromDays(time[0]));
                    ts = ts.Add(TimeSpan.FromHours(time[1]));
                    ts = ts.Add(TimeSpan.FromMinutes(time[2]));
                    ts = ts.Add(TimeSpan.FromSeconds(time[3]));
                    ts = ts.Add(TimeSpan.FromMilliseconds(time[4]));
                    break;

                case SupportedFormats.HH_MM_SS_MS:
                    ts = ts.Add(TimeSpan.FromHours(time[1]));
                    ts = ts.Add(TimeSpan.FromMinutes(time[2]));
                    ts = ts.Add(TimeSpan.FromSeconds(time[3]));
                    ts = ts.Add(TimeSpan.FromMilliseconds(time[4]));
                    break;

                case SupportedFormats.HH_MM_SS:
                    ts = ts.Add(TimeSpan.FromHours(time[1]));
                    ts = ts.Add(TimeSpan.FromMinutes(time[2]));
                    ts = ts.Add(TimeSpan.FromSeconds(time[3]));
                    break;

                case SupportedFormats.MM_SS:
                    ts = ts.Add(TimeSpan.FromMinutes(time[2]));
                    ts = ts.Add(TimeSpan.FromSeconds(time[3]));
                    break;

                case SupportedFormats.SS:
                    ts = ts.Add(TimeSpan.FromSeconds(time[3]));
                    break;

                default:
                    Debug.LogWarning("This digit format is not supported. Returning empty TimeSpan.");
                    break;
            }

            // Debug.Log("Returning value for format " + m_format + ": " 
                      // + ts.Days + ":" + ts.Hours + ":" + ts.Minutes + ":" + ts.Seconds + ":" + ts.Milliseconds);
            
            return ts;
        }

        /// <summary>
        /// Returns a list of key value pairs using the provided enum format.
        /// Each key string determines it's type (such as hour/minute/second/etc...) and,
        /// each value int determines it's starting value.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="separator"></param>
        /// <returns></returns>
        private List<KeyValuePair<string, int>> GetDoubleDigitSet(SupportedFormats format, char separator)
        {
            string formatString = format.ToString();
            string currentAbbreviation = String.Empty;

            List<KeyValuePair<string, int>> result = new List<KeyValuePair<string, int>>();

            // Iterate through each character in enum
            for (int i = 0; i < formatString.Length; i++)
            {
                char currentCharacter = formatString[i];

                // If we hit our separator...
                if (currentCharacter == separator)
                {
                    // Cache our abbreviation and value
                    result.Add(new KeyValuePair<string, int>(currentAbbreviation, 0)); // Starting value of zero
                    currentAbbreviation = String.Empty;
                }
                else
                {
                    currentAbbreviation += currentCharacter;

                    // If we reached the end of our list...
                    if (i == formatString.Length - 1)
                    {
                        result.Add(new KeyValuePair<string, int>(currentAbbreviation, 0)); // Starting value of zero
                    }
                }
            }

            // Return abbreviation and value set
            return result;
        }

        /// <summary>
        /// Generates the appropriate amount of digits and separators based on the provided data set
        /// </summary>
        /// <param name="doubleDigitSetToGenerate">String represents abbreviation, and int represents value</param>
        private void GenerateFormat(List<KeyValuePair<string, int>> doubleDigitSetToGenerate)
        {
            generatedDigits = new List<DoubleDigit>();
            generatedSeparators = new List<DigitSeparator>();

            for (int i = 0; i < doubleDigitSetToGenerate.Count; i++)
            {
                KeyValuePair<string, int> pair = doubleDigitSetToGenerate[i];

                // Generate double digit
                DoubleDigit dd = Instantiate(m_digitSource, m_digitFormatRect);
                dd.Initialize(Timer, this, GetDigitType(pair.Key));
                generatedDigits.Add(dd);

                // Track seconds digit changes...(Steam stats)
                if (dd.m_digit == Digits.SECONDS)
                {
                    dd.m_onDigitChange.AddListener(OnSecondsChanged);
                }
                
                // Skip last iteration to avoid spacer generation
                if (i == doubleDigitSetToGenerate.Count - 1)
                {
                    break;
                }

                // Generate spacer (between each character)
                DigitSeparator separator = Instantiate(m_separatorSource, m_digitFormatRect);
                generatedSeparators.Add(separator);
            }

            // Hook up selectable navigation's
            for (int i = 0; i < generatedDigits.Count; i++)
            {
                DoubleDigit digit = generatedDigits[i];

                // Create navigation
                Navigation digitNav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = generatedDigits[CollectionHelper.Wrap(i - 1, generatedDigits.Count)].GetSelectable(),
                    selectOnRight = generatedDigits[CollectionHelper.Wrap(i + 1, generatedDigits.Count)].GetSelectable()
                };

                // Apply navigation
                digit.GetSelectable().navigation = digitNav;
            }

            // Fix background navigation
            Navigation backgroundNav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = generatedDigits[0].GetSelectable(),
                selectOnLeft = generatedDigits[generatedDigits.Count - 1].GetSelectable()
            };
            Timer.SetBackgroundNavigation(backgroundNav);
        }

        private void OnSecondsChanged(int previousValue, int newValue)
        {
            // Ignore second change call when changing from starting value
            if (previousValue == GetTime().Seconds)
            {
                // Debug.Log("Starting second change ignored.");
                return;
            }
            
            // Debug.Log("Second changed from " + previousValue + " to " + newValue);
            
#if !UNITY_ANDROID && !UNITY_WSA
            if (SteamClient.IsValid)
            {
                // Add second to User Stats (User for stats and achievements)
                SteamUserStats.AddStat("seconds_accumulated", 1);
                SteamUserStats.StoreStats();

                // Fetch achievement
                Achievement ach = new Achievement("ACH_ALL_IN_A_DAYS_WORK");

                // If the 'all in a days work' achievement is not unlocked...
                if (!ach.State)
                {
                    // Fetch progression
                    int secondsAccumulated = SteamUserStats.GetStatInt("seconds_accumulated");

                    // Unlock achievement
                    if (secondsAccumulated >= 86400)
                    {
                        ach.Trigger();
                        Debug.Log("Steam Achievement Unlocked! 'All in a Days Work: Run your timer for a " +
                                  "total of 24 hours. (86,400 seconds)'");
                        return;
                    }
                    
                    // Check if seconds passed has reached the hour point... (86400 / 24 = 3600)
                    if (secondsAccumulated != 0 && secondsAccumulated % 3600 == 0)
                    {
                        // Display progress every hour
                        SteamUserStats.IndicateAchievementProgress(ach.Identifier, secondsAccumulated, 86400);
                    }
                }
            }
#endif
        }
        
#if UNITY_EDITOR && !UNITY_ANDROID && !UNITY_WSA
        [MenuItem("Adrian Miasik/Achievements and Statistics/Display 'All in a day's work' progression")]
        private static void DisplayAchievementAllInADaysWorkProgression()
        {
            if (!SteamClient.IsValid)
            {
                Debug.LogWarning("Steam Client not found. Please init Steam Manager by entering play mode" +
                                 " and try again.");
                return;
            }
            
            // Fetch achievement
            Achievement ach = new("ACH_ALL_IN_A_DAYS_WORK");

            // Fetch progression
            int secondsAccumulated = SteamUserStats.GetStatInt("seconds_accumulated");

            // Verify progression...(If you pass in 0 into SteamUserStats.IndicateAchievementProgress method in
            // the current progression argument, Steam will display 'Achievement Unlocked' without unlocking the 
            // achievement. It won't show you a progression tally either. In this case, we'll forgo showing anything
            // related to Steam.
            if (secondsAccumulated <= 0)
            {
                Debug.LogWarning("Unable to display progression. No progression has been made.");
                return;
            }
            
            // Show progress 
            SteamUserStats.IndicateAchievementProgress(ach.Identifier, secondsAccumulated, 86400);
        }
#endif

        /// <summary>
        /// Sets the generated <see cref="DoubleDigit"/>'s text label to the provided <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="ts">The <see cref="TimeSpan"/> you want to display to the user.</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void ShowTime(TimeSpan ts)
        {
            foreach (DoubleDigit doubleDigit in generatedDigits)
            {
                switch (doubleDigit.m_digit)
                {
                    case Digits.DAYS:
                        doubleDigit.SetTextLabel(ts.Days);
                        break;
                    case Digits.HOURS:
                        doubleDigit.SetTextLabel(ts.Hours);
                        break;
                    case Digits.MINUTES:
                        doubleDigit.SetTextLabel(ts.Minutes);
                        break;
                    case Digits.SECONDS:
                        doubleDigit.SetTextLabel(ts.Seconds);
                        break;
                    case Digits.MILLISECONDS:
                        string millisecondsString = Mathf.Abs(ts.Milliseconds).ToString();
                        if (millisecondsString.Length >= 2)
                        {
                            doubleDigit.SetTextLabel(
                                    int.Parse(millisecondsString.Remove(millisecondsString.Length - 1, 1)));
                        }
                        else
                        {
                            doubleDigit.SetTextLabel(ts.Milliseconds);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <summary>
        /// Sets the generated <see cref="DoubleDigit"/>'s to the provided <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="ts"></param>
        public void SetTime(TimeSpan ts)
        {   
            // Apply time arrays to update only for generated digits (essentially throwing out data that's not used)
            foreach (DoubleDigit doubleDigit in generatedDigits)
            {
                Digits type = doubleDigit.m_digit;

                switch (type)
                {
                    case Digits.DAYS:
                        doubleDigit.SetValue(ts.Days);
                        break;
                    case Digits.HOURS:
                        doubleDigit.SetValue(ts.Hours);
                        break;
                    case Digits.MINUTES:
                        doubleDigit.SetValue(ts.Minutes);
                        break;
                    case Digits.SECONDS:
                        doubleDigit.SetValue(ts.Seconds);
                        break;
                    case Digits.MILLISECONDS:
                        doubleDigit.SetValue(ts.Milliseconds);
                        break;
                }
            }
        }

        /// <summary>
        /// Returns the digit type using the provided abbreviation string
        /// </summary>
        /// <param name="abbreviation"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static Digits GetDigitType(string abbreviation)
        {
            switch (abbreviation)
            {
                case "DD":
                    return Digits.DAYS;
                case "HH":
                    return Digits.HOURS;
                case "MM":
                    return Digits.MINUTES;
                case "SS":
                    return Digits.SECONDS;
                case "MS":
                    return Digits.MILLISECONDS;
            }

            throw new Exception();
        }

        private int GetDigitMin()
        {
            return 0;
        }

        private int GetDigitMax(Digits digits)
        {
            // TODO: You'll need to change our max values depending on the current format we have
            switch (digits)
            {
                case Digits.DAYS:
                    return 99;
                case Digits.HOURS:
                    return 24;
                case Digits.MINUTES:
                    return 59;
                case Digits.SECONDS:
                    return 59;
                case Digits.MILLISECONDS:
                    return 10; // TODO: Should be a thousand but we are limited to double 
                default:
                    return 99;
            }
        }

        /// <summary>
        /// Enables this gameobject.
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Disables this gameobject.
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Applies our <see cref="Theme"/> changes to our components when necessary.
        /// </summary>
        /// <param name="theme"></param>
        public override void ColorUpdate(Theme theme)
        {
            // Digits
            SetDigitColor(theme.GetCurrentColorScheme().m_foreground);

            // Separators
            foreach (DigitSeparator separator in generatedSeparators)
            {
                separator.SetSeparatorColor(theme.GetCurrentColorScheme().m_foreground);
            }
        }

        /// <summary>
        /// Sets the color of our generated <see cref="DoubleDigit"/>'s to the provided color.
        /// </summary>
        /// <param name="newColor">The color you want to set all our <see cref="DoubleDigit"/>'s to.</param>
        public void SetDigitColor(Color newColor)
        {
            foreach (DoubleDigit digit in generatedDigits)
            {
                digit.SetTextColor(newColor);
            }
        }

        /// <summary>
        /// Allows our generated <see cref="DoubleDigit"/>'s to be interacted with.
        /// </summary>
        public void Unlock()
        {
            foreach (DoubleDigit digit in generatedDigits)
            {
                digit.Unlock();
            }
        }

        /// <summary>
        /// Prevents our generated <see cref="DoubleDigit"/>'s to be interacted with.
        /// </summary>
        public void Lock()
        {
            foreach (DoubleDigit digit in generatedDigits)
            {
                digit.Lock();
            }
        }

        /// <summary>
        /// Moves all our generated <see cref="DoubleDigit"/>'s into their default viewport positions and anchors.
        /// </summary>
        public void ResetTextPositions()
        {
            foreach (DoubleDigit digit in generatedDigits)
            {
                digit.ResetTextPosition();
            }
        }

        /// <summary>
        /// Returns our list of generated <see cref="DoubleDigit"/>'s
        /// </summary>
        /// <returns></returns>
        public List<DoubleDigit> GetDigits()
        {
            return generatedDigits;
        }
        
        /// <summary>
        /// Returns our current timer values in a <see cref="String"/>.
        /// <example>Such as "00:24:35" (without the quotation marks)</example>
        /// </summary>
        /// <param name="richPresenceFormat">If true, we will return a simplified but labeled format such as: "23m 50s"</param>
        /// <returns>Our current timer value.</returns>
        public string GetTimerString(bool richPresenceFormat = false)
        {
            string result = String.Empty;

            for (int i = 0; i < generatedDigits.Count; i++)
            {
                DoubleDigit digit = generatedDigits[i];

                if (richPresenceFormat)
                {
                    if (digit.GetDigitsLabel() == "00")
                    {
                        // Skip/Don't print number
                        continue;
                    }
                    // Otherwise...

                    // Print number
                    result += digit.GetDigitsLabel();
                    
                    // Print suffix
                    switch (digit.m_digit)
                    {
                        case Digits.DAYS:
                            result += "d";
                            break;
                        case Digits.HOURS:
                            result += "h";
                            break;
                        case Digits.MINUTES:
                            result += "m";
                            break;
                        case Digits.SECONDS:
                            result += "s";
                            break;
                        case Digits.MILLISECONDS:
                            result += "ms";
                            break;
                        default:
                            Debug.LogWarning("Unsupported enum type.");
                            break;
                    }

                    if (i != generatedDigits.Count - 1) // last digit
                    {
                        // Add padding between "sets" such as: '23m' and '56s'.
                        result += " ";
                    }
                }
                else
                {
                    result += digit.GetDigitsLabel();
                }

                // Skip last iteration
                if (i == generatedDigits.Count - 1)
                {
                    break;
                }

                if (!richPresenceFormat)
                {
                    result += ":";
                }
            }

            return result;
        }

        /// <summary>
        /// Prevents all our digits tick animation from holding, especially useful for when the gameobjects are no
        /// longer active and need to be re-enabled (Such as switching between pages).
        /// </summary>
        [ContextMenu("Correct Tick Animation")]
        public void CorrectTickAnimVisuals()
        {
            foreach (DoubleDigit digit in generatedDigits)
            {
                digit.HideTickAnimation();
                digit.ResetTextPosition();
            }
        }

        /// <summary>
        /// Increments the provided digit by one. (+1)
        /// </summary>
        /// <param name="digits"></param>
        public void IncrementOne(Digits digits)
        {
            SetDigit(digits, GetDigitValue(digits) + 1);
        }

        /// <summary>
        /// Decrements the provided digit by one. (-1)
        /// </summary>
        /// <param name="digits"></param>
        public void DecrementOne(Digits digits)
        {
            SetDigit(digits, GetDigitValue(digits) - 1);
        }

        /// <summary>
        /// Sets the provided digit to it's provided new value.
        /// </summary>
        /// <param name="digit"></param>
        /// <param name="newValue"></param>
        public void SetDigit(Digits digit, int newValue)
        {
            if (!m_isOnBreak)
            {
                m_workTime[(int)digit] = newValue;
            }
            else
            {
                if (!m_isOnLongBreak)
                {
                    m_breakTime[(int)digit] = newValue;
                }
                else
                {
                    m_longBreakTime[(int) digit] = newValue;
                }
            }

            OnValidate();
        }

        /// <summary>
        /// Sets the value of the timer using the provided formatted string.
        /// </summary>
        /// <param name="formattedString">Expected format of "00:25:00" (Without the quotation marks).</param>
        public void SetTimerValue(string formattedString)
        {
            List<string> sections = new List<string>();
            string value = String.Empty;
            
            // Iterate through each character to determine how many sections there are...
            foreach (char c in formattedString)
            {
                // If this character is a separator...
                if (c.ToString() == ":")
                {
                    // Save section
                    if (value != String.Empty)
                    {
                        sections.Add(value);
                        value = string.Empty;
                    }

                    continue;
                }

                // Add to section
                value += c.ToString();
            }

            // Last digit in string won't have a separator so we add the section in once the loop is complete
            sections.Add(value);

            // Compare sections with timer format
            if (sections.Count != generatedDigits.Count)
            {
                Debug.LogWarning("The provided string does not match the pomodoro timer layout exactly. " +
                                 "Truncation may occur.");
            }

            // Set timer sections
            // Check if we have enough generated digits...(We will allow users to paste longer values, but only
            // carry over the values that can fit within the number of generated digits we have)
            if (sections.Count >= generatedDigits.Count)
            {
                for (int i = 0; i < generatedDigits.Count; i++)
                {
                    generatedDigits[i].SetValue(int.Parse(sections[i]));
                    generatedDigits[i].UpdateVisuals(GetDigitValue(generatedDigits[i].m_digit));
                }
            }
        }

        /// <summary>
        /// Returns the provided <see cref="Digits"/> value.
        /// </summary>
        /// <param name="digit"></param>
        /// <returns></returns>
        public int GetDigitValue(Digits digit)
        {
            if (!m_isOnBreak)
            {
                return m_workTime[(int)digit];
            }

            if (!m_isOnLongBreak)
            {
                return m_breakTime[(int)digit];
            }

            return m_longBreakTime[(int) digit];
        }

        /// <summary>
        /// Returns `True` if you can add one to this digit without hitting it's ceiling, otherwise returns `False`.
        /// </summary>
        /// <param name="digit">The <see cref="Digits"/> you want to check if it can be decremented by one.</param>
        /// <returns></returns>
        public bool CanIncrementOne(Digits digit)
        {
            if (GetDigitValue(digit) + 1 > GetDigitMax(digit))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns `True` if you can subtract one from this digit without hitting it's floor, otherwise returns
        /// `False`.
        /// </summary>
        /// <param name="digit">The <see cref="Digits"/> you want to check if it can be decremented by one.</param>
        /// <returns></returns>
        public bool CanDecrementOne(Digits digit)
        {
            if (GetDigitValue(digit) - 1 < GetDigitMin())
            {
                return false;
            }

            return true;
        }

        
        /// <summary>
        /// Clears our digit selection and sets the selection to the <see cref="Background"/> (our default).
        /// </summary>
        public void ClearTimerSelection()
        {
            Timer.ClearSelection();
        }

        /// <summary>
        /// Returns <see cref="PomodoroTimer"/>'s current state. (See: <seealso cref="PomodoroTimer.States"/>)
        /// </summary>
        /// <returns></returns>
        public PomodoroTimer.States GetTimerState()
        {
            return Timer.m_state;
        }

        /// <summary>
        /// Sets the <see cref="PomodoroTimer"/>'s selection to the provided <see cref="DoubleDigit"/>.
        /// </summary>
        /// <param name="digitToSelect"></param>
        public void SetTimerSelection(DoubleDigit digitToSelect)
        {
            Timer.SetSelection(digitToSelect);
        }

        /// <summary>
        /// Switches your format to the provided format.
        /// </summary>
        /// <param name="desiredFormat"></param>
        public void SwitchFormat(SupportedFormats desiredFormat)
        {
            previousFormatSelection = (int) format;
            format = desiredFormat;
        }

        /// <summary>
        /// Returns the previously selected format index
        /// </summary>
        /// <returns></returns>
        public int GetPreviousFormatSelection()
        {
            return previousFormatSelection;
        }
        
        /// <summary>
        /// Return the current format index
        /// </summary>
        /// <returns></returns>
        public int GetFormatIndex()
        {
            return (int) format;
        }
        
        /// <summary>
        /// Sets this digit format to be using the long break dataset. (work / break / long break)
        /// </summary>
        public void ActivateLongBreak()
        {
            m_isOnLongBreak = true;
        }

        /// <summary>
        /// Sets this digit to be using the break dataset. (work / break / long break)
        /// </summary>
        public void DeactivateLongBreak()
        {
            m_isOnLongBreak = false;
        }

        public void ShowTickAnimation()
        {
            foreach (DoubleDigit digit in generatedDigits)
            {
                digit.ShowTickAnimation();
            }
        }
    }
 }