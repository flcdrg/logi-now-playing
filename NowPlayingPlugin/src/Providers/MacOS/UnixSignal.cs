namespace Loupedeck.NowPlayingPlugin.Providers.MacOS
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;

    // Sends POSIX signals to a running process so it can shut down the way it
    // expects to (the MediaRemoteAdapter stream command listens for SIGTERM),
    // instead of always forcefully killing it.
    internal static class UnixSignal
    {
        private const Int32 SIGTERM = 15;

        [DllImport("libc", SetLastError = true, EntryPoint = "kill")]
        private static extern Int32 NativeKill(Int32 pid, Int32 signal);

        // Returns true if the signal was delivered (the process may still take
        // time to exit); returns false if the process had already exited or the
        // signal could not be delivered.
        public static Boolean TryTerminate(Process process)
        {
            try
            {
                return NativeKill(process.Id, SIGTERM) == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
