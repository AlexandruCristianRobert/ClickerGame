namespace ClickerGame.Upgrades.Configuration
{
    public class ServiceUrlsOptions
    {
        public const string SectionName = "Services";

        public ServiceEndpoint GameCore { get; set; } = new();
        public ServiceEndpoint Players { get; set; } = new();
    }

    public class ServiceEndpoint
    {
        public string BaseUrl { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
    }
}