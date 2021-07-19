using Discord;
using Newtonsoft.Json;
using p4gpc.rpc.Configuration;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory.Sources;
using Reloaded.Mod.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace p4gpc.rpc
{
    public class RPC
    {
        private readonly long CLIENT_ID = 808204087828807700;
        private System.Threading.Timer _timer;
        private ActivityManager _activityManager;
        private Discord.Discord _discord;
        private ILogger _logger;
        private bool _rpcEnabled;
        private long _startTime;
        private long _battleStartTime = 0;
        private string _modDirectory;
        private Field[] _fields;
        private int _tickCounter = 0;
        private Field _previousField;

        // For Reading/Writing Memory
        private IMemory _memory = new Memory();
        // Process base address; this is normally a constant 0x400000 unless ASLR gets suddenly enabled.
        private int _baseAddress;

        private static readonly string[] TimeNames = { "Early Morning", "Morning", "Lunchtime", "Daytime", "After School", "Evening" };
        private static readonly string[] Weather = { "Clear", "Rain", "Cloudy", "Thunderstorm", "Snow", "Fog" }; // need to check thunderstorm, snow and fog
        private static readonly string[] MemberNames = { "", "Protagonist", "Yosuke", "Chie", "Yukiko", "Rise", "Kanji", "Naoto", "Teddie" };


        // Current mod configuration
        public Config Configuration { get; set; }

        public RPC(ILogger logger, Config configuration, string modDirectory)
        {
            Configuration = configuration;
            _logger = logger;
            _logger.WriteLine("[RPC] Initialising RPC");
            _modDirectory = modDirectory;
            using var thisProcess = Process.GetCurrentProcess();
            _baseAddress = thisProcess.MainModule.BaseAddress.ToInt32();

            // Activate the get weather hook
            //_weatherHook = new WeatherHook(_logger, hooks, _baseAddress);

            LoadFields();
            // If it failed to load fields, the mod can't work
            if(_fields != null)
            {
                try
                {

                // Activate discord stuff
                _rpcEnabled = true;
                _startTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeMilliseconds();

                _discord = new Discord.Discord(CLIENT_ID, (UInt64)CreateFlags.NoRequireDiscord);
                LogDebug("Discord client created");
                _discord.SetLogHook(LogLevel.Debug, LogProblemsFunction);
                _activityManager = _discord.GetActivityManager();
                _activityManager.RegisterSteam(1113000);
                _timer = new System.Threading.Timer(OnTick, null, 0, 5000);

                _logger.WriteLine("[RPC] RPC activated");

                }
                catch(Exception e)
                {
                    _logger.WriteLine($"[RPC] Error initialising RPC please make sure Discord is running: {e.Message}", System.Drawing.Color.Red);
                    LogDebug(e.StackTrace);
                }
            } 
            else
            {
                _logger.WriteLine($"[RPC] Error initialising RPC: Couldn't load fields. Make sure fields.json exists in the mod's folder", System.Drawing.Color.Red);
            }
        }

        public void LogProblemsFunction(LogLevel level, string message)
        {
            LogDebug($"Discord Error:{level} - {message}");
        }

        private void OnTick(object state)
        {
            if (_rpcEnabled)
            {
                UpdateActivity();
                _discord.RunCallbacks();
            }
        }

        private void UpdateActivity()
        {
            // Get the current location
            _memory.SafeRead((IntPtr)_baseAddress + 7316824, out short[] field, 3);
            // Get the current time
            _memory.SafeRead((IntPtr)_baseAddress + 77454494, out short time);
            // Get the day number
            _memory.SafeRead((IntPtr)_baseAddress + 0x49DDC9C, out short dayOfYear);
            DateTime date = new DateTime(2011, 4, 1).AddDays(dayOfYear);

            // Check if we're in a dungeon (Flag 3075)
            //_memory.SafeRead((IntPtr)_baseAddress + 77453020, out byte inDungeonByte);
            //// Get the 3rd bit from the byte that the flag is in (black magic explained here https://stackoverflow.com/questions/4854207/get-a-specific-bit-from-byte)
            //var inDungeon = (inDungeonByte & (1 << 4 - 1)) != 0;
            //LogDebug($"In dungeon: {inDungeon}");
            var fieldMajor = field[0];
            var fieldMinor = field[2]; // field[1] is just a buffer
            var foundField = _fields.SingleOrDefault(field => field.major == fieldMajor && field.minor == fieldMinor);

            string description = "";
            string state = "";

            // Player is on the after battle screen (hopefully)
            if (_previousField.inBattle && foundField.name == null)
            {
                description = "Resting after a successful battle";
                state = _previousField.state;
                foundField = _previousField;
                _tickCounter++;
            }

            
            // Set variables for activity info
            var name = foundField.name;
            if (foundField.name == null && description != "Resting after a successful battle")
            {
                name = _previousField.name != null ? _previousField.name : "a mysterious place";
                foundField = _previousField;
            }

            // Only set state and description if they weren't set to resting after a battle before
            if(description != "Resting after a successful battle")
            {
                state = foundField.state;
                description = foundField.description != null ? foundField.description : $"Exploring {name}";
            }


            // Keep track of how long we've been in the current field
            if (_previousField.major == foundField.major && _previousField.minor == foundField.minor)
            {
                _tickCounter++;
            }
            // Don't change previous field if you're on after battle screen
            else if (description != "Resting after a successful battle")
            {
                _previousField = foundField;
                _tickCounter = 0;

            }

            // Set the battle start time if necessary
            if (_battleStartTime == 0 && foundField.inBattle) _battleStartTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeMilliseconds();
            else if (_battleStartTime != 0 && !foundField.inBattle) _battleStartTime = 0;

            // Set the state to include level and/or party members in the tv world
            if (foundField.inTVWorld || foundField.inBattle)
            {
                // Get current protag level
                _memory.SafeRead((IntPtr)_baseAddress + 77451322, out short level);
                // Get who is in the party
                _memory.SafeRead((IntPtr)_baseAddress + 0x49DC3C4, out short[] inParty, 3);

                var party = inParty.Where(x => x != 0).ToArray();
                var partyString = "";
                for (int i = 0; i < party.Length; i++)
                {
                    if (i + 1 == party.Length && party.Length > 1)
                    {
                        partyString += $" and {MemberNames[party[i]]}";
                    }
                    else if (i == 0)
                    {
                        partyString += $"With {MemberNames[party[i]]}";
                    }
                    else
                    {
                        partyString += $", {MemberNames[party[i]]}";
                    }
                }

                if (foundField.state != null)
                {
                    switch (_tickCounter % 3)
                    {
                        case 2:
                            state = foundField.state;
                            break;
                        case 1:
                            state = partyString != "" ? partyString : "Solo";
                            break;
                        case 0:
                            state = $"Protagonist level {level}";
                            break;
                    }
                }
                else
                {
                    switch (_tickCounter % 2)
                    {
                        case 1:
                            state = partyString != "" ? partyString : "Solo";
                            break;
                        case 0:
                            state = $"Protagonist level {level}";
                            break;
                    }
                }
            }

            // Sets the weather to display (for images)
            string weather = dayOfYear < 275 ? "" : "_winter";
            if (time == 5) weather += "_night";

            LogDebug(foundField.imageKey != null ? $"{foundField.imageKey}{(foundField.imageKey != "logo" && !foundField.inTVWorld && !foundField.ignoreWeather ? weather : "")}" : "logo");

            // Update the activity
            var activity = new Discord.Activity
            {
                Details = description, // Top text
                State = state != null ? state : $"{date.ToString(Configuration.DateFormat)} {TimeNames[time]}", // Bottom text
                Timestamps =
                  {
                      Start = _battleStartTime != 0 && Configuration.BattleTime ? _battleStartTime : _startTime,
                  },
                Assets =
                  {
                      //LargeImage = foundField.imageKey != null ? $"{foundField.imageKey}{(foundField.imageKey != "logo" ? "_" + weather : "")}" : "logo", // Larger Image Asset Key
                      LargeImage = foundField.imageKey != null ? $"{foundField.imageKey}{(foundField.imageKey != "logo" && !foundField.inTVWorld && !foundField.ignoreWeather ? weather : "")}" : "logo", // Larger Image Asset Key
                      LargeText = foundField.imageText != null ? foundField.imageText: "Persona 4 Golden Logo", // Large Image Tooltip
                      //SmallImage = "entrance", // Small Image Asset Key
                      //SmallText = "Test image", // Small Image Tooltip
                  },
                Instance = false,
            };

            _activityManager.UpdateActivity(activity, (result) =>
            {
                if (result == Result.Ok)
                {
                    LogDebug("Successfully updated activity!");
                    LogDebug($"Field Major: {fieldMajor}");
                    LogDebug($"Field Minor: {fieldMinor}");
                }
                else
                {
                    LogDebug("Problem updating activity :(");
                }

            });
        }

        private void ClearActivity()
        {
            _activityManager.ClearActivity((result) =>
            {
                if (result == Discord.Result.Ok)
                {
                    LogDebug("Successfully cleared activity!");
                }
                else
                {
                    LogDebug("Failed to clear activity :(");
                }
            });
        }

        private void LoadFields()
        {
            try
            {
                using (StreamReader file = File.OpenText(Path.Combine(_modDirectory, "fields.json")))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    _fields = (Field[])serializer.Deserialize(file, typeof(Field[]));
                }
            }
            catch (Exception e)
            {
                _logger.WriteLine($"[RPC] Error loading field information, RPC will not be activated. Please make sure fields.json exists in the mod's folder.", System.Drawing.Color.Red);
            }

        }

        struct Field
        {
            public short major;
            public short minor;
            public string name;
            public string description;
            public string state;
            public bool inTVWorld;
            public bool inBattle;
            public string imageKey;
            public string imageText;
            public bool ignoreWeather;
        }

        private void LogDebug(string message)
        {
            if (Configuration.Debug)
            {
                if(_logger != null)
                {
                    _logger.WriteLine($"[RPC] {message}");
                }
            }
        }

        public void Suspend()
        {
            _rpcEnabled = false;
            ClearActivity();
            if(_logger != null)
            {
                _logger.WriteLine("[RPC] RPC suspended");
            }
        }

        public void Resume()
        {
            _startTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeMilliseconds();
            _rpcEnabled = true;
            if(_logger != null)
            {
                _logger.WriteLine("[RPC] RPC resumed");
            }
        }

        public void Unload()
        {
            _timer.Dispose();
            _discord.Dispose();
            if(_logger != null)
            {
                _logger.WriteLine("[RPC] RPC unloaded");
            }
        }
    }

}
