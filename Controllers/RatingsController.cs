using beatleader_analyzer;
using beatleader_parser;
using Microsoft.AspNetCore.Mvc;
using Parser.Map;
using RatingAPI.Utils;
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
        [JsonPropertyName("predicted_acc")]
        public double PredictedAcc { get; set; } = 0;
        [JsonPropertyName("acc_rating")]
        public double AccRating { get; set; } = 0;
        [JsonPropertyName("lack_map_calculation")]
        public LackMapCalculation LackMapCalculation { get; set; } = new();
        [JsonPropertyName("pointlist")]
        public List<Point> PointList { get; set; } = new();
    }

    public class Point
    {
        public double x { get; set; } = 0;
        public double y { get; set; } = 0;

        public Point()
        {

        }

        public Point(double x, double y)
        {
            this.x = x;
            this.y = y;
        }

        public List<Point> ToPoints(List<(double x, double y)> curve)
        {
            List<Point> points = new();

            foreach (var p in curve)
            {
                points.Add(new(p.x, p.y));
            }

            return points;
        }
    }

    public class RatingsController : Controller
    {
        private readonly ILogger<RatingsController> _logger;
        private readonly Downloader downloader;
        private readonly Analyze analyzer = new();
        private readonly Parse parser = new();

        private readonly InferPublish ai = new();

        public RatingsController(IConfiguration configuration, ILogger<RatingsController> logger)
        {
            _logger = logger;
            downloader = new(configuration.GetValue<string>("MapsPath") ?? "");
        }

        [HttpGet("~/ppai2/{hash}/{mode}/{diff}")]
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
            var difficulty = FormattingUtils.GetDiffLabel(diff);
            var mapset = parser.TryLoadPath(downloader.Map(hash), mode, difficulty);
            if (mapset != null)
            {
                var beatmapSets = mapset.Info._difficultyBeatmapSets.FirstOrDefault();
                if (beatmapSets == null) return results;
                var data = beatmapSets._difficultyBeatmaps.FirstOrDefault();
                if (data == null) return results;
                var map = mapset.Difficulty;
                if (map == null) return results;
                foreach ((var name, var timescale) in modifiers)
                {
                    results[name] = GetBLRatings(map, mode, difficulty, mapset.Info._beatsPerMinute, data._noteJumpMovementSpeed, timescale);
                }
            }
            _logger.LogWarning("Took " + sw.ElapsedMilliseconds);

            return results;
        }

        [HttpGet("~/json/{hash}/{mode}/{diff}/full/time-scale/{scale}")]
        public ActionResult<Dictionary<string, object>?> Get(string hash, string mode, int diff, double scale)
        {
            var difficulty = FormattingUtils.GetDiffLabel(diff);
            var mapset = parser.TryLoadPath(downloader.Map(hash), mode, difficulty);
            if (mapset == null) return null;
            var beatmapSets = mapset.Info._difficultyBeatmapSets.FirstOrDefault();
            if (beatmapSets == null) return null;
            var data = beatmapSets._difficultyBeatmaps.FirstOrDefault();
            if (data == null) return null;
            var map = mapset.Difficulty;
            if (map == null) return null;

            return new Dictionary<string, object>
            {
                ["notes"] = ai.PredictHitsForMapNotes(map, mapset.Info._beatsPerMinute, data._noteJumpMovementSpeed, scale)
            };
        }

        public RatingResult GetBLRatings(DifficultySet map, string characteristic, string difficulty, double bpm, double njs, double timescale) {
            
            var ratings = analyzer.GetRating(map.Data, characteristic, difficulty, (float)bpm, (float)timescale).FirstOrDefault();
            if (ratings == null) return new();
            var predictedAcc = ai.GetAIAcc(map, bpm, njs, timescale);
            var lack = new LackMapCalculation
            {
                PassRating = ratings.Pass,
                TechRating = ratings.Tech * 10,
                LowNoteNerf = ratings.Nerf,
                LinearRating = ratings.Linear,
                PatternRating = ratings.Pattern
            };
            AccRating ar = new();
            var accRating = ar.GetRating(predictedAcc, ratings.Pass, ratings.Tech);
            lack = ModifyRatings(lack, njs * timescale);
            Curve curve = new();
            var pointList = curve.GetCurve(predictedAcc, accRating, lack);
            RatingResult result = new()
            {
                PredictedAcc = predictedAcc,
                AccRating = accRating,
                LackMapCalculation = lack,
                PointList = pointList
            };
            return result;
        }

        public LackMapCalculation ModifyRatings(LackMapCalculation ratings, double njs)
        {
            double buff = 1f;
            if (njs > 20)
            {
                buff = 1 + 0.01 * (njs - 20);
            }

            ratings.PassRating *= buff;
            ratings.TechRating *= buff;

            return ratings;
        }
    }
}
