// PlayOut Co-Op - headless front for the Nucleus Co-op engine.
// Copyright (C) 2026 PlayOut Co-Op contributors. Derived from Nucleus Co-op (SplitScreen-Me), GPL-3.
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. See LICENSE.
//
// Builds the GameProfile that Nucleus's setup screen would build (GameProfile.InitializeDefault, then
// BoundsFunctions.AddPlayer for each dropped device, GameProfile.UpdateProfilePlayerIdentity, the options page,
// and finally GameProfile.CleanClone, which is what GameProfile.InitializeHandlerStartup hands to the handler).

using Nucleus.Gaming.Cache;
using Nucleus.Gaming.Coop;
using Nucleus.Gaming.Headless;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace Nucleus.Gaming.Headless
{
    /// <summary>One player of a headless launch: a device placed in one slot of one screen's split.</summary>
    public sealed class HeadlessPlayerSlot
    {
        /// <summary>From the HeadlessDevices.Create*Player factories.</summary>
        public PlayerInfo Player;
        /// <summary>Index into the UserScreen array passed to GameProfile.CreateHeadless.</summary>
        public int ScreenIndex;
        /// <summary>Index into that screen's split (UserScreen.SubScreensBounds order: row-major).</summary>
        public int Slot;
        /// <summary>Optional; default = Nucleus's nickname for that player index (Settings.ini [ControllerMapping]).</summary>
        public string Nickname;
        /// <summary>Optional Steam ID (Goldberg); default = Nucleus's (handler PlayerSteamIDs or the player cache).</summary>
        public long? SteamID;
    }

    /// <summary>A handler option as the options page would present it.</summary>
    public sealed class HeadlessOptionInfo
    {
        public string Key;
        public string Name;
        public string Description;
        /// <summary>Selectable values (list or enum options); empty for free values.</summary>
        public List<string> Values = new List<string>();
        /// <summary>What Nucleus uses when the user does not change it.</summary>
        public string Default;
        /// <summary>"list", "enum", "bool", "number", "text" or "value".</summary>
        public string Type;
        public bool Hidden;
    }

    public static class HeadlessOptions
    {
        public static HeadlessOptionInfo Describe(GameOption opt)
        {
            HeadlessOptionInfo info = new HeadlessOptionInfo
            {
                Key = opt.Key,
                Name = opt.Name,
                Description = opt.Description,
                Hidden = opt.Hidden,
            };

            if (opt.Value is Enum e)
            {
                info.Type = "enum";
                foreach (object v in Enum.GetValues(e.GetType())) info.Values.Add(v.ToString());
                info.Default = e.ToString();
            }
            else if (opt.List != null && opt.List.Count != 0)
            {
                info.Type = "list";
                foreach (object v in opt.List) info.Values.Add(Convert.ToString(v, CultureInfo.InvariantCulture));
                // PlayerOptionsControl selects List[0] unless the stored value is one of the items.
                object def = opt.List[0]; // GameOption.DefaultValue is not used by Nucleus's options page
                info.Default = Convert.ToString(def, CultureInfo.InvariantCulture);
            }
            else if (opt.Value is bool b)
            {
                info.Type = "bool";
                info.Default = b ? "true" : "false";
            }
            else if (opt.Value is int || opt.Value is double)
            {
                info.Type = "number";
                info.Default = Convert.ToString(opt.Value, CultureInfo.InvariantCulture);
            }
            else if (opt.Value is GameOptionValue gv)
            {
                info.Type = "value";
                info.Default = gv.ToString();
            }
            else
            {
                info.Type = "text";
                info.Default = Convert.ToString(opt.Value, CultureInfo.InvariantCulture) ?? "";
            }

            return info;
        }

        private static bool ContainsItem(IList list, object value)
        {
            foreach (object o in list)
            {
                if (Equals(o, value) || string.Equals(Convert.ToString(o, CultureInfo.InvariantCulture), Convert.ToString(value, CultureInfo.InvariantCulture), StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The Options dictionary the options page would produce (same value types: the selected list item object,
        /// the enum value, bool, decimal for numbers, string for text), with optional string overrides by key.
        /// Unknown keys in <paramref name="overrides"/> are ignored; values that match no list item throw.
        /// </summary>
        public static Dictionary<string, object> Resolve(GenericGameInfo game, IDictionary<string, string> overrides)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            foreach (GameOption opt in game.Options)
            {
                string ov = null;
                bool has = overrides != null && overrides.TryGetValue(opt.Key, out ov) && ov != null;

                if (opt.Value is Enum e)
                {
                    result[opt.Key] = has ? Enum.Parse(e.GetType(), ov, true) : e;
                }
                else if (opt.List != null && opt.List.Count != 0)
                {
                    object chosen = opt.List[0];
                    if (has)
                    {
                        object match = null;
                        foreach (object o in opt.List)
                        {
                            if (string.Equals(Convert.ToString(o, CultureInfo.InvariantCulture), ov, StringComparison.OrdinalIgnoreCase))
                            {
                                match = o;
                                break;
                            }
                        }

                        if (match == null)
                        {
                            throw new ArgumentException($"Option '{opt.Key}': '{ov}' is not one of: {string.Join(", ", opt.List.Cast<object>())}");
                        }

                        chosen = match;
                    }

                    result[opt.Key] = chosen;
                }
                else if (opt.Value is bool b)
                {
                    result[opt.Key] = has ? bool.Parse(ov) : b;
                }
                else if (opt.Value is int || opt.Value is double)
                {
                    // NumericUpDown.Value is a decimal, which is what the options page stores.
                    result[opt.Key] = has ? decimal.Parse(ov, CultureInfo.InvariantCulture) : (decimal)(int)Convert.ToDouble(opt.Value, CultureInfo.InvariantCulture);
                }
                else
                {
                    result[opt.Key] = has ? ov : opt.Value;
                }
            }

            return result;
        }

        private static object Find(IList list, object value)
        {
            foreach (object o in list)
            {
                if (Equals(o, value) || string.Equals(Convert.ToString(o, CultureInfo.InvariantCulture), Convert.ToString(value, CultureInfo.InvariantCulture), StringComparison.Ordinal))
                {
                    return o;
                }
            }
            return list[0];
        }
    }
}

namespace Nucleus.Gaming.Coop
{
    public partial class GameProfile
    {
        /// <summary>
        /// PlayOut Co-Op: the split rectangles Nucleus's UserScreen math gives a monitor for a layout, in slot order.
        /// </summary>
        public static List<Rectangle> GetSplit(UserScreen screen)
        {
            return screen.SubScreensBounds.Keys.ToList();
        }

        /// <summary>
        /// PlayOut Co-Op: builds, without the setup screen, the profile Nucleus passes to GenericGameHandler.Initialize.
        /// <paramref name="screens"/> are the UserScreens (HeadlessDevices.GetUserScreens) with Type already set to the
        /// wanted layout; <paramref name="players"/> are in PlayerID (instance) order. Sets the GameProfile statics
        /// (Game, GameInfo, Instance) exactly like the UI does before Play.
        /// </summary>
        public static GameProfile CreateHeadless(UserGameInfo userGame, UserScreen[] screens, IList<HeadlessPlayerSlot> players, IDictionary<string, string> optionOverrides)
        {
            if (userGame?.Game == null)
            {
                throw new ArgumentNullException(nameof(userGame));
            }

            if (players == null || players.Count == 0)
            {
                throw new ArgumentException("At least one player is required.", nameof(players));
            }

            Game = userGame.Game;
            GameInfo = userGame;

            if (PlayersIdentityCache.DefaultNicknames == null)
            {
                PlayersIdentityCache.LoadPlayersIdentityCache();
            }

            GameProfile prof = new GameProfile();
            prof.InitializeDefault(); // Instance = prof, Reset(), options from Game.Options

            prof.screens = screens.ToList();
            prof.deviceList = new List<PlayerInfo>();

            AssignedDevices.Clear();
            DevicesToMerge.Clear();
            TotalAssignedPlayers = 0;

            foreach (UserScreen s in screens)
            {
                s.PlayerOnScreen = 0;
            }

            for (int i = 0; i < players.Count; i++)
            {
                HeadlessPlayerSlot slot = players[i];
                PlayerInfo player = slot.Player ?? throw new ArgumentException($"Player {i}: no PlayerInfo.");

                if (slot.ScreenIndex < 0 || slot.ScreenIndex >= screens.Length)
                {
                    throw new ArgumentException($"Player {i}: screen {slot.ScreenIndex} does not exist ({screens.Length} screens).");
                }

                UserScreen screen = screens[slot.ScreenIndex];
                List<KeyValuePair<Rectangle, RectangleF>> split = screen.SubScreensBounds.ToList();

                if (slot.Slot < 0 || slot.Slot >= split.Count)
                {
                    throw new ArgumentException($"Player {i}: slot {slot.Slot} does not exist in layout {screen.Type} ({split.Count} slots).");
                }

                Rectangle destMonitorBounds = split[slot.Slot].Key;
                RectangleF destEditBounds = split[slot.Slot].Value;

                if (AssignedDevices.Any(p => p.MonitorBounds == destMonitorBounds))
                {
                    throw new ArgumentException($"Player {i}: screen {slot.ScreenIndex} slot {slot.Slot} is already taken.");
                }

                // BoundsFunctions.AddPlayer
                AssignedDevices.Add(player);
                screen.PlayerOnScreen++;
                TotalAssignedPlayers++;

                player.IsInputUsed = true;
                player.Owner = screen;
                player.ScreenIndex = slot.ScreenIndex;
                player.MonitorBounds = destMonitorBounds;
                player.EditBounds = destEditBounds;
                player.SourceEditBounds = destEditBounds;
                player.ScreenPriority = screen.priority;
                player.DisplayIndex = screen.DisplayIndex;

                prof.deviceList.Add(player);

                try
                {
                    UpdateProfilePlayerIdentity(player);
                }
                catch (FormatException)
                {
                    player.Nickname = player.Nickname ?? PlayersIdentityCache.GetNicknameAt(i);
                    player.SteamID = PlayersIdentityCache.DefaultSteamIds[i];
                }

                if (!string.IsNullOrEmpty(slot.Nickname))
                {
                    player.Nickname = slot.Nickname;
                }

                if (slot.SteamID.HasValue)
                {
                    player.SteamID = slot.SteamID.Value;
                }
            }

            prof.options = HeadlessOptions.Resolve(Game, optionOverrides);
            Ready = true;

            return CleanClone(prof);
        }
    }
}
