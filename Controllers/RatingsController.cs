using beatleader_analyzer;
using beatleader_analyzer.BeatmapScanner.Data;
using beatleader_parser;
using Microsoft.AspNetCore.Mvc;
using RatingAPI.Utils;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace RatingAPI.Controllers
{
    
    public class LackMapCalculation {
        [JsonPropertyName("avg_pattern_rating")]
        public double PatternRating { get; set; }
        [JsonPropertyName("balanced_pass_diff")]
        public double PassRating { get; set; }
        [JsonPropertyName("linear_rating")]
        public double LinearRating { get; set; }
        
        [JsonPropertyName("balanced_tech")]
        public double TechRating { get; set; }
        [JsonPropertyName("low_note_nerf")]
        public double LowNoteNerf { get; set; }
    }

    public class RatingResult {
        [JsonPropertyName("AIAcc")]
        public double AccRating { get; set; }
        [JsonPropertyName("lack_map_calculation")]
        public LackMapCalculation LackMapCalculation { get; set; }
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
                Console.WriteLine("Acc:" + results[name].AccRating + " pass:" + results[name].LackMapCalculation.PassRating);
            }
            Console.WriteLine(sw.ElapsedMilliseconds);

            return results;
        }

        public RatingResult GetBLRatings(string hash, string mode, int diff, double timescale) {
            // Download the map
            Download.Map(hash);
            // Fetch the ratings (and send the data to Parser at the same time)
            var difficulty = FormattingUtils.GetDiffLabel(diff);
            List<Ratings> ratings = Analyze.GetDataFromPathOne($"{Download.maps_dir}/{hash}", mode, difficulty, (float)timescale);
            if(ratings == null || ratings.Count == 0) // Error during the data fetching, early return.
            {
                return new RatingResult
                {
                    AccRating = 0,
                    LackMapCalculation = new() { 
                        PassRating = 0, 
                    TechRating = 0, 
                    LowNoteNerf = 0, 
                    LinearRating = 0, 
                    PatternRating = 0 
                    }
                };
            }
            // Now fetch the acc rating with the data ready to be used from Parser
            var acc = new InferPublish().GetAIAcc(Parse.GetBeatmap().Difficulties.FirstOrDefault(x => x.Characteristic == mode && x.Difficulty == difficulty), timescale);
            return new RatingResult {
                AccRating = acc,
                LackMapCalculation = new () { 
                    PassRating = ratings[0].Pass, 
                    TechRating = ratings[0].Tech, 
                    LowNoteNerf = ratings[0].Nerf, 
                    LinearRating = ratings[0].Linear, 
                    PatternRating = ratings[0].Pattern 
                }
            };
        }
    }
}
