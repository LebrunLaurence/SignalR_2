
using Microsoft.AspNetCore.SignalR;
using Microsoft.Identity.Client;
using signalr.backend.Data;
using signalr.backend.Hubs;

namespace signalr.backend.Services
{
    public class CountService : BackgroundService
    {
        public const int DELAY = 60 * 1000;

        public IServiceScopeFactory _serviceScopeFactory { get; set; }

        public IHubContext<ChatHub> _hub;

        public CountService(IServiceScopeFactory serviceScopeFactory, IHubContext<ChatHub> Hub)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _hub = Hub;
        }

        public async Task CheckChannelCount(CancellationToken stoppingToken)
        {
            using (IServiceScope scope = _serviceScopeFactory.CreateScope())
            {
                ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var highestChannel = dbContext.Channel.OrderByDescending(c => c.NbMessages).FirstOrDefault();

                if (highestChannel != null)
                {
                    _hub.Clients.Group("Channel" + highestChannel.Id).SendAsync("Highest",highestChannel.NbMessages);
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(DELAY, stoppingToken);
                await CheckChannelCount(stoppingToken);
            }
        }
    }
}
