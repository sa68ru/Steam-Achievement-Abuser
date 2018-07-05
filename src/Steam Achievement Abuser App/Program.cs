using SAM.API;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Steam_Achievement_Abuser_App
{
    class Program
    {
        static Client _SteamClient = null;
        static System.Timers.Timer Calls = new System.Timers.Timer();
        static long ID = 0;
        static void Main(string[] args)
        {
            if (args[0].Length == 0)
                return;
            ID = Convert.ToInt64(args[0]);
            try
            {
                _SteamClient = new Client();
                if (_SteamClient.Initialize(ID) == false)
                    return;
            }
            catch 
            {
                return;
            }
            Calls.Elapsed += Calls_Tick;
            Calls.Interval = 100;
            Calls.Start();
            SAM.API.Callbacks.UserStatsReceived _Callback = _SteamClient.CreateAndRegisterCallback<SAM.API.Callbacks.UserStatsReceived>();
            _Callback.OnRun += OnAppStatsReceived;
            if (_SteamClient.SteamUserStats.RequestCurrentStats() == false)
                return;
            Console.ReadKey();
        }
        private static void Calls_Tick(object sender, EventArgs e)
        {
            Calls.Enabled = false;
            _SteamClient.RunCallbacks(false);
            Calls.Enabled = true;
        }
        static void Kill()
        {
            Process.GetCurrentProcess().Kill();
        }
        static void OnAppStatsReceived(SAM.API.Types.UserStatsReceived param)
        {
            if (param.Result != 1)
                Kill();
            List<AchievementDefinition> Achievements = new List<AchievementDefinition>();
            LoadUserGameStatsSchema(out Achievements, (uint)ID);
            foreach (var Achievement in Achievements)
            {
                if (!_SteamClient.SteamUserStats.SetAchievement(Achievement.Id, true))
                    continue;
            }
            Kill();
        }
        private static bool LoadUserGameStatsSchema(out List<AchievementDefinition> achives, uint _GameId)
        {
            achives = new List<AchievementDefinition>();
            string path;
            try
            {
                path = Steam.GetInstallPath();
                path = Path.Combine(path, "appcache");
                path = Path.Combine(path, "stats");
                path = Path.Combine(path, string.Format("UserGameStatsSchema_{0}.bin", _GameId));

                if (File.Exists(path) == false)
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            var kv = KeyValue.LoadAsBinary(path);

            if (kv == null)
            {
                return false;
            }

            var currentLanguage = _SteamClient.SteamApps003.GetCurrentGameLanguage();
            var stats = kv[_GameId.ToString(CultureInfo.InvariantCulture)]["stats"];
            if (stats.Valid == false ||
                stats.Children == null)
            {
                return false;
            }

            foreach (var stat in stats.Children)
            {
                if (stat.Valid == false)
                {
                    continue;
                }

                var rawType = stat["type_int"].Valid
                                  ? stat["type_int"].AsInteger(0)
                                  : stat["type"].AsInteger(0);
                var type = (SAM.API.Types.UserStatType)rawType;
                switch (type)
                {
                    case SAM.API.Types.UserStatType.Invalid:
                        {
                            break;
                        }
                    case SAM.API.Types.UserStatType.Achievements:
                    case SAM.API.Types.UserStatType.GroupAchievements:
                        {
                            if (stat.Children != null)
                            {
                                foreach (var bits in stat.Children.Where(
                                    b => b.Name.ToLowerInvariant() == "bits"))
                                {
                                    if (bits.Valid == false ||
                                        bits.Children == null)
                                    {
                                        continue;
                                    }
                                    foreach (var bit in bits.Children)
                                    {
                                        string id = bit["name"].AsString("");
                                        string name = GetLocalizedString(bit["display"]["name"], currentLanguage, id);
                                        string desc = GetLocalizedString(bit["display"]["desc"], currentLanguage, "");
                                        achives.Add(new AchievementDefinition()
                                        {
                                            Id = id,
                                            Name = name,
                                            Description = desc,
                                            IconNormal = bit["display"]["icon"].AsString(""),
                                            IconLocked = bit["display"]["icon_gray"].AsString(""),
                                            IsHidden = bit["display"]["hidden"].AsBoolean(false),
                                            Permission = bit["permission"].AsInteger(0),
                                        });
                                    }
                                }
                            }

                            break;
                        }
                     default:
                        break;
                }
            }
            return true;
        }
        private static string GetLocalizedString(KeyValue kv, string language, string defaultValue)
        {
            var name = kv[language].AsString("");
            if (string.IsNullOrEmpty(name) == false)
            {
                return name;
            }

            if (language != "english")
            {
                name = kv["english"].AsString("");
                if (string.IsNullOrEmpty(name) == false)
                {
                    return name;
                }
            }

            name = kv.AsString("");
            if (string.IsNullOrEmpty(name) == false)
            {
                return name;
            }

            return defaultValue;
        }
    }
    internal class AchievementDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public string IconNormal;
        public string IconLocked;
        public bool IsHidden;
        public int Permission;
        public override string ToString()
        {
            return string.Format("{0}: {1}", this.Name ?? this.Id ?? base.ToString(), this.Permission);
        }
    }
    internal static class StreamHelpers
    {
        public static byte ReadValueU8(this Stream stream)
        {
            return (byte)stream.ReadByte();
        }

        public static int ReadValueS32(this Stream stream)
        {
            var data = new byte[4];
            int read = stream.Read(data, 0, 4);
            // Debug.Assert(read == 4);
            return BitConverter.ToInt32(data, 0);
        }

        public static uint ReadValueU32(this Stream stream)
        {
            var data = new byte[4];
            int read = stream.Read(data, 0, 4);
            //Debug.Assert(read == 4);
            return BitConverter.ToUInt32(data, 0);
        }

        public static ulong ReadValueU64(this Stream stream)
        {
            var data = new byte[8];
            int read = stream.Read(data, 0, 8);
            // Debug.Assert(read == 8);
            return BitConverter.ToUInt64(data, 0);
        }

        public static float ReadValueF32(this Stream stream)
        {
            var data = new byte[4];
            int read = stream.Read(data, 0, 4);
            //  Debug.Assert(read == 4);
            return BitConverter.ToSingle(data, 0);
        }

        internal static string ReadStringInternalDynamic(this Stream stream, Encoding encoding, char end)
        {
            int characterSize = encoding.GetByteCount("e");
            // Debug.Assert(characterSize == 1 || characterSize == 2 || characterSize == 4);
            string characterEnd = end.ToString(CultureInfo.InvariantCulture);

            int i = 0;
            var data = new byte[128 * characterSize];

            while (true)
            {
                if (i + characterSize > data.Length)
                {
                    Array.Resize(ref data, data.Length + (128 * characterSize));
                }

                int read = stream.Read(data, i, characterSize);
                // Debug.Assert(read == characterSize);

                if (encoding.GetString(data, i, characterSize) == characterEnd)
                {
                    break;
                }

                i += characterSize;
            }

            if (i == 0)
            {
                return "";
            }

            return encoding.GetString(data, 0, i);
        }

        public static string ReadStringAscii(this Stream stream)
        {
            return stream.ReadStringInternalDynamic(Encoding.ASCII, '\0');
        }

        public static string ReadStringUnicode(this Stream stream)
        {
            return stream.ReadStringInternalDynamic(Encoding.UTF8, '\0');
        }
    }
    internal enum KeyValueType : byte
    {
        None = 0,
        String = 1,
        Int32 = 2,
        Float32 = 3,
        Pointer = 4,
        WideString = 5,
        Color = 6,
        UInt64 = 7,
        End = 8,
    }
    internal class KeyValue
    {
        private static readonly KeyValue _Invalid = new KeyValue();
        public string Name = "<root>";
        public KeyValueType Type = KeyValueType.None;
        public object Value;
        public bool Valid;

        public List<KeyValue> Children = null;

        public KeyValue this[string key]
        {
            get
            {
                if (this.Children == null)
                {
                    return _Invalid;
                }

                var child = this.Children.SingleOrDefault(
                    c => c.Name.ToLowerInvariant() == key.ToLowerInvariant());

                if (child == null)
                {
                    return _Invalid;
                }

                return child;
            }
        }

        public string AsString(string defaultValue)
        {
            if (this.Valid == false)
            {
                return defaultValue;
            }

            if (this.Value == null)
            {
                return defaultValue;
            }

            return this.Value.ToString();
        }

        public int AsInteger(int defaultValue)
        {
            if (this.Valid == false)
            {
                return defaultValue;
            }

            switch (this.Type)
            {
                case KeyValueType.String:
                case KeyValueType.WideString:
                    {
                        int value;
                        if (int.TryParse((string)this.Value, out value) == false)
                        {
                            return defaultValue;
                        }
                        return value;
                    }

                case KeyValueType.Int32:
                    {
                        return (int)this.Value;
                    }

                case KeyValueType.Float32:
                    {
                        return (int)((float)this.Value);
                    }

                case KeyValueType.UInt64:
                    {
                        return (int)((ulong)this.Value & 0xFFFFFFFF);
                    }
            }

            return defaultValue;
        }

        public float AsFloat(float defaultValue)
        {
            if (this.Valid == false)
            {
                return defaultValue;
            }

            switch (this.Type)
            {
                case KeyValueType.String:
                case KeyValueType.WideString:
                    {
                        float value;
                        if (float.TryParse((string)this.Value, out value) == false)
                        {
                            return defaultValue;
                        }
                        return value;
                    }

                case KeyValueType.Int32:
                    {
                        return (int)this.Value;
                    }

                case KeyValueType.Float32:
                    {
                        return (float)this.Value;
                    }

                case KeyValueType.UInt64:
                    {
                        return (ulong)this.Value & 0xFFFFFFFF;
                    }
            }

            return defaultValue;
        }

        public bool AsBoolean(bool defaultValue)
        {
            if (this.Valid == false)
            {
                return defaultValue;
            }

            switch (this.Type)
            {
                case KeyValueType.String:
                case KeyValueType.WideString:
                    {
                        int value;
                        if (int.TryParse((string)this.Value, out value) == false)
                        {
                            return defaultValue;
                        }
                        return value != 0;
                    }

                case KeyValueType.Int32:
                    {
                        return ((int)this.Value) != 0;
                    }

                case KeyValueType.Float32:
                    {
                        return ((int)((float)this.Value)) != 0;
                    }

                case KeyValueType.UInt64:
                    {
                        return ((ulong)this.Value) != 0;
                    }
            }

            return defaultValue;
        }

        public override string ToString()
        {
            if (this.Valid == false)
            {
                return "<invalid>";
            }

            if (this.Type == KeyValueType.None)
            {
                return this.Name;
            }

            return string.Format("{0} = {1}", this.Name, this.Value);
        }

        public static KeyValue LoadAsBinary(string path)
        {
            if (File.Exists(path) == false)
            {
                return null;
            }

            try
            {
                var input = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var kv = new KeyValue();

                if (kv.ReadAsBinary(input) == false)
                {
                    return null;
                }

                input.Close();
                return kv;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public bool ReadAsBinary(Stream input)
        {
            this.Children = new List<KeyValue>();

            try
            {
                while (true)
                {
                    var type = (KeyValueType)input.ReadValueU8();

                    if (type == KeyValueType.End)
                    {
                        break;
                    }

                    var current = new KeyValue
                    {
                        Type = type,
                        Name = input.ReadStringUnicode(),
                    };

                    switch (type)
                    {
                        case KeyValueType.None:
                            {
                                current.ReadAsBinary(input);
                                break;
                            }

                        case KeyValueType.String:
                            {
                                current.Valid = true;
                                current.Value = input.ReadStringUnicode();
                                break;
                            }

                        case KeyValueType.WideString:
                            {
                                throw new FormatException("wstring is unsupported");
                            }

                        case KeyValueType.Int32:
                            {
                                current.Valid = true;
                                current.Value = input.ReadValueS32();
                                break;
                            }

                        case KeyValueType.UInt64:
                            {
                                current.Valid = true;
                                current.Value = input.ReadValueU64();
                                break;
                            }

                        case KeyValueType.Float32:
                            {
                                current.Valid = true;
                                current.Value = input.ReadValueF32();
                                break;
                            }

                        case KeyValueType.Color:
                            {
                                current.Valid = true;
                                current.Value = input.ReadValueU32();
                                break;
                            }

                        case KeyValueType.Pointer:
                            {
                                current.Valid = true;
                                current.Value = input.ReadValueU32();
                                break;
                            }

                        default:
                            {
                                throw new FormatException();
                            }
                    }

                    if (input.Position >= input.Length)
                    {
                        throw new FormatException();
                    }

                    this.Children.Add(current);
                }

                this.Valid = true;
                return input.Position == input.Length;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
