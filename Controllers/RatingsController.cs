using beatleader_analyzer;
using beatleader_parser;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace RatingAPI.Controllers
{
    public class RatingResult {
        public double AIAcc { get; set; }
        public List<double> lack_map_calculation {  get; set; }
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

        public RatingResult GetBLRatings(string hash, string mode, int diff, double timescale) {
            // Download the map
            Download.Map(hash);
            // Fetch the ratings (and send the data to Parser at the same time)
            var difficulty = GetDiffLabel(diff);
            List<double> ratings = Analyze.GetDataFromPathOne($"{Download.maps_dir}/{hash}", mode, difficulty, (float)timescale);
            // Now fetch the acc rating with the data ready to be used from Parser
            var acc = new InferPublish().GetAIAcc(Parse.GetBeatmap().Difficulties.FirstOrDefault(x => x.Characteristic == mode && x.Difficulty == difficulty), timescale);
            return new RatingResult {
                AIAcc = acc,
                lack_map_calculation = ratings
            };
        }
    }
}
