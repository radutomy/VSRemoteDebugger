namespace VSRemoteDebugger
{
    internal static class Remote
    {
        internal static string IP => "192.168.0.10";
        internal static string HostName => "pi";
        internal static string GroupName => "pi";
        internal static string VsDbgPath => "~/.vsdbg/vsdbg";
        internal static string MasterFolderPath => "/var/proj";
        internal static string DebugFolderPath => $"{MasterFolderPath}/debug";
        internal static string ReleaseFolderPath => $"{MasterFolderPath}/release";
    }
}
