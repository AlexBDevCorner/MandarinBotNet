using Quartz;

namespace DiscordBot
{
    public class SelfPinger(IHttpClientFactory httpClientFactory) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://localhost:7041");
            var response = await client.GetAsync("/api/SelfPing");
            Console.WriteLine($"SelfPinger: {response.StatusCode}");
        }
    }
}
