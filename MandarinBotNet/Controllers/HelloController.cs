using Microsoft.AspNetCore.Mvc;
using System.Net.Http;

namespace MandarinBotNet.Controllers
{
    [Route("")]
    [ApiController]
    public class HelloController(IHttpClientFactory httpClientFactory) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://fantasy.premierleague.com");
            var response = await client.GetAsync("/api/bootstrap-static");
            Console.WriteLine($"SelfPinger: {response.StatusCode}");

            return Ok("Hello!");
        }
    }
}
