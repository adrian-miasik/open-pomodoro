// Note: Some namespaces are platform specific even though it may seem like they are not being used.
// Change your Unity Build Settings Platform to see which namespaces are used for which platform.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdrianMiasik.Components.Core;
using AdrianMiasik.Components.Core.Containers;
using AdrianMiasik.Components.Core.Items;
using AdrianMiasik.Components.Core.Items.Pages;
using AdrianMiasik.Components.Core.Settings;
using AdrianMiasik.Components.Specific;
using AdrianMiasik.Components.Specific.Platforms.Android;
using AdrianMiasik.Components.Specific.Platforms.Steam;
using AdrianMiasik.Components.Specific.Platforms.UWP;
using AdrianMiasik.Interfaces;
using AdrianMiasik.ScriptableObjects;
using Steamworks;
using Steamworks.Data;
using LeTai.Asset.TranslucentImage;
using QFSW.QC;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.Events;
using UnityEngine.UI;
using Color = UnityEngine.Color;
using Image = UnityEngine.UI.Image;

namespace AdrianMiasik
{
    /// <summary>
    /// Our main class / component. Responsible for controlling the main timer logic, configuring settings,
    /// initializing, and manipulating our components.
    /// </summary>
    public class PomodoroTimer : MonoBehaviour, IColorHook
    {
        /// <summary>
        /// The different modes the timer could be using.
        /// </summary>
        public enum States
        {
            /// <summary>
            /// Digits are editable by the user, time does not progress.
            /// </summary>
            SETUP,
            /// <summary>
            /// Digits are not allowed to be interacted with, and the time left is decreasing over time.
            /// </summary>
            RUNNING,
            /// <summary>
            /// Digits are not allowed to be interacted with, time does not progress.
            /// </summary>
            PAUSED,
            /// <summary>
            /// Digits are no longer visible, completion label is visible.
            /// </summary>
            COMPLETE
        }
        
        /// <summary>
        /// The timer's current state. See enum <see cref="States"/>
        /// </summary>
        public States m_state = States.SETUP;

        [Header("Unity Pomodoro - Managers")]
        [SerializeField] private QuantumConsole m_console;
        [SerializeField] private ThemeManager m_themeManager;
        [SerializeField] private HotkeyDetector m_hotkeyDetector;
        [SerializeField] private ResolutionDetector m_resolutionDetector;
        [SerializeField] private ConfirmationDialogManager m_confirmationDialogManager;

        [Header("Unity Pomodoro - Platform Specific Managers")]
        [SerializeField] private SteamManager m_steamManager; // Disable on Android and WSA platforms
        // ReSharper disable once NotAccessedField.Local
        [SerializeField] private UWPNotifications m_uwpNotifications; // Only for the UWP platform
        // ReSharper disable once NotAccessedField.Local
        [SerializeField] private AndroidNotifications m_androidNotifications; // Only for the Android platform

        [Header("Unity - Basic Components")]
        [SerializeField] private Image m_ring; // Ring used to display timer progress
        [SerializeField] private Image m_ringBackground; // Theming
        [SerializeField] private AudioSource m_alarmSource;

        [Header("Unity Pomodoro - Custom Components")]
        [SerializeField] private Background m_background;
        [SerializeField] private StateIndicator m_stateIndicator;
        [SerializeField] private InputLabelText m_labelText; // Text used to display label + current state
        [SerializeField] private BlurOverlay m_overlay;
        [SerializeField] private TranslucentImageSource m_translucentImageSource; // Necessary reference for blur
        [SerializeField] private CompletionLabel m_completionLabel;
        [SerializeField] private DigitFormat m_digitFormat;
        [SerializeField] private ToggleSprite m_menuToggleSprite; // Used to toggle our sidebar menu
        [SerializeField] private ClickButtonSVGIcon m_leftButtonSVGClick; // Used to restart the timer
        [SerializeField] private RightButton m_rightButton; // Play/Pause/Snooze/Work 
        [SerializeField] private ToggleSlider m_breakSlider; // Used for switching timer between work and break mode
        [SerializeField] private CreditsGhost m_creditsGhost; // Used to display project contributors
        [SerializeField] private ThemeSlider m_themeSlider;
        [SerializeField] private Sidebar m_sidebarMenu;
        [SerializeField] private TomatoCounter m_tomatoCounter;
        [SerializeField] private EndTimestampGhost m_endTimestampGhost;
        [SerializeField] private SkipButton m_skipButton;
        
        private readonly List<ITimerState> timerElements = new(); // Elements that react to timer state changes.
        
        [Header("Unity Pomodoro - Animations")] 
        [SerializeField] private AnimationCurve m_spawnRingProgress;
        private bool animateRingProgress;
        private float accumulatedRingAnimationTime;
        [SerializeField] private Animation m_spawnAnimation; // The timers introduction animation (plays on timer restarts)
        [SerializeField] private AnimationCurve m_completeRingPulseDiameter = AnimationCurve.Linear(0, 0.9f, 1, 0.975f);
        [SerializeField] private float m_delayBetweenRingPulses = 0.5f;
        // Pulse Ring Complete Animation
        private bool disableCompletionAnimation;
        private float accumulatedRingPulseTime;
        private bool hasRingPulseBeenInvoked;

        [SerializeField] private float m_pauseFadeDuration = 0.1f;
        [SerializeField] private float m_pauseHoldDuration = 0.75f; // How long to wait between fade completions?

        /// <summary>
        /// A UnityEvent that gets invoked when the spawn animation is complete.
        /// </summary>
        [Header("Unity Events")]
        public UnityEvent m_onSpawnCompletion;
        
        /// <summary>
        /// A UnityEvent that gets invoked when the ring / timer alarm pulses.
        /// </summary>
        public UnityEvent m_onRingPulse;
        
        /// <summary>
        /// A UnityEvent that gets invoked when the timer finishes. (<see cref="States.COMPLETE"/>)
        /// </summary>
        public UnityEvent m_onTimerCompletion;

        [Header("Cache")]
        [SerializeField] private List<DoubleDigit> m_selectedDigits = new();

        [Header("Pages")] 
        [SerializeField] private SidebarPages m_sidebarPages;

        // Time
        private double currentTime; // Current time left (In seconds)
        private float totalTime; // Total time (In seconds)
        private bool isTimerBeingSetup = true; // First time playing
#if UNITY_ANDROID
        private TimeSpan focusLossTime;
#endif

        // Pause Fade Animation
        private bool isFading;
        private float accumulatedFadeTime;
        private float fadeProgress;
        private Color startingColor;
        private Color endingColor;
        private bool isFadeComplete;

        // Shader Properties
        private static readonly int RingColor = Shader.PropertyToID("Color_297012532bf444df807f8743bdb7e4fd");
        private static readonly int RingDiameter = Shader.PropertyToID("Vector1_98525729712540259c19ac6e37e93b62");
        public static readonly int CircleColor = Shader.PropertyToID("Color_297012532bf444df807f8743bdb7e4fd");

        [Header("Loaded Settings")]
        [SerializeField] private SystemSettings m_loadedSystemSettings;
        [SerializeField] private TimerSettings m_loadedTimerSettings;

        private bool haveSettingsBeenConfigured;
        private bool haveComponentsBeenInitialized;
        private bool isInitialized;

        private List<string> cachedCustomAudioFiles;

        /// <summary>
        /// Mutes our volume when out of focus if permitted by user system settings.
        /// Also lowers target frame rate when not in focus.
        /// Also calculates new time when application is focused again on Android.
        /// </summary>
        /// <param name="hasFocus"></param>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (m_loadedSystemSettings.m_muteSoundWhenOutOfFocus)
            {
                // Prevent application from making noise when not in focus
                AudioListener.volume = !hasFocus ? 0 : 1;
            }
            else
            {
                AudioListener.volume = 1;
            }
            
            if (!hasFocus)
            {
#if UNITY_ANDROID
                // Cache the time we lost focus.
                focusLossTime = DateTime.Now.TimeOfDay;
#endif
                
                Application.targetFrameRate = 15;
            }
            else
            {
#if UNITY_ANDROID
                // Calculate the new time progress based on our focus loss time.
                double secondsPassedSinceFocusLoss = DateTime.Now.TimeOfDay.TotalSeconds - focusLossTime.TotalSeconds;
                currentTime -= secondsPassedSinceFocusLoss;
#endif                

                Application.targetFrameRate = Screen.currentResolution.refreshRate;

                // Prevent this from being invoked until app is fully init.
                if (isInitialized)
                {
                    StartCoroutine(InitializeStreamingAssets());
                }
            }
        }

        private void OnValidate()
        {
            if (!haveSettingsBeenConfigured)
            {
                return;
            }
            
            // Updating settings visuals
            m_themeSlider.Refresh();
            m_sidebarPages.RefreshSettingsPage();
        }

        void Awake()
        {
            // Single entry point
            ConfigureSettings();
            StartCoroutine(Initialize());
        }

        /// <summary>
        /// Loads / Creates settings file for the system and timer.
        /// </summary>
        private void ConfigureSettings()
        {
#if !UNITY_ANDROID && !UNITY_WSA
            // Steam manager has to be loaded prior to the other managers since settings could be saved in
            // (then loaded from) Cloud Save.
            m_steamManager.Initialize(this);
#endif
            
            SystemSettings systemSettings = UserSettingsSerializer.LoadSettings<SystemSettings>("system-settings");

            // System Settings
            if (systemSettings == null)
            {
                Debug.LogWarning("A new SYSTEM settings file has been created successfully!");
                
                // Create new settings
                SystemSettings defaultSystemSettings = new SystemSettings();
                defaultSystemSettings.m_darkMode = false;

                // Apply mute out of focus
#if UNITY_STANDALONE_OSX
                defaultSystemSettings.m_muteSoundWhenOutOfFocus = false;
#elif UNITY_STANDALONE_LINUX
                defaultSystemSettings.m_muteSoundWhenOutOfFocus = false;
#elif UNITY_STANDALONE_WIN
                defaultSystemSettings.m_muteSoundWhenOutOfFocus = false;
#elif UNITY_WSA // UWP
                defaultSystemSettings.m_muteSoundWhenOutOfFocus = true; // Set to true since our UWP Notification will pull focus back to our app
#elif UNITY_ANDROID
                defaultSystemSettings.m_muteSoundWhenOutOfFocus = false; // Doesn't quite matter for mobile
#elif UNITY_IOS
                defaultSystemSettings.m_muteSoundWhenOutOfFocus = false; // Doesn't quite matter for mobile.
#endif
                
                // All platforms have analytics on by default. (User can opt-out via the SettingsPage)
                defaultSystemSettings.m_enableUnityAnalytics = true;

                // Steam rich presence enabled by default on platforms that support it.
                // (User can opt-out via the SettingsPage)
#if !UNITY_ANDROID && !UNITY_WSA
                if (m_steamManager.IsInitialized())
                {
                    defaultSystemSettings.m_enableSteamRichPresence = true;
                }
                else
                {
                    defaultSystemSettings.m_enableSteamRichPresence = false;
                }
#else
                defaultSystemSettings.m_enableSteamRichPresence = false;
#endif

                // Cache
                systemSettings = defaultSystemSettings;
                UserSettingsSerializer.SaveSettingsFile(systemSettings, "system-settings");
            }

            m_loadedSystemSettings = systemSettings;

            // Apply theme changes
            m_themeManager.Register(this);
            if (m_loadedSystemSettings.m_darkMode)
            {
                m_themeManager.SetToDarkMode();
            }
            else
            {
                m_themeManager.SetToLightMode();
            }
            
            TimerSettings timerSettings = UserSettingsSerializer.LoadSettings<TimerSettings>("timer-settings");
            
            // Timer Settings
            // If we don't have any saved settings...
            if (timerSettings == null)
            {
                Debug.LogWarning("A new TIMER settings file has been created successfully!");
                
                // Create new settings
                TimerSettings defaultTimerSettings = new()
                {
                    m_longBreaks = true,
                    m_format = DigitFormat.SupportedFormats.HH_MM_SS,
                    m_pomodoroCount = 4,
                    m_alarmSoundIndex = 0
                };

                // Cache
                timerSettings = defaultTimerSettings;
                UserSettingsSerializer.SaveSettingsFile(timerSettings, "timer-settings");
            }
            
            m_loadedTimerSettings = timerSettings;

            // Set target frame rate to refresh rate to prevent possible unnecessary GPU usage.
            Application.targetFrameRate = Screen.currentResolution.refreshRate;
            
            haveSettingsBeenConfigured = true;

#if ENABLE_CLOUD_SERVICES_ANALYTICS
            // Update analytics
            ToggleUnityAnalytics(m_loadedSystemSettings.m_enableUnityAnalytics, true);
#endif
        }

#if ENABLE_CLOUD_SERVICES_ANALYTICS
        /// <summary>
        /// Enables / Disables our Unity Analytics Service and setting option.
        /// Note: To fully disable analytics, we need to set initializeOnStartup to false and restart our app.
        /// There seems to be no way to "turn off" the analytics service once it's been initialized and turned on.
        /// The disable analytics code does not return our intended values, hence the hard restart required.
        /// </summary>
        /// <param name="enableUnityAnalytics"></param>
        /// <param name="isBootingUp"></param>
        public void ToggleUnityAnalytics(bool enableUnityAnalytics, bool isBootingUp)
        {
            // If there is a change in settings...
            if (enableUnityAnalytics != m_loadedSystemSettings.m_enableUnityAnalytics)
            {
                // Apply and save
                m_loadedSystemSettings.m_enableUnityAnalytics = enableUnityAnalytics;
                UserSettingsSerializer.SaveSettingsFile(m_loadedSystemSettings, "system-settings");
            }

            // Set if service needs to start on init...
            Analytics.initializeOnStartup = enableUnityAnalytics;
#if UNITY_ANALYTICS_EVENT_LOGS
            Debug.LogWarning("Unity Analytics - Initialize on startup: " + Analytics.initializeOnStartup);
#endif

            if (enableUnityAnalytics)
            {
                // Enable analytics
                StartAnalyticsService(isBootingUp);
            }
            else if(!isBootingUp)
            {
                // Send disabled event log
                Dictionary<string, object> parameters = new Dictionary<string, object>()
                {
                    { "testingKey", "testingValue1234Disabled" },
                };
                AnalyticsService.Instance.CustomData("analyticsServiceDisabled", parameters);
                AnalyticsService.Instance.Flush();

                // Disable analytics
                Analytics.enabled = false;
                PerformanceReporting.enabled = false;
                Analytics.limitUserTracking = true;
                Analytics.deviceStatsEnabled = false;
                AnalyticsService.Instance.SetAnalyticsEnabled(false);

#if UNITY_ANALYTICS_EVENT_LOGS
                Debug.LogWarning("Unity Analytics - Stopped Service.");
#endif
            }
        }
        
        /// <summary>
        /// Starts up our Unity Analytics service.
        /// </summary>
        /// <param name="isBootingUp"></param>
        async void StartAnalyticsService(bool isBootingUp)
        {
            try
            {
                await UnityServices.InitializeAsync();

#if UNITY_ANALYTICS_EVENT_LOGS
                Debug.LogWarning("Unity Analytics - Service Started.");
#endif
                
                // Enable analytics
                Analytics.enabled = true;
                PerformanceReporting.enabled = true;
                Analytics.limitUserTracking = false;
                Analytics.deviceStatsEnabled = true;
                await AnalyticsService.Instance.SetAnalyticsEnabled(true);

                if (isBootingUp)
                {
                    // Send enabled event log
                    Dictionary<string, object> parameters = new Dictionary<string, object>()
                    {
                        { "testingKey", "testingValue1234Init" },
                    };
                    AnalyticsService.Instance.CustomData("analyticsServiceInitialized", parameters);
                    AnalyticsService.Instance.Flush();
                }
                else
                {
                    // Send enabled event log
                    Dictionary<string, object> parameters = new Dictionary<string, object>()
                    {
                        { "testingKey", "testingValue1234Enabled" },
                    };
                    AnalyticsService.Instance.CustomData("analyticsServiceEnabled", parameters);
                    AnalyticsService.Instance.Flush();
                }
            }
            catch (ConsentCheckException)
            {
               
            }
        }
#endif

        /// <summary>
        /// Initialize managers, set component overrides, initialize components, fetch streaming assets if applicable,
        /// then setup view, and transition/animate in.
        /// </summary>
        private IEnumerator Initialize()
        {
            InitializeManagers();

            // Overrides
            m_breakSlider.OverrideFalseColor(m_themeManager.GetTheme().GetCurrentColorScheme().m_modeOne);
            m_breakSlider.OverrideTrueColor(m_themeManager.GetTheme().GetCurrentColorScheme().m_modeTwo);
            m_themeSlider.OverrideFalseColor(m_themeManager.GetTheme().m_light.m_backgroundHighlight);
            m_themeSlider.OverrideTrueColor(new Color(0.59f, 0.33f, 1f));
            m_menuToggleSprite.OverrideFalseColor(m_themeManager.GetTheme().GetCurrentColorScheme().m_foreground);
            m_menuToggleSprite.OverrideTrueColor(Color.clear);

            InitializeComponents();

#if !UNITY_ANDROID
            yield return InitializeStreamingAssets();
#else
            yield return new WaitForEndOfFrame();
#endif

            // Switch view
            m_sidebarPages.SwitchToTimerPage();

            // Calculate time
            CalculateTimeValues();

            // Transition to setup state
            SwitchState(States.SETUP);

            // Animate in
            PlaySpawnAnimation();

            isInitialized = true;
        }

        private IEnumerator InitializeStreamingAssets()
        {
            Debug.Log("Validating 'StreamingAssets'...");
            yield return ValidateStreamingAssets();
        }

        /// <summary>
        /// Initializes our manager classes.
        /// </summary>
        private void InitializeManagers()
        {
            // General Managers
            if (m_console)
            {
                // Subscribe to console event callbacks
                m_console.OnActivate += OnConsoleOpen;
                m_console.OnDeactivate += OnConsoleClose;
            }
            m_hotkeyDetector.Initialize(this);
            m_resolutionDetector.Initialize(this);
            m_confirmationDialogManager.Initialize(this);
            
            // Platform Managers
#if !UNITY_ANDROID && !UNITY_WSA
            // Steam Platforms
            m_steamManager.InitializeSteamModules();
#endif

#if UNITY_WSA
            // UWP Toast / Notification
            m_uwpNotifications.Initialize(GetSystemSettings());
            m_onTimerCompletion.AddListener(m_uwpNotifications.ShowToast);
#endif
            
#if UNITY_ANDROID
            // Android Notification
            m_androidNotifications.Initialize(this);

            // Register elements that need updating per timer state change
            SubscribeToTimerStates(m_androidNotifications);
#endif
        }

        /// <summary>
        /// Invoked when the console has been opened and is seen.
        /// </summary>
        private void OnConsoleOpen()
        {
            m_hotkeyDetector.PauseInputs();
        }

        /// <summary>
        /// Invoked when the console has been closed and is no longer visible.
        /// </summary>
        private void OnConsoleClose()
        {
            m_hotkeyDetector.ResumeInputs();
        }

        // TODO: Support Android Platform?
        private IEnumerator ValidateStreamingAssets()
        {
            // Fetch directory
            DirectoryInfo directoryInfo = new(Application.streamingAssetsPath);

            // Fetch files: .wav's + .mp3's and order the file paths based on creation time/date.
            FileInfo[] files = directoryInfo.GetFiles("*.wav").Concat
                                (directoryInfo.GetFiles("*.mp3")).
                                    OrderBy(f => f.CreationTime).ToArray();

            List<string> allFiles = new List<string>();
            List<string> newCustomFiles = new List<string>();

            // Iterate through every found file...
            foreach (FileInfo file in files)
            {
                // Cache every file
                allFiles.Add(file.FullName);

                // Ignore file if it has been previously cached.
                if (cachedCustomAudioFiles != null && cachedCustomAudioFiles.Contains(file.FullName))
                {
                    Debug.Log("Custom '" + file.Name + "' has already been added. Skipping...");
                    continue;
                }

                // Cache files that are new.
                newCustomFiles.Add(file.FullName);

                // Log
                Debug.Log("Custom '" + file.Name + "' audio found.");
            }

            List<string> removedCustomFiles = new List<string>();

            // If something has been previously cached before...(meaning if we have added custom sounds before...)
            if (cachedCustomAudioFiles != null)
            {
                // Iterate through our cache...
                foreach (string audioFile in cachedCustomAudioFiles)
                {
                    // If we find the cached file currently present...
                    if (allFiles.Contains(audioFile))
                    {
                        // Ignore.
                        continue;
                    }

                    // Otherwise, the cached file is no longer present.
                    Debug.Log(Path.GetFileName(audioFile) + " has been removed.");
                    removedCustomFiles.Add(audioFile);

                    m_sidebarPages.RemoveCustomAudioFile(audioFile);
                }
            }

            // Add to sound bank dictionary...
            yield return m_sidebarPages.AddCustomSoundFiles(newCustomFiles);

            // Validate only when we are removing or adding custom audio...
            // if (removedCustomFiles.Count > 0 || newCustomFiles.Count > 0)
            // {
                m_sidebarPages.ValidateCustomSoundChoice();
            // }

            // Cache audio files (for future cross reference...against checking removal of files)
            cachedCustomAudioFiles = new List<string>(allFiles);

            // Dispose
            allFiles.Clear();
            newCustomFiles.Clear();
            removedCustomFiles.Clear();
        }

        /// <summary>
        /// Hooks up our button functionality, initializes our pomodoro timer components, and registers our
        /// ITimerState interfaced elements.
        /// </summary>
        private void InitializeComponents()
        {
            // Restart Button
            m_leftButtonSVGClick.m_onClick.AddListener(() =>
            {
                TryRestart(false);
            });

            // Play / Pause Button
            m_rightButton.m_playOnClick.AddListener(Play);
            m_rightButton.m_pauseOnClick.AddListener(Pause);
            m_rightButton.m_snoozeOnClick.AddListener(() =>
            {
                Restart(true);
                m_breakSlider.Press();
            });

            // Switch Timer / Mode Slider
            m_breakSlider.m_onSetToTrueClick.AddListener(TrySwitchToBreakTimer);
            m_breakSlider.m_onSetToFalseClick.AddListener(TrySwitchToWorkTimer);
            
            // Sidebar Menu Toggle
            m_menuToggleSprite.m_onSetToTrueClick.AddListener(m_sidebarMenu.Open);
            m_menuToggleSprite.m_onSetToFalseClick.AddListener(m_sidebarMenu.Close);
            
            // Components
            m_background.Initialize(this);
            m_stateIndicator.Initialize(this);
            m_labelText.Initialize(this);
            m_overlay.Initialize(this);
            m_digitFormat.Initialize(this, GetTimerSettings().m_format);
            m_completionLabel.Initialize(this);
            m_themeSlider.Initialize(this);
            m_creditsGhost.Initialize(this);
            m_rightButton.Initialize(this);
            m_menuToggleSprite.Initialize(this, false);
            m_breakSlider.Initialize(this, false);
            m_sidebarMenu.Initialize(this, m_resolutionDetector);
            m_endTimestampGhost.Initialize(this);

            m_skipButton.Initialize(this);
            if (GetTimerSettings().m_longBreaks)
            {
                m_tomatoCounter.Initialize(this, GetTimerSettings().m_pomodoroCount);
                m_completionLabel.MoveAnchors(true);
            }
            else
            {
                m_tomatoCounter.gameObject.SetActive(false);
                m_completionLabel.MoveAnchors(false);
            }
            
            m_sidebarPages.Initialize(this);

            // Register elements that need updating per timer state change
            SubscribeToTimerStates(m_labelText);
            SubscribeToTimerStates(m_stateIndicator);
            SubscribeToTimerStates(m_rightButton);
            SubscribeToTimerStates(m_completionLabel);
            SubscribeToTimerStates(m_endTimestampGhost);
            SubscribeToTimerStates(m_skipButton);

            haveComponentsBeenInitialized = true;
        }
        
        /// <summary>
        /// Registers the provided interface to be invoked when this pomodoro timer's state changes.
        /// </summary>
        /// <param name="timerState"></param>
        public void SubscribeToTimerStates(ITimerState timerState)
        {
            timerElements.Add(timerState);
        }

        public bool HaveComponentsBeenInitialized()
        {
            return haveComponentsBeenInitialized;
        }
        
        /// <summary>
        /// Unity's OnDestroy(). Deregisters self from <see cref="Theme"/> on destruction.
        /// </summary>
        public void OnDestroy()
        {
            // Make sure to deregister this when and if we do destroy the timer
            m_themeManager.GetTheme().Deregister(this);
        }
        
        /// <summary>
        /// Switches the timer to the provided state and handles all visual changes.
        /// Basically handles our transitions between timer states. <see cref="PomodoroTimer.States"/>
        /// </summary>
        /// <param name="desiredState">The state you want to transition to</param>
        public void SwitchState(States desiredState)
        {
            m_state = desiredState;
            Theme theme = m_themeManager.GetTheme();

            // Workaround...
            m_labelText.gameObject.SetActive(true);

            // Update the registered timer elements
            foreach (ITimerState element in timerElements)
            {
                element.StateUpdate(m_state, theme);
            }
            
            UpdateRing(theme);

            // Do transition logic
            switch (m_state)
            {
                case States.SETUP:
                    m_digitFormat.SetDigitColor(theme.GetCurrentColorScheme().m_foreground);
                    
                    // Show timer context
                    m_labelText.gameObject.SetActive(true);
                    // m_labelText.ClearSuffix();

                    // if (!m_digitFormat.m_isOnBreak)
                    // {
                    //     m_labelText.text = "Set a work time";
                    // }
                    // else
                    // {
                    //     m_labelText.text = !IsOnLongBreak() ? "Set a break time" : "Set a long break time";
                    // }

                    // Show digits and hide completion label
                    m_digitFormat.Show();

                    isFading = false;
                    accumulatedRingPulseTime = 0;

                    ClearSelection();
                    
                    // Unlock editing
                    m_digitFormat.Unlock();
                    break;

                case States.RUNNING:
                    animateRingProgress = false;

                    m_digitFormat.SetDigitColor(theme.GetCurrentColorScheme().m_foreground);
                    //m_labelText.SetSuffix("Running");
                    
                    // Deselection
                    ClearSelection();
                    foreach (DoubleDigit digit in m_selectedDigits)
                    {
                        digit.Deselect();
                    }

                    // Lock Editing
                    m_digitFormat.Lock();
                    
                    break;

                case States.PAUSED:
                    //m_labelText.SetSuffix("Paused");
                    ResetDigitFadeAnim();
                    break;

                case States.COMPLETE:

                    if (GetTimerSettings().m_longBreaks)
                    {
                        // Consume tomatoes once user has started long break via Play
                        if (IsOnBreak() && IsOnLongBreak())
                        {
                            // The timer will remain in 'long break' mode, until the timer is completed. See SwitchState();
                            m_tomatoCounter.ConsumeTomatoes();
                            DeactivateLongBreak();
                        }
                    }

                    // Hide state text
                    m_labelText.gameObject.SetActive(false);
                    // m_labelText.ClearSuffix();

                    // Hide digits and reveal completion
                    m_spawnAnimation.Stop();
                    m_digitFormat.Hide();

                    m_onRingPulse.Invoke();
                    m_alarmSource.Play();

                    break;
            }
            
            ColorUpdateEndTimestampGhost();
        }

        private void ResetDigitFadeAnim()
        {
            accumulatedFadeTime = 0;
            isFadeComplete = true;
            isFading = true;
            accumulatedFadeTime = 0f;
        }
        
        /// <summary>
        /// Plays our timer spawning animation.
        /// <remarks>Used as a UnityEvent</remarks>
        /// </summary>
        public void PlaySpawnAnimation()
        {
            m_spawnAnimation.Stop();
            m_spawnAnimation.Play();

            accumulatedRingAnimationTime = 0;
            animateRingProgress = true;
        }
        
        /// <summary>
        /// Removes any digit selection, and selects the background (our default selection).
        /// </summary>
        public void ClearSelection()
        {
            SetSelection(null);
            m_background.Select();
        }
        
        /// <summary>
        /// Sets the selection to a single <see cref="DoubleDigit"/> and calculates text visibility based on new
        /// selection data. If you'd like to select multiple digits: See <see cref="AddSelection"/>.
        /// </summary>
        /// <param name="currentDigit"></param>
        public void SetSelection(DoubleDigit currentDigit)
        {
            foreach (DoubleDigit digit in m_selectedDigits)
            {
                // Deselect previous digit selections
                if (digit != currentDigit)
                {
                    digit.Deselect();
                }
            }

            m_selectedDigits.Clear();
            if (currentDigit != null)
            {
                m_selectedDigits.Add(currentDigit);
            }
            
            CalculateContextVisibility();
        }
        
        /// <summary>
        /// Determines timer context visibility depending on the selected digits and timer state.
        /// </summary>
        private void CalculateContextVisibility()
        {
            // Hide/show timer context
            if (m_selectedDigits.Count <= 0)
            {
                if (m_state != States.COMPLETE)
                {
                    m_labelText.gameObject.SetActive(true);
                    if (GetTimerSettings().m_longBreaks)
                    {
                        m_tomatoCounter.gameObject.SetActive(true);
                    }
                }
            }
            else
            {
                m_labelText.gameObject.SetActive(false);
                if (GetTimerSettings().m_longBreaks)
                {
                    m_tomatoCounter.gameObject.SetActive(false);
                }
            }
        }

        private void Update()
        {
            if (animateRingProgress)
            {
                m_ring.fillAmount = m_spawnRingProgress.Evaluate(accumulatedRingAnimationTime);
                accumulatedRingAnimationTime += Time.deltaTime;

                // If completed...
                if (accumulatedRingAnimationTime > m_spawnRingProgress.keys[m_spawnRingProgress.length-1].time)
                {
                    m_ring.fillAmount = 1;
                    animateRingProgress = false;
                    
                    m_onSpawnCompletion?.Invoke();
                }
            }
            
            switch (m_state)
            {
                case States.PAUSED:
                    AnimatePausedDigits();
                    break;

                case States.RUNNING:
                    Tick();
                    break;

                case States.COMPLETE:
                    if (disableCompletionAnimation)
                    {
                        return;
                    }
                    AnimateRingPulse();
                    break;
            }
        }

        /// <summary>
        /// Update method for the timer logic. This get invoked every frame.
        /// </summary>
        private void Tick()
        {
            if (currentTime > 0)
            {
                // Decrement timer
                currentTime -= Time.deltaTime;
                        
                // Update visuals
                m_ring.fillAmount = (float)currentTime / totalTime;
                m_digitFormat.ShowTime(TimeSpan.FromSeconds(currentTime));
            }
            else
            {
                CompleteTimer();
            }
        }
        
        private void CompleteTimer()
        {
            OnTimerComplete();
            m_onTimerCompletion?.Invoke();
        }

        private void OnTimerComplete()
        {
            SwitchState(States.COMPLETE);
            
            m_confirmationDialogManager.TryClearCurrentDialogPopup();
            
            // If timer completion was based on work/mode one timer
            // (We don't add tomatoes for breaks)
            if (!IsOnBreak() && !IsOnLongBreak() && m_state != States.SETUP)
            {
                if (GetTimerSettings().m_longBreaks)
                {
                    m_tomatoCounter.FillTomato();
                }
            }
        }
        
        /// <summary>
        /// Animates our digits to flash on and off
        /// </summary>
        private void AnimatePausedDigits()
        {
            accumulatedFadeTime += Time.deltaTime;
            if (isFadeComplete)
            {
                if (accumulatedFadeTime > m_pauseHoldDuration)
                {
                    isFadeComplete = false;
                    accumulatedFadeTime = 0;
                }
            }
            else
            {
                fadeProgress = accumulatedFadeTime / m_pauseFadeDuration;

                m_digitFormat.SetDigitColor(isFading
                    ? Color.Lerp(startingColor, endingColor, fadeProgress)
                    : Color.Lerp(endingColor, startingColor, fadeProgress));

                if (fadeProgress >= 1)
                {
                    // Flip state
                    isFading = !isFading;
                    accumulatedFadeTime = 0f;

                    isFadeComplete = true;
                }
            }
        }
        
        /// <summary>
        /// Animates our ring visuals to pulse
        /// </summary>
        private void AnimateRingPulse()
        {
            // Calculate diameter
            accumulatedRingPulseTime += Time.deltaTime;
            float ringDiameter = m_completeRingPulseDiameter.Evaluate(accumulatedRingPulseTime);

            // Set diameter
            m_ring.material.SetFloat(RingDiameter, ringDiameter);
            m_completionLabel.SetScale(ringDiameter);
            
            // Scale tomatoes too
            m_tomatoCounter.SetHorizontalScale(Vector3.one * ringDiameter);

            if (!hasRingPulseBeenInvoked)
            {
                hasRingPulseBeenInvoked = true;

                m_onRingPulse.Invoke();
                m_alarmSource.Play();
            }

            // Ignore wrap mode and replay completion animation from start
            if (hasRingPulseBeenInvoked && accumulatedRingPulseTime >
                // m_completeRingPulseDiameter[m_completeRingPulseDiameter.length - 1].time)
                m_alarmSource.clip.length + m_delayBetweenRingPulses)
            {
                accumulatedRingPulseTime = 0;
                hasRingPulseBeenInvoked = false;
            }
        }
        
        /// <summary>
        /// Updates our core timer values based on the digit format time span
        /// </summary>
        private void CalculateTimeValues()
        {
            TimeSpan ts = m_digitFormat.GetTime();
            currentTime = ts.TotalSeconds;
            totalTime = (float)ts.TotalSeconds;
            m_digitFormat.SetTime(ts);
            m_digitFormat.RefreshDigitVisuals();
        }

        public double GetCurrentTime()
        {
            return currentTime;
        }
        
        /// <summary>
        /// Shows about content, hides main content, and shows credits bubble.
        /// </summary>
        public void ShowAbout()
        {
            m_sidebarPages.SwitchToAboutPage();
                
            // Special behaviour that's used to display/open up credits bubble when on this page
            m_creditsGhost.Lock();
            m_creditsGhost.FadeIn();
        }

        /// <summary>
        /// Shows main content, hides info, and hides credits bubble
        /// </summary>
        public void ShowMainContent()
        {
            m_sidebarPages.SwitchToTimerPage(() =>
            {
                m_digitFormat.InitializeRuntimeCaret();
            });
            
            m_digitFormat.GenerateFormat();
            m_digitFormat.ShowTime(TimeSpan.FromSeconds(currentTime)); // Update visuals to current time
            
            if (m_state != States.SETUP)
            {
                m_digitFormat.Lock();
            }
            else
            {
                m_digitFormat.Unlock();
            }
            
            // Reset digit animation timings when opening/re-opening this page
            if (m_state == States.PAUSED)
            {
                ResetDigitFadeAnim();
            }
            else
            {
                m_sidebarPages.RefreshTimerPage();
            }
        }
        
        public void ShowSettings()
        {
            m_sidebarPages.SwitchToSettingsPage();
        }
        
        /// <summary>
        /// Transitions timer into States.RUNNING mode
        /// </summary>
        public void Play()
        {
            if (isTimerBeingSetup)
            {
                isTimerBeingSetup = false;
                CalculateTimeValues();
            }

            SwitchState(States.RUNNING);
        }

        /// <summary>
        /// Transitions timer into States.PAUSED mode
        /// </summary>
        public void Pause()
        {
            SwitchState(States.PAUSED);
        }
        
        /// <summary>
        /// Completes the current running timer so the user can move on to the next one.
        /// <remarks>Intended to be used as a UnityEvent on the Skip button.</remarks>
        /// </summary>
        public void Skip()
        {
            CompleteTimer();
            m_digitFormat.CorrectTickAnimVisuals();
        }
        
        /// <summary>
        /// Attempts to transition timer into <see cref="States.SETUP"/> and sets our <see cref="DigitFormat"/> to break
        /// mode, will prompt user with confirmation dialog if necessary.
        /// <remarks>Used as a <see cref="UnityEvent"/> on our timer switch.</remarks>
        /// </summary>
        public void TrySwitchToBreakTimer()
        {
            if (!isTimerBeingSetup && m_state != States.COMPLETE)
            {
                m_confirmationDialogManager.SpawnConfirmationDialog(() =>
                {
                    SwitchTimer(true);
                }, () =>
                {
                    // Switch To Break Timer is triggered by Unity Event via break slider, this simply reset's 
                    // our bool back to it's original setting prior to interaction.
                    m_breakSlider.Initialize(this, IsOnBreak()); // Re-init component
                });
            }
            else
            {
                SwitchTimer(true);
            }
        }
        
        public void SwitchTimer(bool isOnBreak)
        {
            m_digitFormat.m_isOnBreak = isOnBreak;
            m_labelText.UpdateSteamRichPresenceLabel(); // Force steam rich presence to trigger
            SwitchState(States.SETUP);
            isTimerBeingSetup = true;
            CalculateTimeValues();
        }

        /// <summary>
        /// Attempts to transition timer into <see cref="States.SETUP"/> and sets our <see cref="DigitFormat"/> to work
        /// mode, will prompt user with confirmation dialog if necessary.
        /// <remarks>Used as a <see cref="UnityEvent"/> on our timer switch.</remarks>
        /// </summary>
        public void TrySwitchToWorkTimer()
        {
            if (!isTimerBeingSetup && m_state != States.COMPLETE)
            {
                m_confirmationDialogManager.SpawnConfirmationDialog(() =>
                {
                    SwitchTimer(false);
                }, (() =>
                {
                    m_breakSlider.Initialize(this, IsOnBreak()); // Re-init component
                }));
            }
            else
            {
                SwitchTimer(false);
            }
        }

        /// <summary>
        /// Attempts to restart the timer, will prompt user with confirmation dialog if necessary.
        /// </summary>
        public void TryRestart(bool isComplete)
        {
            if (!isTimerBeingSetup && m_state != States.COMPLETE)
            {
                m_confirmationDialogManager.SpawnConfirmationDialog((() =>
                {
                    Restart(isComplete);
                }));
            }
            else
            {
                Restart(isComplete);
            }
        }
        
        /// <summary>
        /// Directly toggles the timer mode to it's opposite mode (break/work) and transitions timer into States.SETUP.
        /// Note: If you want to verify this action, <see cref="TryRestart"/>. <see cref="TryRestart"/> will prompt the
        /// user with a confirmation dialog if necessary.
        /// </summary>
        /// <param name="isCompleted"></param>
        public void Restart(bool isCompleted)
        {
            if (isCompleted)
            {
                m_digitFormat.FlipIsOnBreakBool();
            }

            SwitchState(States.SETUP);
            isTimerBeingSetup = true;
            CalculateTimeValues();

            // Stop digit tick animation
            m_digitFormat.ResetTextPositions();
            ShowTickAnimation();
            
            PlaySpawnAnimation();
        }

        #region Button/Keyboard OnClick Events
        /// <summary>
        /// Presses the play/pause button.
        /// </summary>
        public void TriggerPlayPause()
        {
            m_rightButton.OnPointerClick();
        }

        /// <summary>
        /// Presses the boolean slider to toggle between our <see cref="DigitFormat"/> to work/break mode.
        /// </summary>
        public void TriggerTimerSwitch()
        {
            m_breakSlider.OnPointerClick(null);
        }
        
        /// <summary>
        /// Presses/Taps the skip button.
        /// </summary>
        public void TriggerTimerSkip()
        {
            m_skipButton.Press();
        }
        
        /// <summary>
        /// Presses the restart button.
        /// </summary>
        public void TriggerTimerRestart()
        {
            m_leftButtonSVGClick.OnPointerClick(null);
        }

        /// <summary>
        /// Presses the boolean slider to toggle between light/dark themes.
        /// </summary>
        public void TriggerThemeSwitch()
        {
            m_themeSlider.Interact();
        }
        #endregion

        /// <summary>
        /// Selects all the digits in our <see cref="DigitFormat"/>.
        /// </summary>
        public void SelectAll()
        {
            // Only allow 'select all' to work when we are in setup state
            if (m_state != States.SETUP)
            {
                return;
            }
            
            ClearSelection();

            foreach (DoubleDigit digit in m_digitFormat.GetDigits())
            {
                AddSelection(digit);
            }

            foreach (DoubleDigit digit in m_selectedDigits)
            {
                digit.Highlight();
            }
            
            // Since we are highlighting (instead of selecting), we bypass the text state logic hence we 
            // invoke it again here.
            CalculateContextVisibility();
        }

        /// <summary>
        /// Adds the provided <see cref="DoubleDigit"/> to our selection list.
        /// </summary>
        /// <param name="digitToAddToSelection">What <see cref="DoubleDigit"/> do you want to add to our selection?
        /// </param>
        private void AddSelection(DoubleDigit digitToAddToSelection)
        {
            if (!m_selectedDigits.Contains(digitToAddToSelection))
            {
                m_selectedDigits.Add(digitToAddToSelection);
            }
        }
        
        /// <summary>
        /// Returns our current timer values in a <see cref="String"/>.
        /// <example>Such as "00:24:35" (without the quotation marks)</example>
        /// </summary>
        /// <returns>Our current timer value.</returns>
        public string GetTimerString(bool richPresenceFormat = false)
        {
            return m_digitFormat.GetTimerString(richPresenceFormat);
        }
        
        /// <summary>
        /// Returns our currently selected <see cref="DigitFormat.SupportedFormats"/>'s index value.
        /// </summary>
        /// <returns>A number representing our enum index. See <see cref="DigitFormat.SupportedFormats"/></returns>
        public int GetDigitFormatIndex()
        {
            return m_digitFormat.GetFormatIndex();
        }

        /// <summary>
        /// Returns a list of our current selected element.
        /// <remarks>Will return a list of <see cref="Selectable"/>'s, not specifically <see cref="DoubleDigit"/>'s.
        /// </remarks>
        /// </summary>
        /// <returns></returns>
        public List<Selectable> GetSelections()
        {
            return m_selectedDigits.Select(doubleDigit => doubleDigit.GetSelectable()).ToList();
        }
        
        /// <summary>
        /// Is our <see cref="DigitFormat"/> in break mode?
        /// </summary>
        /// <returns></returns>
        public bool IsOnBreak()
        {
            return m_digitFormat.m_isOnBreak;
        }

        /// <summary>
        /// Is our <see cref="DigitFormat"/> in long break mode?
        /// </summary>
        public bool IsOnLongBreak()
        {
            return m_digitFormat.m_isOnLongBreak;
        }

        /// <summary>
        /// Is our <see cref="AboutPage"/> currently open and visible?
        /// </summary>
        /// <returns></returns>
        public bool IsAboutPageOpen()
        {
            return m_sidebarPages.IsAboutPageOpen();
        }

        /// <summary>
        /// Is our <see cref="SettingsPage"/> currently open and visible?
        /// </summary>
        /// <returns></returns>
        public bool IsSettingPageOpen()
        {
            return m_sidebarPages.IsSettingsPageOpen();
        }

        /// <summary>
        /// Is our <see cref="Sidebar"/> currently open and visible?
        /// </summary>
        /// <returns></returns>
        public bool IsSidebarOpen()
        {
            return m_sidebarMenu.IsOpen();
        }

        /// <summary>
        /// Is our timer / digit format currently open and visible?
        /// </summary>
        /// <returns></returns>
        public bool IsMainContentOpen()
        {
            return m_sidebarPages.IsTimerPageOpen();
        }
        
        /// <summary>
        /// Sets our <see cref="DigitFormat"/> timer to the provided string. Intended to be used when pasting
        /// values in from our clipboard.
        /// <remarks>This will only work if the timer is in <see cref="States.SETUP"/> mode.</remarks>
        /// </summary>
        /// <param name="timeString"></param>
        public void SetTimerValue(string timeString)
        {
            // Only allow 'Set Timer Value' to work when we are in the setup state
            if (m_state != States.SETUP)
            {
                return;
            }
            
            m_digitFormat.SetTimerValue(timeString);
        }

        /// <summary>
        /// Sets our background's selection navigation to the provided <see cref="Navigation"/>.
        /// <remarks>Intended to change focus to our digits when attempting to select left / right
        /// from the background.</remarks>
        /// </summary>
        /// <param name="backgroundNav"></param>
        public void SetBackgroundNavigation(Navigation backgroundNav)
        {
            m_background.SetSelectionNavigation(backgroundNav);
        }
        
        /// <summary>
        /// Attempts to change our <see cref="DigitFormat"/> to the provided <see cref="DigitFormat.SupportedFormats"/>,
        /// will prompt user with confirmation dialog if necessary.
        /// </summary>
        /// <param name="desiredFormat"></param>
        public void TryChangeFormat(DigitFormat.SupportedFormats desiredFormat, bool restartCreditsGhost = true)
        {
            if (!isTimerBeingSetup && m_state == States.RUNNING)
            {
                m_digitFormat.SwitchFormat(desiredFormat);
                m_confirmationDialogManager.SpawnConfirmationDialog(GenerateFormat, () =>
                {
                    GetTimerSettings().m_format = desiredFormat;
                    UserSettingsSerializer.SaveSettingsFile(GetTimerSettings(), "timer-settings");
                    m_sidebarPages.SetSettingDigitFormatDropdown(m_digitFormat.GetPreviousFormatSelection());
                });
            }
            else
            {
                m_digitFormat.SwitchFormat(desiredFormat);
                GenerateFormat();
                GetTimerSettings().m_format = desiredFormat;
                UserSettingsSerializer.SaveSettingsFile(GetTimerSettings(), "timer-settings");
            }

            if (restartCreditsGhost)
            {
                RestartCreditsGhost();
            }
        }

        /// <summary>
        /// Changes the format directly
        /// </summary>
        private void GenerateFormat()
        {
            m_digitFormat.GenerateFormat();
            Restart(false); 
            
            if (m_sidebarPages.IsSettingsPageOpen())
            {
                m_sidebarPages.UpdateSettingsDigitFormatDropdown();
            }
        }
        
        /// <summary>
        /// Applies our <see cref="Theme"/> changes to our referenced components when necessary.
        /// </summary>
        /// <param name="theme">The theme to apply on our referenced components.</param>
        public void ColorUpdate(Theme theme)
        {
            ColorScheme currentColors = theme.GetCurrentColorScheme();
            
            // State text
            m_labelText.SetTextColor(currentColors.m_backgroundHighlight);
            
            // Ring background
            m_ringBackground.material.SetColor(RingColor, theme.GetCurrentColorScheme().m_backgroundHighlight);

            // Left Button Background
            Image leftContainerTarget = m_leftButtonSVGClick.m_containerTarget.GetComponent<Image>();
            if (leftContainerTarget != null)
            {
                Material material = new (leftContainerTarget.material);
                material.SetColor(CircleColor, theme.GetCurrentColorScheme().m_backgroundHighlight);
                leftContainerTarget.material = material;
            }
            
            // Left Button Icon
            m_leftButtonSVGClick.m_icon.color = currentColors.m_foreground;

            // Paused Digits
            startingColor = theme.GetCurrentColorScheme().m_foreground;
            endingColor = theme.GetCurrentColorScheme().m_backgroundHighlight;

            // Reset paused digit anim
            ResetDigitFadeAnim();
            
            m_menuToggleSprite.OverrideFalseColor(theme.GetCurrentColorScheme().m_foreground);
            m_menuToggleSprite.ColorUpdate(theme);

            UpdateRing(theme);
        }

        private void UpdateRing(Theme theme)
        {
            // Create a new material (copied from m_ring)
            Material ringMaterial = new(m_ring.material);
            
            switch (m_state)
            {
                case States.SETUP:
                    // Modify copied material
                    ringMaterial.SetColor(RingColor, !m_digitFormat.m_isOnBreak ? 
                        theme.GetCurrentColorScheme().m_modeOne : theme.GetCurrentColorScheme().m_modeTwo);
                    ringMaterial.SetFloat(RingDiameter, 0.9f);

                    // Complete ring
                    m_ring.fillAmount = 1f;
                    break;
                
                case States.RUNNING:
                    // Modify copied material
                    ringMaterial.SetColor(RingColor, theme.GetCurrentColorScheme().m_running);
                    break;
                
                case States.PAUSED:
                    // Modify copied material
                    ringMaterial.SetColor(RingColor, !m_digitFormat.m_isOnBreak ? 
                        theme.GetCurrentColorScheme().m_modeOne : theme.GetCurrentColorScheme().m_modeTwo);
                    break;
                
                case States.COMPLETE:
                    // Modify copied material
                    ringMaterial.SetColor(RingColor, theme.GetCurrentColorScheme().m_complete);
                    
                    // Complete ring
                    m_ring.fillAmount = 1f;
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Apply modified material
            m_ring.material = ringMaterial;
        }

        // Piper
        public void RefreshInputLabel()
        {
            m_labelText.StateUpdate(m_state, GetTheme());
        }

        // Piper
        public void RefreshDigitVisuals()
        {
            m_digitFormat.RefreshDigitVisuals();
        }
        
        /// <summary>
        /// Triggers a <see cref="IColorHook"/> ColorUpdate() on our <see cref="CreditsGhost"/>.
        /// </summary>
        public void ColorUpdateCreditsBubble()
        {
            m_creditsGhost.ColorUpdate(m_themeManager.GetTheme());
        }
        
        /// <summary>
        /// Sets the users setting preference to enable/disable long breaks.
        /// </summary>
        /// <remarks>Intended to be used as a UnityEvent. Otherwise you can directly do this
        /// on the public property in the settings object.</remarks>
        /// <param name="state">Do you want the user to be able to collect tomatoes/pomodoros and unlock the
        /// long break mode?</param>
        public void SetSettingLongBreaks(bool state = true)
        {
            // Apply and save
            GetTimerSettings().m_longBreaks = state;
            UserSettingsSerializer.SaveSettingsFile(GetTimerSettings(), "timer-settings");
            
            // Apply component swap
            if (state)
            {
                if (!m_tomatoCounter.IsInitialized())
                {
                    m_tomatoCounter.Initialize(this, m_loadedTimerSettings.m_pomodoroCount);
                }
                else
                {
                    // Only rebuild tomatoes
                    m_tomatoCounter.SetPomodoroCount(m_loadedTimerSettings.m_pomodoroCount, m_tomatoCounter.GetTomatoProgress());
                }

                m_tomatoCounter.gameObject.SetActive(true);
            }
            else
            {
                m_tomatoCounter.gameObject.SetActive(false);
                DeactivateLongBreak();
                IfSetupTriggerRebuild();
            }
            m_completionLabel.MoveAnchors(state);
        }

        /// <summary>
        /// Triggers a rebuild of our timer if we are in SETUP mode.
        /// <remarks>Intended when in long break mode and you wipe tomato progress.</remarks>
        /// </summary>
        public void IfSetupTriggerRebuild()
        {
            if (m_state == States.SETUP)
            {
                SwitchTimer(m_digitFormat.m_isOnBreak);
            }
        }
        
        // TODO: Implement
        /// <summary>
        /// Sets the users setting preference to enable/disable the EndTimestampBubble (located at the bottom right).
        /// </summary>
        /// <param name="state"></param>
        private void SetSettingEndTimestampBubble(bool state)
        {
            // TODO: Feature setting
        }

        /// <summary>
        /// Enables long break on our <see cref="DigitFormat"/>.
        /// </summary>
        public void ActivateLongBreak()
        {
            m_digitFormat.ActivateLongBreak();

#if !UNITY_ANDROID && !UNITY_WSA
            // Check if steam client is found...
            if (SteamClient.IsValid)
            {
                // Fetch first tomato achievement
                Achievement ach = new Achievement("ACH_ACQUIRE_LONG_BREAK");
                
                // If achievement is not unlocked...
                if (!ach.State)
                {
                    ach.Trigger();
                    Debug.Log("Steam Achievement Unlocked! 'Couch Tomato!: Unlock your first long break.'");
                }
            }
#endif
        }

        /// <summary>
        /// Disables long break on our <see cref="DigitFormat"/>. (Note: <see cref="DigitFormat"/> could still be
        /// in a work / break mode)
        /// </summary>
        public void DeactivateLongBreak()
        {
            m_digitFormat.DeactivateLongBreak();
        }

        /// <summary>
        /// Fades in/out our credits bubble.
        /// </summary>
        /// <param name="fadeIn">Do you want the credits bubble to fade in? (Providing `False` will make
        /// the credit's bubble fade out.)</param>
        public void FadeCreditsBubble(bool fadeIn)
        {
            if (fadeIn)
            {
                m_creditsGhost.FadeIn();
                m_creditsGhost.Lock();
            }
            else
            {
                m_creditsGhost.FadeOut();
                m_creditsGhost.Unlock();
            }
        }

        /// <summary>
        /// Positions our <see cref="CreditsGhost"/> to stay within the bounds of the sidebar.
        /// </summary>
        /// <param name="desiredWidthPercentage"></param>
        /// <param name="rightOffsetInPixels"></param>
        public void ConformCreditsBubbleToSidebar(float desiredWidthPercentage, float rightOffsetInPixels = -10)
        {
            m_creditsGhost.SetWidth(desiredWidthPercentage);
            m_creditsGhost.SetRightOffset(rightOffsetInPixels);
        }

        /// <summary>
        /// Positions our <see cref="CreditsGhost"/> back to it's original position. (Not conforming to the sidebar)
        /// </summary>
        public void ResetCreditsBubbleSidebarConformity()
        {
            m_creditsGhost.ResetWidth();
            m_creditsGhost.ResetRightOffset();
        }
        
        /// <summary>
        /// Does this timer currently have any pomodoro/tomato progression?
        /// </summary>
        /// <returns></returns>
        public bool HasTomatoProgression()
        {
            return m_tomatoCounter.HasProgression() || m_digitFormat.m_isOnLongBreak;
        }

        /// <summary>
        /// Changes the current timer to the provided value.
        /// <remarks>Intended to be used by our Media Creator.</remarks>
        /// </summary>
        /// <param name="currentTimeInSeconds"></param>
        public void SetCurrentTime(float currentTimeInSeconds)
        {
            currentTime = currentTimeInSeconds;
            m_digitFormat.CorrectTickAnimVisuals();
            m_digitFormat.ShowTime(TimeSpan.FromSeconds(currentTime));
        }
        
        public void SetPomodoroCount(int desiredPomodoroCount, int pomodoroProgress)
        {
            m_tomatoCounter.SetPomodoroCount(desiredPomodoroCount, pomodoroProgress);
            
            // Check if user achieved long break with new settings...
            if (pomodoroProgress == desiredPomodoroCount)
            {
                // Trigger long break and rebuild.
                ActivateLongBreak();
                IfSetupTriggerRebuild();
            }
            // It's also possible our user is already in the long break state screen, and their new 
            // numbers might not be valid. Check for this too...
            else if (IsOnLongBreak() && m_state == States.SETUP)
            {
                // De-activate their long break since their pomodoro count changed / is no longer valid.
                DeactivateLongBreak();
                IfSetupTriggerRebuild();
            }            
        }

        public void SetAlarmSound(AudioClip alarmSound, bool attemptAlarmSoundPreview)
        {
            m_alarmSource.clip = alarmSound;

            if (!attemptAlarmSoundPreview)
            {
                return;
            }

            // Preview alarm sound, if it's not currently playing
            if (m_state != States.COMPLETE)
            {
                m_alarmSource.Play();
            }
        }

        public int GetTomatoProgress()
        {
            return m_tomatoCounter.GetTomatoProgress();
        }

        public int GetTomatoCount()
        {
            return m_tomatoCounter.GetTomatoCount();
        }

        // TODO: Remove piper methods - We want explicit methods instead of reaching in. Separation of concerns.
        [Obsolete]
        public ConfirmationDialogManager GetConfirmDialogManager()
        {
            return m_confirmationDialogManager;
        }

        public TranslucentImageSource GetTranslucentImageSource()
        {
            return m_translucentImageSource;
        }
        
        // TODO: Remove PomodoroTimer dependency
        public Theme GetTheme()
        {
            return m_themeManager.GetTheme();
        }

        public void ShowOverlay()
        {
            ClearSelection();
            m_overlay.Show();
        }

        public void HideOverlay()
        {
            m_overlay.Hide();
        }
        
        public void CloseSidebar()
        {
            m_sidebarMenu.Close();
        }
        
        public void ColorUpdateEndTimestampGhost()
        {
            m_endTimestampGhost.ColorUpdate(GetTheme());
        }

#if UNITY_EDITOR
        // TODO: Find a more consistent way of doing all these. Such as a ShowInstantly() method or something.
        // Creator Media Methods: Only intended for instant visual changes for Editor tooling
        public void MCHideCreditsBubble()
        {
            m_creditsGhost.FadeOut(true);
        }

        public void MCShowCreditsBubble()
        {
            m_creditsGhost.FadeIn(true);
        }

        public void MCShowEndTimestampBubble()
        {
            m_endTimestampGhost.FadeIn(true);
        }

        public void MCDisableCompletionAnimation()
        {
            m_completionLabel.HideCompletionAnimation();
            disableCompletionAnimation = true;
        }

        public void MCEnableBreakSlider()
        {
            m_breakSlider.SetVisualToEnable();
        }
        
        public void MCShowSidebar()
        {
            m_sidebarMenu.gameObject.SetActive(true);
            ConformCreditsBubbleToSidebar(m_sidebarMenu.CalculateSidebarWidth());
            ShowOverlay();
        }

        public void MCDisableBreakSlider()
        {
            m_breakSlider.SetVisualToDisable();
        }

        public void MCHideSidebar()
        {
            m_sidebarMenu.gameObject.SetActive(false); 
            HideOverlay();
        }

        public void MCEnableDarkModeToggle()
        {
            m_themeSlider.SetVisualToEnable();
        }

        public void MCDisableDarkModeToggle()
        {
            m_themeSlider.SetVisualToDisable();
        }

        public void MCEnableThemeToggleAnimation()
        {
            m_themeSlider.EnableAnimation();
        }

        public void MCShowMain()
        {
            m_sidebarPages.MCSwitchToMainPageInstant();
        }

        public void MCShowAbout()
        {
            m_sidebarPages.MCSwitchToAboutPageInstant();
            
            // Special behaviour that's used to display/open up credits bubble when on this page
            m_creditsGhost.Lock();
            m_creditsGhost.FadeIn();
        }

        public void MCShowSettings()
        {
            m_sidebarPages.MCSwitchToSettingsPageInstant();
        }
#endif
        
        // Settings
        public SystemSettings GetSystemSettings()
        {
            return m_loadedSystemSettings;
        }

        public TimerSettings GetTimerSettings()
        {
            return m_loadedTimerSettings;
        }

        public void ShowTickAnimation()
        {
            m_digitFormat.ShowTickAnimation();
        }

        public bool IsSteamworksInitialized()
        {
            return m_steamManager.IsInitialized();
        }
        
#if !UNITY_ANDROID && !UNITY_WSA
        public void ShutdownSteamManager()
        {
            m_steamManager.Shutdown();
        }
#endif
        
        public void UpdateSteamRichPresence()
        {
            // Update state
            m_steamManager.UpdateState(m_state, GetTheme());
            
            // Override state if on settings page...
            if (IsSettingPageOpen())
            {
                SteamTrySetRichPresence("steam_display", "#Settings");
            }
            
            // Update label
            m_labelText.UpdateSteamRichPresenceLabel();
        }
        
        public void TrySubmitConfirmationDialog()
        {
            m_confirmationDialogManager.GetCurrentConfirmationDialog()?.Submit();
        }

        public void TryCancelConfirmationDialog()
        {
            m_confirmationDialogManager.GetCurrentConfirmationDialog()?.Cancel();
        }

        public void RestartCreditsGhost()
        {
            // Show instantly
            m_creditsGhost.FadeIn(true);

            // Re-init
            m_creditsGhost.Restart();
        }
        
        /// <summary>
        /// Attempts to set the Steam Rich Presence states.
        /// Will safely fail if Steam Rich Presence module isn't initialized.
        /// See <see cref="IsSteamworksRichPresenceInitialized"/>, <see cref="InitializeSteamRichPresence"/>, and
        /// <seealso cref="ClearSteamRichPresence"/>.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SteamTrySetRichPresence(string key, string value)
        {
            m_steamManager.SetRichPresence(key, value);
        }
        
#if !UNITY_ANDROID && !UNITY_WSA 
        /// <summary>
        /// Is the Steam Rich presence module initialized/currently running?
        /// </summary>
        /// <returns></returns>
        public bool IsSteamworksRichPresenceInitialized()
        {
            return m_steamManager.IsRichPresenceInitialized();
        }

        /// <summary>
        /// Inits/Starts-up our Steam Rich presence module.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void InitializeSteamRichPresence()
        {
            // TODO: Only initialize steam rich presence
            m_steamManager.InitializeSteamModules();
        }

        /// <summary>
        /// Clears out and disables the Steam Rich Presence module.
        /// </summary>
        public void ClearSteamRichPresence()
        {
            m_steamManager.ClearSteamRichPresence();
        }
#endif
    }
}
