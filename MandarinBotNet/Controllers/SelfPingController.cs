using Microsoft.AspNetCore.Mvc;

namespace MandarinBotNet.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SelfPingController : ControllerBase
    {  
        [HttpGet]
        public IActionResult Get()
        {
            Console.WriteLine("SelfPingController: Pinged");
            return Ok();
        }
    }
}
