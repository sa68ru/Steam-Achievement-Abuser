﻿using SAM.API;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.XPath;
using Newtonsoft.Json;

namespace Steam_Achievement_Abuser
{
    class Program
    {
        private static Client _SteamClient = null;
        private static List<GameInfo> _Games = new List<GameInfo>();

        static void Main()
        {
            Console.Title = "Steam Achievement Abuser";
            Console.WriteLine("Welcome to Steam Achievement Abuser by sa68ru.xyz (Cache added by KaiFu_)");
            Console.WriteLine("Based on: https://github.com/gibbed/SteamAchievementManager");
            Console.WriteLine("Init...");
            try
            {
                _SteamClient = new Client();
                if (_SteamClient.Initialize(0) == false)  
                    return;             
            }
            catch (DllNotFoundException)
            {
                throw;
            }
            AddGames();
            Console.WriteLine($"Found {_Games.Count()} games...");
            string data;
            cache cach = new cache();
            if (File.Exists("cache.json"))
            {
                data = File.ReadAllText("cache.json");
                cach = JsonConvert.DeserializeObject<cache>(data);
                Console.WriteLine($"{cach.names.Count} already done...");
                List<GameInfo> toremovegames = new List<GameInfo>();
                foreach(var name in cach.names)
                {
                    foreach(var game in _Games)
                    {
                        if (name == game.Name)
                            toremovegames.Add(game);
                    }
                }
                foreach(var game in toremovegames)
                {
                    _Games.Remove(game);
                }
                if(_Games.Count > 0)
                {
                    foreach(var game in _Games)
                    {
                        cach.names.Add(game.Name);
                    }
                    File.WriteAllText("cache.json", JsonConvert.SerializeObject(cach));
                    Console.WriteLine("Added new Games to cache...");
                }
                Console.WriteLine($"{_Games.Count} remain...");
            }
            else
            {
                File.Create("cache.json").Close();
                foreach(var game in _Games)
                {
                    cach.names.Add(game.Name);
                }
                File.WriteAllText("cache.json",JsonConvert.SerializeObject(cach));
            }
            Console.WriteLine("");
            Console.WriteLine("Press key for start abuse...");
            Console.ReadKey();
            if(_Games.Count > 0)
                StartAbuse();
            else
                Console.WriteLine("All Games already Done...");
            Console.ReadKey();
        }
        static void StartAbuse()
        {
            Console.WriteLine("Starting abuse...");

            int i = 1;
            foreach (var Game in _Games)
            {
                ProcessStartInfo ps = new ProcessStartInfo("Steam Achievement Abuser App.exe", Game.Id.ToString());
                ps.CreateNoWindow = true;
                ps.UseShellExecute = false;
                Console.WriteLine($"{i}/{_Games.Count()} | {Game.Name}");
                using (Process p = Process.Start(ps)) 
                    p.WaitForExit();
                i++;
            }
            Console.WriteLine("");
            Console.WriteLine("Done!");
        }
        static void AddGames()
        {
            Console.WriteLine("Downloading base...");
            var pairs = new List<KeyValuePair<uint, string>>();
            byte[] bytes;
            using (var downloader = new WebClient())
            {
                bytes = downloader.DownloadData(new Uri(string.Format("http://gib.me/sam/games.xml")));
            }
            using (var stream = new MemoryStream(bytes, false))
            {
                var document = new XPathDocument(stream);
                var navigator = document.CreateNavigator();
                var nodes = navigator.Select("/games/game");
                while (nodes.MoveNext())
                {
                    string type = nodes.Current.GetAttribute("type", "");
                    if (type == string.Empty)
                    {
                        type = "normal";
                    }
                    pairs.Add(new KeyValuePair<uint, string>((uint)nodes.Current.ValueAsLong, type));
                }
                foreach (var kv in pairs)
                {
                    AddGame(kv.Key, kv.Value);
                }
            }
        }
        private static void AddGame(uint id, string type)
        {
            if (_Games.Any(i => i.Id == id))     
                return;
            
            if (!_SteamClient.SteamApps003.IsSubscribedApp(id))        
                return;
            
            var info = new GameInfo(id, type);
            info.Name = _SteamClient.SteamApps001.GetAppData(info.Id, "name");
            if (info.Type == "demo" || info.Type == "mod" || info.Type == "junk")  
                return;
            _Games.Add(info);
        }
    }
    internal class GameInfo
    {
        private string _Name;
        public uint Id;
        public string Type;
        public string Name
        {
            get { return _Name; }
            set { _Name = value ?? "App " + this.Id.ToString(CultureInfo.InvariantCulture); }
        }
        public GameInfo(uint id, string type)
        {
            this.Id = id;
            this.Type = type;
            this.Name = null;
        }
    }
    internal class cache
    {
        public List<string> names = new List<string>();
    }
}
