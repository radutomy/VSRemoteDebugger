namespace VSRemoteDebugger
{
    public static class Remote
    {
        public static string IP { get; set; } = "192.168.0.10";
        public static string HostName { get; set; } = "pi";
        public static string DebugFolderPath { get; set; } = "/var/proj/debug";
        public static string MasterFolderPath { get; set; } = "/var/proj";
        public static string ReleaseFolderPath { get; set; } = "/var/proj/release";
        public static string VsDbgPath { get; set; } = "~/.vsdbg/vsdbg";
    }
}
