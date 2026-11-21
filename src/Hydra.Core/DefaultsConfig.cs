namespace Hydra.Core
{
    public class DefaultsConfig
    {
        public string Version { get; set; } = "1.0.0";
        public UserDefaults User { get; set; } = new();
        public SystemDefaults System { get; set; } = new();
        public SessionManagerDefaults SessionManager { get; set; } = new();
    }

    public class UserDefaults
    {
        public string UserID { get; set; } = "USR_001";
        public string Nome { get; set; } = "Carlos Marques";
        public string GitHubHandle { get; set; } = "carloshmarques";
        public string Role { get; set; } = "Fundador";
    }

    public class SystemDefaults
    {
        public string AppName { get; set; } = "Hydra";
        public string DefaultMode { get; set; } = "ALL_ONLINE";
        public string FallbackTemp { get; set; } = @"C:\Users\carlo\Data\Temp\Hydra";
        public int AutoSaveSec { get; set; } = 30;
        public string LogLevel { get; set; } = "INFO";
    }

    public class SessionManagerDefaults
    {
        public string LiveFormat { get; set; } = "yyyyMMdd_session.txt";
        public string ArchiveFormat { get; set; } = "yyyyMMdd_session.json";
        public bool AutoConsolidate { get; set; } = true;
    }
}
