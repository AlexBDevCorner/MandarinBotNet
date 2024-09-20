using Microsoft.AspNetCore.Mvc;

namespace MandarinBotNet.Controllers
{
    [Route("")]
    [ApiController]
    public class HelloController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok("Hello!");
        }
    }
}
