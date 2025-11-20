namespace Apartment.Services
{
    public interface ILogSnagClient
    {
        Task PublishAsync(LogSnagEvent logEvent, CancellationToken cancellationToken = default);
    }

    public class LogSnagEvent
    {
        public string Event { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Channel { get; set; }
        public string? Icon { get; set; }
        public bool Notify { get; set; }
        public Dictionary<string, string>? Tags { get; set; }
    }
}


