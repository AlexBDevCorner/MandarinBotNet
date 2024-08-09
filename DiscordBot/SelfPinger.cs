using Quartz;

namespace DiscordBot
{
    public class SelfPinger(IHttpClientFactory httpClientFactory) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("http://localhost:8080");
            var response = await client.GetAsync("/api/SelfPing");
            Console.WriteLine($"SelfPinger: {response.StatusCode}");
        }
    }
}
