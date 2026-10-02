using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;

namespace NocturneMemoryHelper
{
    [DataContract]
    internal sealed class AppSettings
    {
        [DataMember] public int IntervalMinutes = 30;
        // Retained only for compatibility with old settings; never loaded or used.
        [DataMember] public string SimConnectPath = String.Empty;
        [DataMember] public bool AutomaticCleaningEnabled = false;
        [DataMember] public bool CleanAtLaunch = false;
        [DataMember] public bool StartWithMSFS = false;
        [DataMember] public bool CloseWithMSFS = false;

        internal static AppSettings Load()
        {
            AppSettings settings = JsonFiles.Read<AppSettings>(Paths.SettingsPath);
            if (settings == null) settings = new AppSettings();
            if (settings.IntervalMinutes < 1 || settings.IntervalMinutes > 1440) settings.IntervalMinutes = 30;
            if (settings.SimConnectPath == null) settings.SimConnectPath = String.Empty;

            // Cleaning must be explicitly enabled again after every launch.
            settings.CleanAtLaunch = false;
            settings.AutomaticCleaningEnabled = false;
            return settings;
        }

        internal void Save()
        {
            JsonFiles.Write(Paths.SettingsPath, this);
        }
    }

    [DataContract]
    internal sealed class WorkerStatus
    {
        [DataMember] public string RunId = String.Empty;
        [DataMember] public bool Running;
        [DataMember] public int Step;
        [DataMember] public int Total = 9;
        [DataMember] public string Action = String.Empty;
        [DataMember] public bool? Succeeded;
        [DataMember] public string Message = String.Empty;
        [DataMember] public DateTime UpdatedAt = DateTime.UtcNow;
        [DataMember] public bool GateApproved;
    }

    internal static class JsonFiles
    {
        private static readonly object Sync = new object();

        internal static T Read<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    lock (Sync)
                    {
                        using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                            return serializer.ReadObject(stream) as T;
                        }
                    }
                }
                catch (IOException)
                {
                    Thread.Sleep(25);
                }
                catch (SerializationException)
                {
                    return null;
                }
            }
            return null;
        }

        internal static void Write<T>(string path, T value)
        {
            Paths.EnsureDataDirectory();
            string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");

            lock (Sync)
            {
                try
                {
                    using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                        serializer.WriteObject(stream, value);
                        stream.Flush(true);
                    }

                    if (File.Exists(path))
                    {
                        try { File.Replace(temporaryPath, path, null); }
                        catch (PlatformNotSupportedException)
                        {
                            File.Delete(path);
                            File.Move(temporaryPath, path);
                        }
                        catch (IOException)
                        {
                            File.Delete(path);
                            File.Move(temporaryPath, path);
                        }
                    }
                    else File.Move(temporaryPath, path);
                }
                finally
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
            }
        }
    }

    internal static class Logger
    {
        private static readonly object Sync = new object();

        internal static void Write(string message)
        {
            try
            {
                Paths.EnsureDataDirectory();
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine;
                lock (Sync)
                {
                    File.AppendAllText(Paths.LogPath, line, new UTF8Encoding(false));
                }
            }
            catch { }
        }
    }
}
