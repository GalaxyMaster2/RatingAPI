using beatleader_analyzer;
using beatleader_parser;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace RatingAPI.Controllers
{
    public class LackMapCalculation
    {
        [JsonPropertyName("avg_pattern_rating")]
        public double PatternRating { get; set; } = 0;
        [JsonPropertyName("balanced_pass_diff")]
        public double PassRating { get; set; } = 0;
        [JsonPropertyName("linear_rating")]
        public double LinearRating { get; set; } = 0;

        [JsonPropertyName("balanced_tech")]
        public double TechRating { get; set; } = 0;
        [JsonPropertyName("low_note_nerf")]
        public double LowNoteNerf { get; set; } = 0;
    }

    public class RatingResult
    {
        [JsonPropertyName("AIAcc")]
        public double AccRating { get; set; } = 0;
        [JsonPropertyName("lack_map_calculation")]
        public LackMapCalculation LackMapCalculation { get; set; } = new();
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
            var mapset = parser.TryLoadPath(downloader.Map(hash)).FirstOrDefault();
            if (mapset == null) return new();
            var beatmapSets = mapset.Info._difficultyBeatmapSets.FirstOrDefault(x => x._beatmapCharacteristicName == mode);
            if (beatmapSets == null) return new();
            var data = beatmapSets._difficultyBeatmaps.FirstOrDefault(x => x._difficulty == diff);
            if (data == null) return new();
            var map = mapset.Difficulties.FirstOrDefault(x => x.Characteristic == mode && x.Difficulty == diff);
            if (map == null) return new();
            var ratings = analyzer.GetRating(map.Data, mode, diff, mapset.Info._beatsPerMinute, (float)timescale).FirstOrDefault();
            if (ratings == null) return new();
            var acc = new InferPublish().GetAIAcc(map, mapset.Info._beatsPerMinute, data._noteJumpMovementSpeed, timescale);
            var lack = new LackMapCalculation
            {
                PassRating = ratings.Pass,
                TechRating = ratings.Tech,
                LowNoteNerf = ratings.Nerf,
                LinearRating = ratings.Linear,
                PatternRating = ratings.Pattern
            };
            RatingResult result = new()
            {
                AccRating = acc,
                LackMapCalculation = lack,
            };
            return result;
        }
    }
}
