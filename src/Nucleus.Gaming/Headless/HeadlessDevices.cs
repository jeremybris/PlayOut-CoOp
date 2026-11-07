// PlayOut Co-Op - headless front for the Nucleus Co-op engine.
// Copyright (C) 2026 PlayOut Co-Op contributors. Derived from Nucleus Co-op (SplitScreen-Me), GPL-3.
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version. See LICENSE.
//
// Device enumeration without the setup screen. The PlayerInfo objects built here carry the same fields
// Nucleus's Controls/SetupScreen/DevicesFunctions.cs (GamepadTimer_Tick / UpdateDevices) and
// Coop/InputManagement/RawInputManager.GetDeviceInputInfos give them, so GenericGameHandler treats them alike.

using Nucleus.Gaming.Coop;
using Nucleus.Gaming.Coop.InputManagement;
using Nucleus.Gaming.Coop.InputManagement.Gamepads;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Nucleus.Gaming.Headless
{
    /// <summary>A display as Nucleus sees it (User32Util.GetDisplays), in the process's DPI context.</summary>
    public sealed class HeadlessScreenInfo
    {
        public int Index;
        public bool Primary;
        public System.Drawing.Rectangle Bounds;
        public string DeviceName;
        public int DisplayIndex;
        public string MonitorID;
    }

    /// <summary>An XInput slot.</summary>
    public sealed class HeadlessXInputPad
    {
        public int Index;
        public bool Connected;
        public ushort VendorId;
        public ushort ProductId;
        /// <summary>"openxinput" when read through openxinput1_3.dll (Nucleus's way), else "xinput1_4".</summary>
        public string Api;
    }

    /// <summary>A DirectInput game controller.</summary>
    public sealed class HeadlessDInputPad
    {
        /// <summary>Position in DirectInput enumeration order = Nucleus's GamepadId for DInput players.</summary>
        public int Index;
        public Guid InstanceGuid;
        public Guid ProductGuid;
        public string Name;
        public string InterfacePath;
        public string VendorId;
        /// <summary>True for XInput devices seen through DirectInput (interface path contains "IG_").</summary>
        public bool IsXInputDevice;
    }

    /// <summary>A raw keyboard or mouse (GetRawInputDeviceList), as Nucleus lists them.</summary>
    public sealed class HeadlessRawDevice
    {
        public IntPtr Handle;
        public bool IsKeyboard;
        public bool IsMouse;
        public string Name;
    }

    public static class HeadlessDevices
    {
        /// <summary>The fixed GUID Nucleus gives its single shared keyboard/mouse player.</summary>
        public static readonly Guid SharedKeyboardGuid = new Guid("10000000-1000-1000-1000-100000000000");

        // ---- screens -------------------------------------------------------------------------------------

        public static List<HeadlessScreenInfo> GetScreens()
        {
            Display[] all = ScreensUtil.AllScreensParams();
            System.Drawing.Rectangle primary = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            List<HeadlessScreenInfo> list = new List<HeadlessScreenInfo>();

            for (int i = 0; i < all.Length; i++)
            {
                list.Add(new HeadlessScreenInfo
                {
                    Index = i,
                    // Display.Primary is always true in Nucleus; compare with WinForms' primary screen instead.
                    Primary = all[i].Bounds.Location == primary.Location,
                    Bounds = all[i].Bounds,
                    DeviceName = all[i].DeviceName,
                    DisplayIndex = all[i].DisplayIndex,
                    MonitorID = all[i].MonitorID,
                });
            }

            return list;
        }

        /// <summary>
        /// The UserScreen objects Nucleus builds for its setup screen (ScreensUtil.AllScreens + Index), with UIBounds
        /// set to the monitor bounds so EditBounds are meaningful. Index matches <see cref="GetScreens"/>.
        /// </summary>
        public static UserScreen[] GetUserScreens()
        {
            UserScreen[] screens = ScreensUtil.AllScreens();
            for (int i = 0; i < screens.Length; i++)
            {
                screens[i].Index = i;
                screens[i].UIBounds = screens[i].MonitorBounds;
            }

            return screens;
        }

        // ---- XInput --------------------------------------------------------------------------------------

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState14(int dwUserIndex, out State state);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetCapabilities")]
        private static extern int XInputGetCapabilities14(int dwUserIndex, int dwFlags, out SharpDX.XInput.Capabilities caps);

        /// <summary>
        /// Polls XInput slots 0..count-1 like DevicesFunctions (openxinput1_3.dll from the engine root, which allows
        /// up to 32 slots). Falls back to the system xinput1_4.dll (4 slots) when openxinput1_3.dll is not there.
        /// </summary>
        public static List<HeadlessXInputPad> GetXInputPads(int count = 4, bool includeDisconnected = true)
        {
            List<HeadlessXInputPad> pads = new List<HeadlessXInputPad>();
            bool useOpen = true;

            for (int i = 0; i < count; i++)
            {
                bool connected = false;
                ushort vid = 0, pid = 0;
                string api = useOpen ? "openxinput" : "xinput1_4";

                if (useOpen)
                {
                    try
                    {
                        connected = new OpenXinputController(true, i).IsConnected;
                        if (connected)
                        {
                            try
                            {
                                OpenXinputController.XInputGetCapabilitiesEx(1, (uint)i, 1, out OpenXinputController.NativeOpenXinput.CapabilitiesEx cap);
                                vid = cap.VendorId;
                                pid = cap.ProductId;
                            }
                            catch { }
                        }
                    }
                    catch (DllNotFoundException)
                    {
                        useOpen = false;
                        api = "xinput1_4";
                    }
                }

                if (!useOpen)
                {
                    if (i >= 4)
                    {
                        break;
                    }

                    connected = XInputGetState14(i, out State _) == 0;
                }

                if (connected || includeDisconnected)
                {
                    pads.Add(new HeadlessXInputPad { Index = i, Connected = connected, VendorId = vid, ProductId = pid, Api = api });
                }
            }

            return pads;
        }

        /// <summary>Reads one slot's state (for tests and UIs). Returns false when the slot is empty.</summary>
        public static bool TryGetXInputState(int index, out State state)
        {
            try
            {
                return new OpenXinputController(true, index).GetState(out state);
            }
            catch (DllNotFoundException)
            {
                return XInputGetState14(index, out state) == 0;
            }
        }

        // ---- DirectInput ---------------------------------------------------------------------------------

        /// <summary>DirectInput game controllers in enumeration order (DInputManager.InitJoystickList's order).</summary>
        public static List<HeadlessDInputPad> GetDInputPads()
        {
            List<HeadlessDInputPad> list = new List<HeadlessDInputPad>();

            using (DirectInput dinput = new DirectInput())
            {
                IList<DeviceInstance> devices = dinput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly);

                for (int i = 0; i < devices.Count; i++)
                {
                    DeviceInstance dev = devices[i];
                    string path = "";
                    string vendor = "";

                    try
                    {
                        using (Joystick joystick = new Joystick(dinput, dev.InstanceGuid))
                        {
                            path = joystick.Properties.InterfacePath ?? "";
                            vendor = joystick.Properties.VendorId.ToString();
                        }
                    }
                    catch { }

                    list.Add(new HeadlessDInputPad
                    {
                        Index = i,
                        InstanceGuid = dev.InstanceGuid,
                        ProductGuid = dev.ProductGuid,
                        Name = dev.InstanceName,
                        InterfacePath = path,
                        VendorId = vendor,
                        IsXInputDevice = path.ToUpper().Contains("IG_"),
                    });
                }
            }

            return list;
        }

        // ---- raw keyboards / mice ------------------------------------------------------------------------

        /// <summary>Raw keyboards and mice (RawInputManager.GetDeviceList, dwType 0 = mouse, 1 = keyboard).</summary>
        public static List<HeadlessRawDevice> GetRawKeyboardsAndMice()
        {
            return RawInputManager.GetDeviceList()
                .Where(d => d.deviceInfo.dwType <= 1)
                .Select(d => new HeadlessRawDevice
                {
                    Handle = d.deviceHandle,
                    IsMouse = d.deviceInfo.dwType == 0,
                    IsKeyboard = d.deviceInfo.dwType == 1,
                    Name = d.deviceName,
                })
                .ToList();
        }

        // ---- PlayerInfo factories (same fields DevicesFunctions sets) ---------------------------------------

        /// <summary>An XInput player, as DevicesFunctions.GamepadTimer_Tick creates it.</summary>
        public static PlayerInfo CreateXInputPlayer(int slot)
        {
            PlayerInfo player = new PlayerInfo
            {
                HIDDeviceID = new string[] { "Not required", "Not required" },
                XInputJoystick = new OpenXinputController(true, slot),
                GamepadId = slot,
                IsXInput = true,
            };

            if (GameProfile.UseXinputIndex)
            {
                string guid = player.GamepadId + 1 >= 10 ? $"00000000-0000-0000-0000-2000000000{player.GamepadId + 1}" : $"00000000-0000-0000-0000-20000000000{player.GamepadId + 1}";
                player.GamepadGuid = new Guid(guid);
            }

            player.IsInputUsed = true;
            return player;
        }

        /// <summary>
        /// A DirectInput player, as DevicesFunctions.GamepadTimer_Tick creates it. Opens and acquires the device the
        /// way DInputManager.MakeNewJoystick does (kept alive for the session in PlayerInfo.DInputJoystick).
        /// Returns null when no attached device has that instance GUID.
        /// </summary>
        public static PlayerInfo CreateDInputPlayer(Guid instanceGuid)
        {
            HeadlessDInputPad pad = GetDInputPads().FirstOrDefault(p => p.InstanceGuid == instanceGuid);
            if (pad == null)
            {
                return null;
            }

            DirectInput dinput = new DirectInput();
            Joystick joystick = new Joystick(dinput, instanceGuid);

            D_Joystick d = new D_Joystick
            {
                Joystick = joystick,
                InstanceName = joystick.Information.InstanceName,
                InstanceGuid = joystick.Information.InstanceGuid,
                ProductGuid = joystick.Information.ProductGuid,
                InterfacePath = joystick.Properties.InterfacePath,
                VendorId = joystick.Properties.VendorId.ToString(),
                NativePointer = joystick.NativePointer,
                Device_Guid = instanceGuid,
            };

            try { d.Joystick.Acquire(); } catch { }
            d.Status = JoystickStatus.Connected;

            PlayerInfo player = new PlayerInfo
            {
                DInputJoystick = d,
                GamepadProductGuid = d.ProductGuid,
                GamepadGuid = d.InstanceGuid,
                GamepadName = d.InstanceName,
                RawHID = d.InterfacePath,
            };

            try
            {
                int start = player.RawHID.IndexOf("hid#");
                int end = player.RawHID.LastIndexOf("#{");
                string fhid = player.RawHID.Substring(start, end - start).Replace('#', '\\').ToUpper();
                player.HIDDeviceID = new string[] { fhid, "" };
            }
            catch
            {
                player.HIDDeviceID = new string[] { "Not required", "" };
            }

            player.IsInputUsed = true;
            player.GamepadId = pad.Index;
            player.IsDInput = true;
            return player;
        }

        /// <summary>Nucleus's single shared keyboard/mouse player (Game.SupportsKeyboard), as DevicesFunctions.UpdateDevices adds it.</summary>
        public static PlayerInfo CreateSharedKeyboardPlayer()
        {
            return new PlayerInfo
            {
                IsKeyboardPlayer = true,
                GamepadId = 99,
                IsInputUsed = true,
                HIDDeviceID = new string[] { "Not required", "Not required" },
                GamepadGuid = SharedKeyboardGuid,
            };
        }

        /// <summary>
        /// A raw keyboard + mouse player (Game.SupportsMultipleKeyboardsAndMice / ProtoInput), already in the merged
        /// form GameProfile.CleanClone produces when a raw keyboard and a raw mouse share one slot.
        /// Pass IntPtr.Zero for Nucleus's "HandleZero" pseudo devices and (IntPtr)(-1) for "none".
        /// </summary>
        public static PlayerInfo CreateRawKeyboardMousePlayer(IntPtr keyboardHandle, IntPtr mouseHandle)
        {
            List<HeadlessRawDevice> devices = GetRawKeyboardsAndMice();
            string kbName = keyboardHandle == IntPtr.Zero ? "KeyboardHandleZero" : devices.FirstOrDefault(d => d.IsKeyboard && d.Handle == keyboardHandle)?.Name ?? "";
            string mouseName = mouseHandle == IntPtr.Zero ? "MouseHandleZero" : devices.FirstOrDefault(d => d.IsMouse && d.Handle == mouseHandle)?.Name ?? "";

            bool hasKb = keyboardHandle != (IntPtr)(-1);
            bool hasMouse = mouseHandle != (IntPtr)(-1);

            PlayerInfo player = new PlayerInfo
            {
                GamepadId = 100,
            };

            // Same order of assignments as RawInputManager.GetDeviceInputInfos + CleanClone's merge.
            player.IsRawMouse = hasMouse;
            player.IsRawKeyboard = hasKb;
            player.RawKeyboardDeviceHandle = hasKb ? keyboardHandle : (IntPtr)(-1);
            player.RawMouseDeviceHandle = hasMouse ? mouseHandle : (IntPtr)(-1);
            player.HIDDeviceID = new string[] { hasKb ? kbName : mouseName, hasKb && hasMouse ? mouseName : "" };
            player.IsKeyboardPlayer = true;
            player.IsInputUsed = true;
            return player;
        }

        /// <summary>A player with no input device (spectator / handler-driven input).</summary>
        public static PlayerInfo CreateNoInputPlayer()
        {
            return new PlayerInfo
            {
                HIDDeviceID = new string[] { "Not required", "Not required" },
                IsInputUsed = true,
            };
        }

        /// <summary>
        /// Which device families Nucleus would offer for this handler (same tests as DevicesFunctions.GamepadTimer_Tick).
        /// </summary>
        public static void DeviceKindsFor(GenericGameInfo g, out bool xinput, out bool dinput, out bool sdl2, out bool keyboard, out bool rawKeyboardsAndMice)
        {
            dinput = g.Hook.DInputEnabled || g.Hook.XInputReroute || g.ProtoInput.DinputDeviceHook;
            xinput = (g.Hook.XInputEnabled && !g.Hook.XInputReroute && !g.ProtoInput.DinputDeviceHook) || g.ProtoInput.XinputHook;
            sdl2 = g.Hook.SDL2Enabled;
            keyboard = g.SupportsKeyboard;
            rawKeyboardsAndMice = g.SupportsMultipleKeyboardsAndMice;
        }
    }
}
