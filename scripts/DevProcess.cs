#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Crm.Dev;

// Windows job ownership closes the root-first-exit hole in Kill(entireProcessTree).
// No service, scheduler or restart framework: just two owned console child trees.
public sealed class Child : IDisposable
{
    private IntPtr job;
    private readonly Process root;
    public string Name { get; }
    public int Id => root.Id;
    public bool HasExited => root.HasExited;
    public static volatile bool StopRequested;
    private static readonly ConsoleCancelEventHandler Cancel = (_, e) => { e.Cancel = true; StopRequested = true; };
    public static void RegisterCancel() { StopRequested = false; Console.CancelKeyPress += Cancel; }
    public static void UnregisterCancel() => Console.CancelKeyPress -= Cancel;
    private Child(string name, IntPtr owner, Process process) { Name = name; job = owner; root = process; }
    public uint ActiveCount
    {
        get
        {
            if (job == IntPtr.Zero) return 0;
            if (!QueryInformationJobObject(job, 1, out Accounting accounting, Marshal.SizeOf<Accounting>(), IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return accounting.ActiveProcesses;
        }
    }
    public static Child Start(string name, string executable, string[] arguments, string directory)
    {
        IntPtr owner = CreateJobObject(IntPtr.Zero, null);
        if (owner == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        IntPtr limits = Marshal.AllocHGlobal(144);
        ProcessInfo process = default;
        Process? managedRoot = null;
        try
        {
            Marshal.Copy(new byte[144], 0, limits, 144);
            Marshal.WriteInt32(limits, 16, 0x2000); // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            if (!SetInformationJobObject(owner, 9, limits, 144)) throw new Win32Exception(Marshal.GetLastWin32Error());
            var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>(), Flags = 0x100,
                Input = GetStdHandle(-10), Output = GetStdHandle(-11), Error = GetStdHandle(-12) };
            var command = new StringBuilder(Quote(executable));
            foreach (string argument in arguments) command.Append(' ').Append(Quote(argument));
            // Suspended assignment prevents a fast-exiting wrapper from escaping ownership.
            if (!CreateProcess(executable, command, IntPtr.Zero, IntPtr.Zero, true, 0x204, IntPtr.Zero, directory, ref startup, out process))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!AssignProcessToJobObject(owner, process.Process)) throw new Win32Exception(Marshal.GetLastWin32Error());
            managedRoot = Process.GetProcessById((int)process.Id);
            _ = managedRoot.Handle; // Retain the root object even after exit; never signal a recycled PID.
            var child = new Child(name, owner, managedRoot);
            if (ResumeThread(process.Thread) == uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error());
            owner = IntPtr.Zero;
            return child;
        }
        finally
        {
            Marshal.FreeHGlobal(limits);
            if (owner != IntPtr.Zero)
            {
                if (process.Process != IntPtr.Zero) TerminateProcess(process.Process, 1);
                CloseHandle(owner);
                managedRoot?.Dispose();
            }
            if (process.Thread != IntPtr.Zero) CloseHandle(process.Thread);
            if (process.Process != IntPtr.Zero) CloseHandle(process.Process);
        }
    }
    public static string[] StopAll(Child[] children, int graceMilliseconds)
    {
        var deadline = Stopwatch.StartNew();
        foreach (var child in children)
            if (child.ActiveCount > 0) GenerateConsoleCtrlEvent(1, (uint)child.Id); // owned CTRL_BREAK group only
        while (deadline.ElapsedMilliseconds < graceMilliseconds)
        {
            bool active = false;
            foreach (var child in children) active |= child.ActiveCount > 0;
            if (!active) break;
            Thread.Sleep(50);
        }
        var outcomes = new List<string>();
        foreach (var child in children)
        {
            bool forced = child.ActiveCount > 0;
            if (forced && !TerminateJobObject(child.job, 1)) throw new Win32Exception(Marshal.GetLastWin32Error());
            outcomes.Add(child.Name + ": " + (forced ? "forced cleanup" : "graceful exit"));
        }
        var remaining = Stopwatch.StartNew();
        foreach (var child in children)
        {
            while (child.ActiveCount > 0 && remaining.ElapsedMilliseconds < 2000) Thread.Sleep(25);
            if (child.ActiveCount > 0) throw new InvalidOperationException("Owned child cleanup did not complete.");
        }
        return outcomes.ToArray();
    }
    public void Dispose() { if (job != IntPtr.Zero) { CloseHandle(job); job = IntPtr.Zero; } root.Dispose(); }
    private static string Quote(string value)
    {
        var result = new StringBuilder("\""); int slashes = 0;
        foreach (char character in value)
        {
            if (character == '\\') { slashes++; continue; }
            if (character == '"') { result.Append('\\', slashes * 2 + 1).Append('"'); slashes = 0; continue; }
            result.Append('\\', slashes).Append(character); slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StartupInfo
    {
        public int Size; public string? Reserved, Desktop, Title;
        public int X, Y, XSize, YSize, XCount, YCount, Fill, Flags;
        public short Show, ReservedSize; public IntPtr ReservedBytes, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInfo { public IntPtr Process, Thread; public uint Id, ThreadId; }
    [StructLayout(LayoutKind.Sequential)] private struct Accounting { public long User, Kernel, PeriodUser, PeriodKernel; public uint Faults, TotalProcesses, ActiveProcesses, TerminatedProcesses; }
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32", SetLastError = true)] private static extern bool SetInformationJobObject(IntPtr job, int kind, IntPtr information, int length);
    [DllImport("kernel32", SetLastError = true)] private static extern bool QueryInformationJobObject(IntPtr job, int kind, out Accounting information, int length, IntPtr returned);
    [DllImport("kernel32", CharSet = CharSet.Unicode, EntryPoint = "CreateProcessW", SetLastError = true)] private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes, bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInfo process);
    [DllImport("kernel32", SetLastError = true)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32", SetLastError = true)] private static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32", SetLastError = true)] private static extern bool TerminateJobObject(IntPtr job, uint code);
    [DllImport("kernel32", SetLastError = true)] private static extern bool TerminateProcess(IntPtr process, uint code);
    [DllImport("kernel32")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32")] private static extern IntPtr GetStdHandle(int kind);
    [DllImport("kernel32", SetLastError = true)] private static extern bool GenerateConsoleCtrlEvent(uint kind, uint group);
}
