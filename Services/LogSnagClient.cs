using Apartment.Options;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Apartment.Services
{
    public class LogSnagClient : ILogSnagClient
    {
        private readonly HttpClient _httpClient;
        private readonly LogSnagOptions _options;

        public LogSnagClient(HttpClient httpClient, IOptions<LogSnagOptions> options)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress ??= new Uri("https://api.logsnag.com/");
            _options = options.Value;
        }

        public async Task PublishAsync(LogSnagEvent logEvent, CancellationToken cancellationToken = default)
        {
            if (!_options.IsConfigured)
            {
                return;
            }

            var channel = logEvent.Channel ?? _options.DefaultChannel ?? "general";
            var payload = new
            {
                project = _options.Project,
                channel,
                @event = logEvent.Event,
                description = logEvent.Description,
                notify = logEvent.Notify,
                icon = logEvent.Icon,
                tags = logEvent.Tags
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/log")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }
}


