namespace TDFMAUI.Config
{
    public class AppSettings
    {
        public ApiConfigSettings ApiSettings { get; set; }
    }

    public class ApiConfigSettings
    {
        public ApiEnvironmentSettings Production { get; set; }
        public ApiEnvironmentSettings Development { get; set; }
        public bool DevelopmentMode { get; set; }
        public int Timeout { get; set; }
        public int MaxRetries { get; set; }
        public int RetryDelay { get; set; }
        public double RetryMultiplier { get; set; }
    }

    public class ApiEnvironmentSettings
    {
        public string BaseUrl { get; set; }
        public string WebSocketUrl { get; set; }
    }
}
