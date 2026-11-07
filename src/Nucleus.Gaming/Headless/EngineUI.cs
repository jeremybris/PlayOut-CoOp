// PlayOut Co-Op - headless front for the Nucleus Co-op engine.
// Copyright (C) 2026 PlayOut Co-Op contributors. Derived from Nucleus Co-op (SplitScreen-Me), GPL-3.
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. See LICENSE.

using Nucleus.Gaming.Coop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Nucleus.Gaming.Headless
{
    /// <summary>What kind of user interaction the engine is asking for.</summary>
    public enum EngineUiKind
    {
        /// <summary>A System.Windows.Forms.MessageBox (info, warning, error or a Yes/No question).</summary>
        MessageBox,
        /// <summary>Nucleus's own themed message box (NucleusMessageBox.Show); always informational.</summary>
        NucleusMessageBox,
        /// <summary>A "Press OK when ..." pause (Forms.Prompt). Returning continues the launch.</summary>
        Prompt,
        /// <summary>The user must pick a file (Forms.Prompt with an OpenFileDialog: LauncherExe = "NucleusDefined").</summary>
        SelectFile,
        /// <summary>A handler-defined text prompt (Game.CustomUserGeneralPrompts / PlayerPrompts / InstancePrompts).</summary>
        CustomPrompt,
        /// <summary>The engine could not find the game process and wants the user to pick one (ProcessPicker).</summary>
        ProcessPick,
        /// <summary>An on-screen display notice (Globals.MainOSD.Show). Never blocks.</summary>
        Osd,
    }

    /// <summary>One interaction request. Fields that do not apply to a kind are left at their defaults.</summary>
    public sealed class EngineUiRequest
    {
        public EngineUiKind Kind;
        /// <summary>Where it came from: "File.cs:Member:line" of the Nucleus call site.</summary>
        public string Source;
        public string Title;
        public string Message;

        // MessageBox
        public MessageBoxButtons Buttons = MessageBoxButtons.OK;
        public MessageBoxIcon Icon = MessageBoxIcon.None;
        public MessageBoxDefaultButton DefaultButton = MessageBoxDefaultButton.Button1;

        /// <summary>The answer the engine will use if the callback returns null (the dialog's default button).</summary>
        public DialogResult DefaultResult = DialogResult.OK;

        // SelectFile / CustomPrompt
        public string DefaultValue;
        /// <summary>SelectFile: expected file name (may be null). CustomPrompt: "General", "Player" or "Instance".</summary>
        public string Detail;
        /// <summary>CustomPrompt: prompt index. ProcessPick / CustomPrompt: player index (PlayerID) when known.</summary>
        public int Index = -1;
        public int PlayerIndex = -1;

        // ProcessPick: candidates in Nucleus's own order (matching names first, already attached ones excluded)
        public IList<Process> Candidates;

        // Osd
        public int DurationMs;

        public override string ToString()
        {
            return $"[{Kind}] {Title} {Message}".Trim();
        }
    }

    /// <summary>The answer to an <see cref="EngineUiRequest"/>.</summary>
    public sealed class EngineUiResponse
    {
        public DialogResult Result = DialogResult.OK;
        /// <summary>SelectFile: the chosen path (null = none). CustomPrompt: the entered text (null/empty = none).</summary>
        public string Value;
        /// <summary>ProcessPick: the chosen process (null = none).</summary>
        public Process Process;

        public static EngineUiResponse Of(DialogResult r) => new EngineUiResponse { Result = r };
    }

    /// <summary>
    /// Every place where the Nucleus engine would show a MessageBox, a prompt, a picker or an OSD message goes
    /// through here. With <see cref="Headless"/> false the original Nucleus UI is shown (unchanged behaviour);
    /// with <see cref="Headless"/> true nothing is shown: the request is logged, passed to <see cref="Handler"/>
    /// (if set) and otherwise auto-answered with the dialog's default button / value.
    /// </summary>
    public static class EngineUI
    {
        /// <summary>True = never show Nucleus windows (dialogs, prompts, pickers, OSD, shortcut reminders).</summary>
        public static bool Headless;

        /// <summary>
        /// Optional answer provider. Called on the engine thread that raised the request (often the STA play thread
        /// or a worker thread): it may block to wait for an answer. Return null to take the default.
        /// </summary>
        public static Func<EngineUiRequest, EngineUiResponse> Handler;

        /// <summary>Receives every engine log line: (category, message). Category is e.g. "HANDLER", "UI", "LOG".</summary>
        public static Action<string, string> LogSink;

        /// <summary>Show Nucleus's black split-division / hide-desktop background windows (WPFDiv) even when headless.</summary>
        public static bool AllowBackgroundWindows = true;

        /// <summary>Launch progress: (step, index, total). Step "instance" = GenericGameHandler starts setting up player index.</summary>
        public static Action<string, int, int> ProgressSink;

        public static void Progress(string step, int index, int total)
        {
            Log("PROGRESS", $"{step} {index + 1}/{total}");
            try { ProgressSink?.Invoke(step, index, total); } catch { }
        }

        public static void Log(string category, string message)
        {
            try { LogSink?.Invoke(category, message); } catch { }
        }

        private static string SourceOf(string file, string member, int line)
        {
            return $"{Path.GetFileName(file)}:{member}:{line}";
        }

        private static EngineUiResponse Ask(EngineUiRequest req)
        {
            Log("UI", $"{req.Source} {req}");
            EngineUiResponse resp = null;

            try
            {
                resp = Handler?.Invoke(req);
            }
            catch (Exception ex)
            {
                Log("UI", $"UI handler threw: {ex.Message}");
            }

            if (resp == null)
            {
                resp = new EngineUiResponse { Result = req.DefaultResult, Value = req.DefaultValue };
                if (req.Kind == EngineUiKind.ProcessPick)
                {
                    resp.Process = req.Candidates?.FirstOrDefault();
                }
            }

            Log("UI", $"answer: {resp.Result}{(resp.Value != null ? " value=" + resp.Value : "")}{(resp.Process != null ? " pid=" + resp.Process.Id : "")}");
            return resp;
        }

        /// <summary>The result a user pressing Enter would get: the default button of that button set.</summary>
        public static DialogResult DefaultResultFor(MessageBoxButtons buttons, MessageBoxDefaultButton def)
        {
            DialogResult[] set;
            switch (buttons)
            {
                case MessageBoxButtons.OKCancel: set = new[] { DialogResult.OK, DialogResult.Cancel }; break;
                case MessageBoxButtons.YesNo: set = new[] { DialogResult.Yes, DialogResult.No }; break;
                case MessageBoxButtons.YesNoCancel: set = new[] { DialogResult.Yes, DialogResult.No, DialogResult.Cancel }; break;
                case MessageBoxButtons.RetryCancel: set = new[] { DialogResult.Retry, DialogResult.Cancel }; break;
                case MessageBoxButtons.AbortRetryIgnore: set = new[] { DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore }; break;
                default: set = new[] { DialogResult.OK }; break;
            }

            int idx = def == MessageBoxDefaultButton.Button3 ? 2 : def == MessageBoxDefaultButton.Button2 ? 1 : 0;
            return set[Math.Min(idx, set.Length - 1)];
        }

        /// <summary>Drop-in for System.Windows.Forms.MessageBox.Show(...) used by the engine.</summary>
        public static DialogResult MessageBox(string text, string caption = "", MessageBoxButtons buttons = MessageBoxButtons.OK,
            MessageBoxIcon icon = MessageBoxIcon.None, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1,
            MessageBoxOptions options = 0, bool displayHelpButton = false,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            if (!Headless)
            {
                return System.Windows.Forms.MessageBox.Show(text, caption, buttons, icon, defaultButton, options, displayHelpButton);
            }

            return Ask(new EngineUiRequest
            {
                Kind = EngineUiKind.MessageBox,
                Source = SourceOf(file, member, line),
                Title = caption,
                Message = text,
                Buttons = buttons,
                Icon = icon,
                DefaultButton = defaultButton,
                DefaultResult = DefaultResultFor(buttons, defaultButton),
            }).Result;
        }

        /// <summary>Headless side of Forms.NucleusMessageBox.Show(title, message, formating). Returns true if handled.</summary>
        public static bool NucleusMessageBox(string title, string message,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            if (!Headless)
            {
                return false;
            }

            Ask(new EngineUiRequest
            {
                Kind = EngineUiKind.NucleusMessageBox,
                Source = SourceOf(file, member, line),
                Title = title,
                Message = message,
                Icon = MessageBoxIcon.Information,
                DefaultResult = DialogResult.OK,
            });
            return true;
        }

        /// <summary>Drop-in for "new Forms.Prompt(message).ShowDialog()": a blocking "Press OK when ..." pause.</summary>
        public static void Prompt(string message,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            if (!Headless)
            {
                Forms.Prompt prompt = new Forms.Prompt(message);
                prompt.ShowDialog();
                return;
            }

            Ask(new EngineUiRequest
            {
                Kind = EngineUiKind.Prompt,
                Source = SourceOf(file, member, line),
                Title = "Nucleus",
                Message = message,
                DefaultResult = DialogResult.OK,
            });
        }

        /// <summary>
        /// Drop-in for "new Forms.Prompt(message, true, exeName).ShowDialog()" (choose the launcher exe).
        /// Sets GenericGameHandler.ofdPath exactly like the original form (backslashes doubled), or leaves it null.
        /// </summary>
        public static void SelectFile(string message, string expectedFileName,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            if (!Headless)
            {
                Forms.Prompt prompt = new Forms.Prompt(message, true, expectedFileName);
                prompt.ShowDialog();
                return;
            }

            EngineUiResponse resp = Ask(new EngineUiRequest
            {
                Kind = EngineUiKind.SelectFile,
                Source = SourceOf(file, member, line),
                Title = "Select the launcher executable",
                Message = message,
                Detail = expectedFileName,
                DefaultResult = DialogResult.Cancel,
            });

            if (resp.Result == DialogResult.OK && !string.IsNullOrEmpty(resp.Value))
            {
                GenericGameHandler.ofdPath = resp.Value.Replace(@"\", @"\\");
            }
        }

        /// <summary>
        /// Headless side of Forms.CustomPrompt (handler-defined text input). Returns the value to store in
        /// CustomPromptRuntime.customValue[index] (default: the previous answer, i.e. what the text box is prefilled with).
        /// </summary>
        public static string CustomPrompt(string scope, string promptText, string previousAnswer, int index, PlayerInfo player,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            EngineUiResponse resp = Ask(new EngineUiRequest
            {
                Kind = EngineUiKind.CustomPrompt,
                Source = SourceOf(file, member, line),
                Title = "Handler prompt",
                Message = promptText,
                Detail = scope,
                Index = index,
                PlayerIndex = player?.PlayerID ?? -1,
                DefaultValue = previousAnswer,
                DefaultResult = DialogResult.OK,
            });

            return resp.Result == DialogResult.OK ? resp.Value : null;
        }

        /// <summary>Headless side of ProcessPickerRuntime.LaunchProcessPick: default = first candidate (best name match).</summary>
        public static Process PickProcess(IList<Process> candidates, PlayerInfo player, string message,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            return Ask(new EngineUiRequest
            {
                Kind = EngineUiKind.ProcessPick,
                Source = SourceOf(file, member, line),
                Title = "Select the game process",
                Message = message,
                Candidates = candidates,
                PlayerIndex = player?.PlayerID ?? -1,
                DefaultResult = DialogResult.OK,
            }).Process;
        }

        /// <summary>Drop-in for Globals.MainOSD.Show(timing, text). Never blocks.</summary>
        public static void Osd(int timing, string text,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            if (!Headless && Globals.MainOSD != null)
            {
                Globals.MainOSD.Show(timing, text);
                return;
            }

            var req = new EngineUiRequest
            {
                Kind = EngineUiKind.Osd,
                Source = SourceOf(file, member, line),
                Message = text,
                DurationMs = timing,
            };

            Log("OSD", text);
            try { Handler?.Invoke(req); } catch { }
        }
    }
}
