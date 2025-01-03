﻿using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HarmonyLib;
using JALib.Core.Patch;
using JALib.Tools.ByteTool;
using SkyHook;
using UnityEngine;
using EventType = SkyHook.EventType;

namespace RealAsync;

public class RealAsyncManager {
    public static Process Process;
    public static Stream output;
    public static StreamReader error;
    public static byte[] buffer;
    public static int tryCount;
    public static int cur;
    public static int timeDiff;
    public static Action<SkyHookEvent> Callback;

    public static void Initialize() {
        bool active = SkyHookManager.Instance.isHookActive;
        tryCount = 0;
        buffer = new byte[20];
        timeDiff = (int) (DateTime.Now.Ticks / 10000000L) - (int) (DateTime.UtcNow.Ticks / 10000000L);
        Callback = (Action<SkyHookEvent>) Delegate.CreateDelegate(typeof(Action<SkyHookEvent>), SkyHookManager.Instance, typeof(SkyHookManager).Method("HookCallback"));
        if(!active) return;
        SkyHookManager.StopHook();
        SetupProcess();
    }

    private static void SetupProcess() {
        Process = new Process();
        try {
            ProcessStartInfo ummStartInfo = Process.StartInfo;
            ummStartInfo.FileName = Path.Combine(Main.Instance.Path, "AdofaiRealAsync.Backend.exe");
            ummStartInfo.UseShellExecute = false;
            ummStartInfo.RedirectStandardInput = true;
            ummStartInfo.RedirectStandardOutput = true;
            ummStartInfo.RedirectStandardError = true;
            ummStartInfo.CreateNoWindow = true;
            Application.wantsToQuit += OnQuit;
            Process.Start();
            Process.PriorityClass = ProcessPriorityClass.RealTime;
            output = Process.StandardOutput.BaseStream;
            error = Process.StandardError;
            cur = 0;
            output.ReadAsync(buffer, 0, 20).ContinueWith(Read);
            error.ReadLineAsync().ContinueWith(ReadError);
            Main.WaterMark(true);
            Main.Instance.Log("RealAsync hook started.");
        } catch (Exception) {
            Process.Dispose();
            throw;
        }
    }

    private static bool OnQuit() {
        try {
            Process?.Dispose();
        } catch (Exception) {
            // ignored
        }
        return true;
    }

    private static void ReadError(Task<string> t) {
        try {
            Main.Instance.Error(t.Result);
            if(!IsHookActive()) return;
            error.ReadLineAsync().ContinueWith(ReadError);
        } catch (ObjectDisposedException) {
        } catch (Exception e) {
            if(Process == null || e.InnerException is NullReferenceException) return;
            Main.Instance.Error("Failed to read error RealAsync event.");
            Main.Instance.LogException(e);
        }
    }

    private static void Read(Task<int> t) {
        try {
            if(Process == null) return;
            if(!IsHookActive()) {
                if(tryCount++ > 5) {
                    Main.Instance.Error("RealAsync backend process has exited.");
                    Main.Instance.Error();
                    return;
                }
                Main.Instance.Error("RealAsync backend process has exited. Restarting...");
                try {
                    Process.Dispose();
                } catch (Exception) {
                    // ignored
                }
                SetupProcess();
                return;
            }
            cur += t.Result;
            int remove = 0;
            for(int i = 1; i < cur; i++) {
                if(buffer[i] == 10 && buffer[i - 1] == 13) remove += 1;
                buffer[i - remove] = buffer[i];
            }
            cur -= remove;
            if(cur >= 11) {
                if(IsHookActive()) {
                    if(buffer[8] < 2) {
                        long time = buffer[..8].Reverse().ToLong();
                        RealAsyncEvent realAsyncEvent = new(time / 1000000000 + timeDiff, (uint) (time % 1000000000), (EventType) buffer[8], (KeyLabel) buffer[9], buffer[10]);
                        SkyHookEvent skyHookEvent;
                        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<SkyHookEvent>());
                        try {
                            Marshal.StructureToPtr(realAsyncEvent, ptr, false);
                            skyHookEvent = Marshal.PtrToStructure<SkyHookEvent>(ptr);
                        } finally {
                            Marshal.FreeHGlobal(ptr);
                        }
                        Callback(skyHookEvent);
                    } else {
                        Main.Instance.Error("RealAsync send invalid message.");
                        Main.Instance.Error("cur: " + cur + ", buffer: " + buffer.Join());
                    }
                }
                cur = 0;
            }
            output.ReadAsync(buffer, cur, 20 - cur).ContinueWith(Read);
        } catch (ObjectDisposedException) {
        } catch (Exception e) {
            Main.Instance.Error("Failed to read RealAsync event.");
            Main.Instance.LogException(e);
            output.ReadAsync(buffer, 0, 20).ContinueWith(Read);
        }
    }

    public static void Dispose() {
        if(IsHookActive()) SkyHookManager.StartHook();
        Close();
        buffer = null;
        Callback = null;
    }

    public static void Close() {
        output?.Dispose();
        error?.Dispose();
        try {
            Process.Dispose();
        } catch (Exception) {
            // ignored
        }
        Application.wantsToQuit -= OnQuit;
        Process = null;
        output = null;
        error = null;
        Main.WaterMark(false);
        Main.Instance.Log("RealAsync hook stopped.");
    }

    [JAPatch(typeof(SkyHookManager), "_StartHook", PatchType.Replace, false)]
    private static void StartHook() {
        if(IsHookActive()) return;
        SetupProcess();
    }

    [JAPatch(typeof(SkyHookManager), "_StopHook", PatchType.Replace, false)]
    private static void StopHook() {
        if(!IsHookActive()) return;
        tryCount = 0;
        Close();
    }

    [JAPatch(typeof(SkyHookManager), "isHookActive.get", PatchType.Replace, false)]
    private static bool IsHookActive() {
        try {
            return Process is { HasExited: false };
        } catch (Exception) {
            return false;
        }
    }

    private struct RealAsyncEvent(long timeSec, uint timeSubsecNano, EventType type, KeyLabel label, ushort key) {
        public long TimeSec = timeSec;
        public uint TimeSubsecNano = timeSubsecNano;
        public EventType Type = type;
        public KeyLabel Label = label;
        public ushort Key = key;
    }
}