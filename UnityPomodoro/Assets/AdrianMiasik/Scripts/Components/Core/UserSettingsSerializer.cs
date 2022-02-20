﻿using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace AdrianMiasik.Components.Core
{
    public static class UserSettingsSerializer
    {
        private static string dataExtension = ".dat";
        
        /// <summary>
        /// Creates two data files in our persistent path. One for system settings, and one for timer settings.
        /// </summary>
        /// <param name="systemSettings"></param>
        /// <param name="timerSettings"></param>
        public static void Save(SystemSettings systemSettings, TimerSettings timerSettings)
        {
            BinaryFormatter bf = new BinaryFormatter();
            
            // System settings
            FileStream fs = File.Create(Application.persistentDataPath + "/system-settings" + dataExtension);
            bf.Serialize(fs, systemSettings);
            
            // Timer settings
            fs = File.Create(Application.persistentDataPath + "/timer-settings" + dataExtension);
            bf.Serialize(fs, timerSettings);
            
            fs.Close();
        }

        public static void SaveSystemSettings(SystemSettings systemSettings)
        {
            BinaryFormatter bf = new BinaryFormatter();
            
            // System settings
            FileStream fs = File.Create(Application.persistentDataPath + "/system-settings" + dataExtension);
            bf.Serialize(fs, systemSettings);

            fs.Close();
        }
        
        public static void SaveTimerSettings(TimerSettings timerSettings)
        {
            BinaryFormatter bf = new BinaryFormatter();
            
            // System settings
            FileStream fs = File.Create(Application.persistentDataPath + "/timer-settings" + dataExtension);
            bf.Serialize(fs, timerSettings);

            fs.Close();
        }

        /// <summary>
        /// Returns the timer settings saved in  our data file.
        /// </summary>
        /// <returns></returns>
        public static TimerSettings LoadTimerSettings()
        {
            if (File.Exists(Application.persistentDataPath + "/timer-settings" + dataExtension))
            {
                BinaryFormatter bf = new BinaryFormatter();
                FileStream fs = File.Open(Application.persistentDataPath + "/timer-settings" + dataExtension,
                    FileMode.Open);
                TimerSettings timerSettings = bf.Deserialize(fs) as TimerSettings;
                fs.Close();
                Debug.Log("Loaded Timer Settings Successfully!");
                return timerSettings;
            }

            Debug.LogWarning("File not found. Unable to load timer settings.");
            return null;
        }

        /// <summary>
        /// Returns the system settings saved in our data file.
        /// </summary>
        /// <returns></returns>
        public static SystemSettings LoadSystemSettings()
        {
            if (File.Exists(Application.persistentDataPath + "/system-settings" + dataExtension))
            {
                BinaryFormatter bf = new BinaryFormatter();
                FileStream fs = File.Open(Application.persistentDataPath + "/system-settings" + dataExtension,
                    FileMode.Open);
                SystemSettings systemSettings = bf.Deserialize(fs) as SystemSettings;
                fs.Close();
                Debug.Log("Loaded System Settings Successfully!");
                return systemSettings;
            }

            Debug.LogWarning("File not found. Unable to load system settings.");
            return null;
        }
    }
}