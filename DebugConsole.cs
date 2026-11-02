using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CuboidFever;

// Opens a console window next to the game so Console.WriteLine and Debug output are visible.
// Windows only: it relies on kernel32's AllocConsole.
public static class DebugConsole
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    public static void Init()
    {
        // Spawns a background console window for this process
        AllocConsole();
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });

        // This bridges Debug.WriteLine over to the standard output stream
        System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());

        Console.WriteLine("Console window initialized! Debug output is live.");
    }
}
