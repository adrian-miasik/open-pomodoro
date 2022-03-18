using System;
using System.Linq;
using AdrianMiasik.Interfaces;
using AdrianMiasik.ScriptableObjects;
using Unity.Notifications.Android;
using UnityEngine;

namespace AdrianMiasik.Android
{
    // TODO: Implement TimerState interface instead
    public class AndroidNotifications : MonoBehaviour, ITimerState
    {
        private int timerNotificationID;
        private PomodoroTimer timer;
        
        private enum NotificationChannels
        {
            ALARMS
        }

        public void Initialize(PomodoroTimer pomodoroTimer)
        {
            timer = pomodoroTimer;
            
            // Create notification channel for our alarms / timer completions.
            AndroidNotificationChannel channel = new AndroidNotificationChannel
            {
                Id = GetChannelString(NotificationChannels.ALARMS),
                Name = "Alarms (& Timers)",
                Importance = Importance.High,
                Description = "This channel is used to send notifications on timer completions. " +
                              "This includes both work and break timers.",
            };
            
            // Register notification channel
            AndroidNotificationCenter.RegisterNotificationChannel(channel);

            Debug.Log("Created notification channel: " + GetChannelString(NotificationChannels.ALARMS));
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                AndroidNotificationIntentData intentData = AndroidNotificationCenter.GetLastNotificationIntent();

                // If user has pressed the notification...
                if (intentData != null)
                {
                    timer.Skip();
                }
            }
        }

        /// <summary>
        /// Schedules a notification to activate/prompt the user at the provided fire time.
        /// </summary>
        /// <param name="notificationFireTime"></param>
        /// <param name="titlePrefix"></param>
        /// <returns>The scheduled notification's ID. (This ID can be used for modifying the notification if needed)
        /// </returns>
        private void ScheduleTimerNotification(string titlePrefix, DateTime notificationFireTime)
        {
            string notificationTitle = "Timer Completed!";

            // If a title prefix has been provided...
            if (!string.IsNullOrEmpty(titlePrefix))
            {
                // Set prefix
                string newTitle = titlePrefix + " " + notificationTitle;
                notificationTitle = newTitle;
            }
            
            AndroidNotification notification = new AndroidNotification
            {
                Title = notificationTitle,
                Text = "Your timer is complete! (" + notificationFireTime.ToString("h:mm:ss tt") + ")",
                FireTime = notificationFireTime,
                LargeIcon = "app-icon"
            };

            timerNotificationID = AndroidNotificationCenter.SendNotification(notification, 
                GetChannelString(NotificationChannels.ALARMS));
        }

        private void CancelScheduledTimerNotification()
        {
            NotificationStatus notificationStatus = 
                AndroidNotificationCenter.CheckScheduledNotificationStatus(timerNotificationID);

            switch (notificationStatus)
            {
                case NotificationStatus.Unavailable:
                    break;
                case NotificationStatus.Unknown:
                    break;
                case NotificationStatus.Scheduled:
                    AndroidNotificationCenter.CancelScheduledNotification(timerNotificationID);
                    break;
                case NotificationStatus.Delivered:
                    AndroidNotificationCenter.CancelNotification(timerNotificationID);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private string GetChannelString(NotificationChannels desiredChannel)
        {
            return desiredChannel.ToString().ToLower();
        }
        
        public void StateUpdate(PomodoroTimer.States state, Theme theme)
        {
            switch (state)
            {
                case PomodoroTimer.States.SETUP:
                    CancelScheduledTimerNotification();
                    break;
                
                case PomodoroTimer.States.RUNNING:
                    // Schedule Android Notification
                    string prefixTitle;
                    if (!timer.IsOnBreak())
                    {
                        prefixTitle = "Work";
                    }
                    else
                    {
                        prefixTitle = "Break";
                    }
                    ScheduleTimerNotification(prefixTitle, DateTime.Now.AddSeconds(timer.GetCurrentTime()));
                    break;
                
                case PomodoroTimer.States.PAUSED:
                    CancelScheduledTimerNotification();
                    break;
                
                case PomodoroTimer.States.COMPLETE:
                    // We don't need to show notification when app is in focus.
                    if (Application.isFocused)
                    {
                        CancelScheduledTimerNotification();
                    }
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }
    }
}
