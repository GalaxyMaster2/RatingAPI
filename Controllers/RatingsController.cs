using beatleader_analyzer;
using beatleader_analyzer.BeatmapScanner.Data;
using beatleader_parser;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace RatingAPI.Controllers
{
    public class RatingResult {
        public double AIAcc { get; set; } = 0;
        public List<double> lack_map_calculation { get; set; } = new();
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
                results[name] = GetBLRatings(hash, mode, GetDiffLabel(diff), timescale);
                Console.WriteLine("Acc:" + results[name].AIAcc + " pass:" + results[name].lack_map_calculation[0]);
            }
            Console.WriteLine(sw.ElapsedMilliseconds);

            return results;
        }

        public string GetDiffLabel(int difficulty)
        {
            switch (difficulty)
            {
                case 1: return "Easy";
                case 3: return "Normal";
                case 5: return "Hard";
                case 7: return "Expert";
                case 9: return "ExpertPlus";
                default: return difficulty.ToString();
            }
        }

        public RatingResult GetBLRatings(string hash, string mode, string diff, double timescale) {
            Downloader downloader = new();
            Analyze analyzer = new();
            Parse parser = new();
            RatingResult result = new();
            var map = parser.TryLoadPath(downloader.Map(hash)).FirstOrDefault()?.Difficulties.FirstOrDefault(x => x.Characteristic == mode && x.Difficulty == diff);
            if (map == null)
                return result;

            List<Ratings> ratings = analyzer.GetRating(map.Data, mode, diff, (float)timescale);
            if (ratings == null || ratings.Count == 0)
                return result;

            var acc = new InferPublish().GetAIAcc(map, timescale);
            result.AIAcc = acc;
            result.lack_map_calculation = new() { ratings[0].Pass, ratings[0].Tech, ratings[0].Nerf, ratings[0].Linear, ratings[0].Pattern };
            return result;
        }
    }
}
