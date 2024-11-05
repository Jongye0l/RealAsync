using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HarmonyLib;
using JALib.Core.Patch;
using JALib.Tools.ByteTool;
using SkyHook;

namespace RealAsync;

public class RealAsyncManager {
    public static Process Process;
    public static Stream output;
    public static StreamReader error;
    public static byte[] buffer;
    public static int tryCount;
    public static int cur;

    public static void Initialize() {
        bool active = SkyHookManager.Instance.isHookActive;
        tryCount = 0;
        buffer = new byte[20];
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
            Process.Start();
            Process.PriorityClass = ProcessPriorityClass.RealTime;
            output = Process.StandardOutput.BaseStream;
            error = Process.StandardError;
            cur = 0;
            output.ReadAsync(buffer, 0, 20).ContinueWith(Read);
            error.ReadLineAsync().ContinueWith(ReadError);
            Main.Instance.Log("RealAsync hook started.");
        } catch (Exception) {
            Process.Kill();
            Process.Dispose();
            throw;
        }
    }

    private static void ReadError(Task<string> t) {
        try {
            Main.Instance.Error(t.Result);
            if(!get_isHookActive()) return;
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
            if(Process.HasExited) {
                if(tryCount++ > 5) {
                    Main.Instance.Error("RealAsync backend process has exited.");
                    Main.Instance.Error();
                    return;
                }
                Main.Instance.Error("RealAsync backend process has exited. Restarting...");
                Process.Dispose();
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
            if((cur += t.Result) >= 11) {
                if(get_isHookActive()) {
                    if(buffer[8] < 2) {
                        long time = buffer[..8].Reverse().ToLong();
                        RealAsyncEvent realAsyncEvent = new(time / 1000000000 + 32400, (uint) (time % 1000000000), (EventType) buffer[8], (KeyLabel) buffer[9], buffer[10]);
                        SkyHookEvent skyHookEvent;
                        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<SkyHookEvent>());
                        try {
                            Marshal.StructureToPtr(realAsyncEvent, ptr, false);
                            skyHookEvent = Marshal.PtrToStructure<SkyHookEvent>(ptr);
                        } finally {
                            Marshal.FreeHGlobal(ptr);
                        }
                        SkyHookManager.KeyUpdated.Invoke(skyHookEvent);
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
        if(get_isHookActive()) SkyHookManager.StartHook();
        Close();
        buffer = null;
    }

    public static void Close() {
        output?.Dispose();
        error?.Dispose();
        try {
            Process.Kill();
            Process.Dispose();
        } catch (Exception) {
            // ignored
        }
        Process = null;
        output = null;
        error = null;
        Main.Instance.Log("RealAsync hook stopped.");
    }

    [JAPatch(typeof(SkyHookManager), "_StartHook", PatchType.Replace, false)]
    private static void StartHook() {
        if(get_isHookActive()) return;
        SetupProcess();
    }

    [JAPatch(typeof(SkyHookManager), "_StopHook", PatchType.Replace, false)]
    private static void StopHook() {
        if(!get_isHookActive()) return;
        tryCount = 0;
        Close();
    }

    [JAPatch(typeof(SkyHookManager), "get_isHookActive", PatchType.Replace, false)]
    private static bool get_isHookActive() => Process is { HasExited: false };

    private struct RealAsyncEvent(long timeSec, uint timeSubsecNano, EventType type, KeyLabel label, ushort key) {
        public long TimeSec = timeSec;
        public uint TimeSubsecNano = timeSubsecNano;
        public EventType Type = type;
        public KeyLabel Label = label;
        public ushort Key = key;
    }
}