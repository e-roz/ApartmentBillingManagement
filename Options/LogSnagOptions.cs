namespace Apartment.Options
{
    public class LogSnagOptions
    {
        public string? Project { get; set; }
        public string? Token { get; set; }
        public string? DefaultChannel { get; set; } = "billing";
        public decimal OutstandingWarningThreshold { get; set; } = 50000m;
        public decimal CollectionEfficiencyWarning { get; set; } = 85m;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Project) &&
            !string.IsNullOrWhiteSpace(Token);
    }
}


