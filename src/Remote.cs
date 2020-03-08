namespace VSRemoteDebugger
{
    public static class Remote
    {
        public static string IP { get; set; } = "192.168.0.10";
        public static string HostName { get; set; } = "pi";
        public static string DebugFolderPath { get; set; } = "/var/ticketer/debug-ngm-server";
        public static string MasterFolderPath { get; set; } = "/var/ticketer";
        public static string ReleaseFolderPath { get; set; } = "/var/ticketer/release-ngm-server";
        public static string VsDbgPath { get; set; } = "~/.vsdbg/vsdbg";
    }
}
