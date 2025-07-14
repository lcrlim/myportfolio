using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System;

namespace NetCoreWebServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ColorController : ControllerBase
    {
        private static readonly (string Name, string HexCode)[] Colors = new[] { ("Red", "#FF0000"), ("Green", "#00FF00"), ("Blue", "#0000FF"), ("Yellow", "#FFFF00"), ("Purple", "#800080"), ("Orange", "#FFA500"), ("Cyan", "#00FFFF"), ("Magenta", "#FF00FF"), ("Lime", "#32CD32"), ("Pink", "#FFC1CC") };
        private readonly ILogger<ColorController> _logger;

        public ColorController(ILogger<ColorController> logger)
        {
            _logger = logger;
        }

        [Authorize]
        [EnableRateLimiting("fixed_100_1sec")]
        [HttpGet(Name = "GetColors")]
        public IEnumerable<Color> Get()
        {
            return Enumerable.Range(1, 5).Select(_ => Colors[Random.Shared.Next(Colors.Length)])
                .Select(color => new Color
                {
                    Name = color.Name,
                    HexCode = color.HexCode
                })
                .ToArray();
        }
    }

    public class Color 
    { 
        public string Name { get; set; } = string.Empty; 
        public string HexCode { get; set; } = string.Empty; 
    }
}
