// PlayOut Co-Op - headless front for the Nucleus Co-op engine.
// Copyright (C) 2026 PlayOut Co-Op contributors. Derived from Nucleus Co-op (SplitScreen-Me), GPL-3.
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. See LICENSE.

using Nucleus.Gaming.Coop;
using Nucleus.Gaming.Coop.InputManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Nucleus.Gaming.Headless
{
    /// <summary>
    /// One co-op session: what Nucleus's Play button does (GameProfile.InitializeHandlerStartup, minus the setup
    /// screen), plus a Stop() that is the Stop button / Ctrl+E and an Ended notification.
    /// </summary>
    public sealed class HeadlessSession
    {
        private readonly ManualResetEvent endedEvent = new ManualResetEvent(false);
        private readonly ManualResetEvent playFinishedEvent = new ManualResetEvent(false);
        private volatile bool stopRequested;

        public GenericGameHandler Handler { get; private set; }
        public UserGameInfo UserGame { get; private set; }
        public GameProfile Profile { get; private set; }

        /// <summary>Raised once Play() has returned (argument: error message, empty = success).</summary>
        public event Action<string> PlayFinished;
        /// <summary>Raised once End() has finished restoring everything. Argument: "exited", "stopped" or "error".</summary>
        public event Action<string> Ended;

        public bool HasEnded => Handler?.HasEnded ?? false;
        /// <summary>Play's error, or the last error/warning dialog text when the launch was aborted.</summary>
        public string Error => !string.IsNullOrEmpty(Handler?.error) ? Handler.error : (EndReason == "error" ? EngineUI.LastMessage : null);
        public string EndReason { get; private set; }

        /// <summary>PIDs of the game processes Nucleus attached to, in instance order.</summary>
        public List<int> Pids => Handler?.attachedIds.ToList() ?? new List<int>();

        /// <summary>
        /// Starts the session. Returns immediately: Nucleus runs Play() on its own STA thread (and the window update
        /// tick on another thread). Throws if GenericGameHandler.Initialize refuses.
        /// </summary>
        public static HeadlessSession Start(UserGameInfo userGame, GameProfile profile)
        {
            if (GenericGameHandler.IsRunning || GenericGameHandler.Instance != null && !GenericGameHandler.Instance.HasEnded && GenericGameHandler.Instance.profile != null)
            {
                throw new InvalidOperationException("A session is already running in this process.");
            }

            HeadlessSession session = new HeadlessSession { UserGame = userGame, Profile = profile };

            GameProfile.Game = userGame.Game;
            GameProfile.GameInfo = userGame;

            IGameHandler handler = GameManager.Instance.MakeHandler(userGame.Game);
            GenericGameHandler generic = (GenericGameHandler)handler;
            session.Handler = generic;

            generic.PlayFinished += session.OnPlayFinished;
            handler.Ended += session.OnEnded;

            GameProfile.I_GameHandler = handler;

            if (!handler.Initialize(userGame, profile, handler))
            {
                throw new InvalidOperationException("GenericGameHandler.Initialize failed (no game info).");
            }

            return session;
        }

        private void OnPlayFinished(string error)
        {
            playFinishedEvent.Set();
            try { PlayFinished?.Invoke(error ?? string.Empty); } catch { }
        }

        private void OnEnded()
        {
            // End() before Play() returned, without Stop() = Nucleus aborted the launch (e.g. UnlockGameFiles failed).
            bool abortedDuringPlay = !playFinishedEvent.WaitOne(0);
            EndReason = stopRequested ? "stopped" : (!string.IsNullOrEmpty(Handler?.error) || abortedDuringPlay ? "error" : "exited");
            endedEvent.Set();
            try { Ended?.Invoke(EndReason); } catch { }
        }

        /// <summary>
        /// Ends the session like the Stop button: GenericGameHandler.End(true), which kills the instances and restores
        /// files, registry, windows, taskbar and backups. Input lock (End key) is released first, because Nucleus
        /// ignores the Stop button while input is locked. Blocks until End() has run (or the timeout elapses).
        /// </summary>
        public bool Stop(int timeoutMs = 60000)
        {
            stopRequested = true;

            if (Handler == null || Handler.HasEnded)
            {
                return true;
            }

            if (LockInputRuntime.IsLocked)
            {
                try { LockInputRuntime.Unlock(Handler.CurrentGameInfo?.ProtoInput.FreezeExternalInputWhenInputNotLocked ?? false, Handler.CurrentGameInfo?.ProtoInput); } catch { }
            }

            Thread t = new Thread(() =>
            {
                try { Handler.End(true); } catch (Exception ex) { EngineUI.Log("ERROR", "End() threw: " + ex); }
            });
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.STA);
            t.Start();

            return endedEvent.WaitOne(timeoutMs);
        }

        public bool WaitForPlayFinished(int timeoutMs) => playFinishedEvent.WaitOne(timeoutMs);

        public bool WaitForEnd(int timeoutMs) => endedEvent.WaitOne(timeoutMs);
    }
}
