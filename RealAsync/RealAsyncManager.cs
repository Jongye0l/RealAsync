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
    public static bool isActive;
    public static Process Process;
    public static Stream input;
    public static Stream output;
    public static StreamReader error;
    public static byte[] buffer;
    public static int tryCount;
    public static int cur;

    public static void Initialize() {
        bool active = SkyHookManager.Instance.isHookActive;
        if(active) SkyHookManager.StopHook();
        tryCount = 0;
        buffer = new byte[20];
        SetupProcess(active);
    }

    private static void SetupProcess(bool active) {
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
            input = Process.StandardInput.BaseStream;
            output = Process.StandardOutput.BaseStream;
            error = Process.StandardError;
            cur = 0;
            output.ReadAsync(buffer, 0, 20).ContinueWith(Read);
            error.ReadLineAsync().ContinueWith(ReadError);
            if(active) StartHook();
        } catch (Exception) {
            Process.Kill();
            Process.Dispose();
            throw;
        }
    }

    private static void ReadError(Task<string> t) {
        Main.Instance.Error(t.Result);
        error.ReadLineAsync().ContinueWith(ReadError);
    }

    private static void Read(Task<int> t) {
        try {
            cur += t.Result;
            if(Process.HasExited) {
                if(tryCount++ > 5) {
                    Main.Instance.Error("RealAsync backend process has exited.");
                    Main.Instance.Error();
                    return;
                }
                Main.Instance.Error("RealAsync backend process has exited. Restarting...");
                Process.Dispose();
                bool active = isActive;
                isActive = false;
                SetupProcess(active);
                return;
            }
            int remove = 0;
            for(int i = 1; i < cur; i++) {
                if(buffer[i] == 10 && buffer[i - 1] == 13) remove += 1;
                buffer[i - remove] = buffer[i];
            }
            cur -= remove;
            if((cur += t.Result) >= 11) {
                if(isActive) {
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
        } catch (Exception e) {
            Main.Instance.Error("Failed to read RealAsync event.");
            Main.Instance.LogException(e);
            output.ReadAsync(buffer, 0, 20).ContinueWith(Read);
        }
    }

    public static void Dispose() {
        if(isActive) SkyHookManager.StartHook();
        if(input != null) {
            input.WriteByte(2);
            input.Dispose();
            output.Dispose();
            error.Dispose();
        }
        try {
            Process.Kill();
            Process.Dispose();
        } catch (Exception) {
            // ignored
        }
        input = null;
        output = null;
        error = null;
        Process = null;
        isActive = false;
    }

    [JAPatch(typeof(SkyHookManager), "_StartHook", PatchType.Replace, false)]
    private static void StartHook() {
        if(isActive) return;
        isActive = true;
        input.WriteByte(0);
        Main.Instance.Log("RealAsync hook started.");
    }

    [JAPatch(typeof(SkyHookManager), "_StopHook", PatchType.Replace, false)]
    private static void StopHook() {
        if(!isActive) return;
        isActive = false;
        input.WriteByte(1);
        Main.Instance.Log("RealAsync hook stopped.");
    }

    [JAPatch(typeof(SkyHookManager), "get_isHookActive", PatchType.Replace, false)]
    private static bool get_isHookActive() => isActive;

    private struct RealAsyncEvent(long timeSec, uint timeSubsecNano, EventType type, KeyLabel label, ushort key) {
        public long TimeSec = timeSec;
        public uint TimeSubsecNano = timeSubsecNano;
        public EventType Type = type;
        public KeyLabel Label = label;
        public ushort Key = key;
    }
}