using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace RatingAPI.Controllers
{
    public class RatingResult {
        public double AIAcc { get; set; }
        // public lack_map_calculation
    }

    public class RatingsController : Controller
    {
        private readonly ILogger<RatingsController> _logger;

        public RatingsController(ILogger<RatingsController> logger)
        {
            _logger = logger;
        }

        [HttpGet("~/ppai/{hash}/{mode}/{diff}")]
        public ActionResult<Dictionary<string, RatingResult>> Get(string hash, string mode, int diff)
        {
            Stopwatch sw = Stopwatch.StartNew();
            var modifiers = new List<(string, double)>() {
                ("SS", 0.85),
                ("none", 1),
                ("FS", 1.2),
                ("SFS", 1.5),
            };
            var results = new Dictionary<string, RatingResult>();
            foreach ((var name, var timescale) in modifiers) {
                results[name] = GetBLRatings(hash, mode, diff, timescale);
            }
            Console.WriteLine(sw.ElapsedMilliseconds);
            return results;
        }

        public RatingResult GetBLRatings(string hash, string mode, int diff, double timescale) {
            return new RatingResult {
                AIAcc = new InferPublish().GetAIAcc(hash, mode, diff, timescale)
            };
        }
    }
}