using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace RatingAPI.Controllers
{
    public class RatingsController : Controller
    {
        private readonly ILogger<RatingsController> _logger;

        public RatingsController(ILogger<RatingsController> logger)
        {
            _logger = logger;
        }

        [HttpGet("~/ppai/{hash}/{diff}/{mode}")]
        public ActionResult<double> Get(string hash, int diff, string mode)
        {
            Stopwatch sw = Stopwatch.StartNew();
            var res = new InferPublish().GetBlRatings(hash, mode, diff, 1);
            Console.WriteLine(sw.ElapsedMilliseconds);
            return res;
        }
    }
}