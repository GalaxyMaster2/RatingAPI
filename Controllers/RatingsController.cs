using beatleader_analyzer;
using beatleader_parser;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Parser.Map;
using Parser.Map.Difficulty.V3.Base;
using RatingAPI.Utils;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace RatingAPI.Controllers
{
    public class LackMapCalculation
    {
        //[JsonPropertyName("multi_rating")]
        //public double MultiRating { get; set; } = 0;
        [JsonPropertyName("balanced_pass_diff")]
        public double PassRating { get; set; } = 0;
        //[JsonPropertyName("linear_rating")]
        //public double LinearRating { get; set; } = 0;

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
        [JsonPropertyName("star_rating")]
        public double StarRating { get; set; } = 0;
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

        public string CustomModeMapping(string mode)
        {
            switch (mode)
            {
                case "InvertedStandard":
                    return "Standard";
                default:
                    break;
            }

            return mode;
        }

        public DifficultyV3 CustomModeDataMapping(string mode, DifficultyV3 mapdata)
        {
            int numberOfLines = 4;
            bool is_ME = false;
            bool is_ME_or_NE = false;
            DifficultyV3 result = mapdata;
            switch (mode)
            {
                case "VerticalStandard":
                    result = Parser.Utils.ChiralitySupport.Mirror_Vertical(mapdata, false, is_ME_or_NE, is_ME);
                    break;
                case "HorizontalStandard":
                    result = Parser.Utils.ChiralitySupport.Mirror_Horizontal(mapdata, numberOfLines, false, is_ME_or_NE, is_ME);
                    break;
                case "InverseStandard":
                    result = Parser.Utils.ChiralitySupport.Mirror_Inverse(mapdata, numberOfLines, true, true, is_ME_or_NE, is_ME);
                    break;
                case "InvertedStandard":
                    result = Parser.Utils.ChiralitySupport.Mirror_Inverse(mapdata, numberOfLines, false, false, is_ME_or_NE, is_ME);
                    break;

            }

            return result;
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
                ("BFS", 1.2),
                ("BSF", 1.5),
            };
            var results = new Dictionary<string, RatingResult>();
            var difficulty = FormattingUtils.GetDiffLabel(diff);
            var mapPath = downloader.Map(hash);
            if (mapPath == null) return NotFound();
            BeatmapV3? mapset = null;
            try
            {
                mapset = parser.TryLoadPath(mapPath);
            }
            catch (FileNotFoundException e)
            {
                Directory.Delete(mapPath, true);
                mapPath = downloader.Map(hash);
                mapset = parser.TryLoadPath(mapPath);
            }
            if (mapset != null)
            {
                var map = mapset.Difficulties.FirstOrDefault(d => d.Characteristic == CustomModeMapping(mode) && (d.BeatMap._difficultyRank == diff || d.BeatMap._difficulty == difficulty));
                if (map == null) return results;
                foreach ((var name, var timescale) in modifiers)
                {
                    var njs = map.BeatMap._noteJumpMovementSpeed;
                    if (name == "BFS" || name == "BSF")
                    {
                        njs = (float)(((njs * timescale - njs) / 2 + njs) / timescale);
                    }
                    results[name] = GetBLRatings(map, mode, difficulty, mapset.Info._beatsPerMinute, njs, timescale);
                }
            }
            _logger.LogWarning("Took " + sw.ElapsedMilliseconds);

            return results;
        }

        [HttpGet("~/ppai2/link/{mode}/{diff}")]
        public ActionResult<Dictionary<string, RatingResult>> GetByLink(string mode, int diff, [FromQuery] string link)
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
            var mapset = parser.TryDownloadLink(link).FirstOrDefault();
            if (mapset != null)
            {
                var beatmapSets = mapset.Info._difficultyBeatmapSets.FirstOrDefault(s => s._beatmapCharacteristicName == mode);
                if (beatmapSets == null) return results;
                var data = beatmapSets._difficultyBeatmaps.FirstOrDefault(b => b._difficultyRank == diff);
                if (data == null) return results;
                var map = mapset.Difficulties.FirstOrDefault(d => d.Characteristic == mode && d.Difficulty == difficulty);
                if (map == null) return results;

                foreach ((var name, var timescale) in modifiers)
                {
                    results[name] = GetBLRatings(map, mode, difficulty, mapset.Info._beatsPerMinute, data._noteJumpMovementSpeed, timescale);
                }
            }
            _logger.LogWarning("Took " + sw.ElapsedMilliseconds);

            return results;
        }

        [HttpGet("~/ppai2/tag")]
        public ActionResult<string> Tag([FromQuery] float acc, [FromQuery] float pass, [FromQuery] float tech)
        {
            return ai.Tag(acc, tech, pass);
        }

        [HttpGet("~/json/{hash}/{mode}/{diff}/full/time-scale/{scale}")]
        public ActionResult<Dictionary<string, object>?> Get(string hash, string mode, int diff, double scale)
        {
            var difficulty = FormattingUtils.GetDiffLabel(diff);
            var mapPath = downloader.Map(hash);
            if (mapPath == null) return NotFound();
            var mapset = parser.TryLoadPath(mapPath, CustomModeMapping(mode), difficulty);
            if (mapset == null) return null;
            var beatmapSets = mapset.Info._difficultyBeatmapSets.FirstOrDefault();
            if (beatmapSets == null) return null;
            var data = beatmapSets._difficultyBeatmaps.FirstOrDefault();
            if (data == null) return null;
            var map = mapset.Difficulty;
            if (map == null) return null;

            return new Dictionary<string, object>
            {
                ["notes"] = ai.PredictHitsForMapNotes(CustomModeDataMapping(mode, map.Data), mapset.Info._beatsPerMinute, data._noteJumpMovementSpeed, scale)
            };
        }

        [NonAction]
        private PredictionResult? PredictHits(DifficultyV3 mapdata, double bpm, double njs, string characteristic, string difficulty, double timescale)
        {
            var result = ai.PredictHitsForMapAllNotes(mapdata, bpm, njs, timescale);

            var ratings = analyzer.GetRating(mapdata, characteristic, difficulty, (float)bpm, (float)njs, (float)timescale).FirstOrDefault();
            if (result == null || ratings == null) return null;

            ai.SetMapAccForHits(result.Notes);

            AccRating ar = new();
            var unmarkedNotes = result.Notes.ToList();

            foreach (var item in ratings.PerSwing)
            {
                var mapnote = mapdata.Notes.FirstOrDefault(n => Math.Abs(n.BpmTime - item.Time) < 0.001);
                if (mapnote == null) continue;

                var note = unmarkedNotes.FirstOrDefault(n => Math.Abs(n.Time - mapnote.Seconds) < 0.001);
                if (note != null)
                {
                    var lack = new LackMapCalculation
                    {
                        PassRating = item.Pass / 5,
                        TechRating = item.Tech * (-(Math.Pow(1.4, -(item.Pass / 5))) + 1) * 10 / 5,
                        LowNoteNerf = ratings.Nerf
                    };
                    lack = ModifyRatings(lack, njs * timescale, timescale);

                    note.Tech = (float)lack.TechRating;
                    note.Pass = (float)lack.PassRating;
                    note.Acc = (float)ar.GetRating(note.Acc, item.Pass, item.Tech);
                    unmarkedNotes.Remove(note);
                }
            }

            foreach (var note in unmarkedNotes)
            {
                note.Acc = (float)ar.GetRating(note.Acc, 0, 0);
            }

            // Interpolate values for notes with 0 tech or pass rating
            for (int i = 0; i < result.Notes.Length; i++)
            {
                if (result.Notes[i].Tech == 0 || result.Notes[i].Pass == 0)
                {
                    // Find nearest non-zero values before and after
                    float prevTech = 0, prevPass = 0, nextTech = 0, nextPass = 0;
                    int prevIndex = i - 1;
                    int nextIndex = i + 1;

                    while (prevIndex >= 0)
                    {
                        if (result.Notes[prevIndex].Tech != 0 && result.Notes[prevIndex].Pass != 0)
                        {
                            prevTech = result.Notes[prevIndex].Tech;
                            prevPass = result.Notes[prevIndex].Pass;
                            break;
                        }
                        prevIndex--;
                    }

                    while (nextIndex < result.Notes.Length)
                    {
                        if (result.Notes[nextIndex].Tech != 0 && result.Notes[nextIndex].Pass != 0)
                        {
                            nextTech = result.Notes[nextIndex].Tech;
                            nextPass = result.Notes[nextIndex].Pass;
                            break;
                        }
                        nextIndex++;
                    }

                    // Linear interpolation
                    if (prevTech != 0 && nextTech != 0)
                    {
                        float t = (float)(i - prevIndex) / (nextIndex - prevIndex);
                        result.Notes[i].Tech = prevTech + (nextTech - prevTech) * t;
                        result.Notes[i].Pass = prevPass + (nextPass - prevPass) * t;
                    }
                    else if (prevTech != 0)
                    {
                        result.Notes[i].Tech = prevTech;
                        result.Notes[i].Pass = prevPass;
                    }
                    else if (nextTech != 0)
                    {
                        result.Notes[i].Tech = nextTech;
                        result.Notes[i].Pass = nextPass;
                    }
                }
            }

            return result;
        }

        [HttpGet("~/ppai2/graph/{hash}/{mode}/{diff}/full")]
        public ActionResult<Dictionary<string, object>?> GetGraph(string hash, string mode, int diff)
        {
            var difficulty = FormattingUtils.GetDiffLabel(diff);
            var mapPath = downloader.Map(hash);
            if (mapPath == null) return NotFound();
            var mapset = parser.TryLoadPath(mapPath, CustomModeMapping(mode), difficulty);
            if (mapset == null) return null;
            var beatmapSets = mapset.Info._difficultyBeatmapSets.FirstOrDefault();
            if (beatmapSets == null) return null;
            var data = beatmapSets._difficultyBeatmaps.FirstOrDefault();
            if (data == null) return null;
            var map = mapset.Difficulty;
            if (map == null) return null;

            var mapdata = CustomModeDataMapping(mode, map.Data);
            var bpm = mapset.Info._beatsPerMinute;
            var njs = data._noteJumpMovementSpeed;

            return new Dictionary<string, object>
            {
                ["SS"] = PredictHits(mapdata, bpm, njs, mode, difficulty, 0.85),
                ["base"] = PredictHits(mapdata, bpm, njs, mode, difficulty, 1.0),
                ["FS"] = PredictHits(mapdata, bpm, njs, mode, difficulty, 1.2),
                ["SF"] = PredictHits(mapdata, bpm, njs, mode, difficulty, 1.5),
            };
        }

        [HttpGet("~/json/link/{mode}/{diff}/full/time-scale/{scale}")]
        public ActionResult<Dictionary<string, object>?> GetByLink(string mode, int diff, double scale, [FromQuery] string link)
        {
            var difficulty = FormattingUtils.GetDiffLabel(diff);
            var mapset = parser.TryDownloadLink(link).FirstOrDefault();
            if (mapset == null) return null;
            var beatmapSets = mapset.Info._difficultyBeatmapSets.FirstOrDefault(s => s._beatmapCharacteristicName == CustomModeMapping(mode));
            if (beatmapSets == null) return null;
            var data = beatmapSets._difficultyBeatmaps.FirstOrDefault(b => b._difficultyRank == diff);
            if (data == null) return null;
            var map = mapset.Difficulties.FirstOrDefault(d => d.Characteristic == CustomModeMapping(mode) && d.Difficulty == difficulty);
            if (map == null) return null;

            return new Dictionary<string, object>
            {
                ["notes"] = ai.PredictHitsForMapNotes(CustomModeDataMapping(mode, map.Data), mapset.Info._beatsPerMinute, data._noteJumpMovementSpeed, scale)
            };
        }

        public RatingResult GetBLRatings(DifficultySet map, string characteristic, string difficulty, double bpm, double njs, double timescale)
        {
            var mapdata = CustomModeDataMapping(characteristic, map.Data);
            var ratings = analyzer.GetRating(mapdata, characteristic, difficulty, (float)bpm, (float)njs, (float)timescale).FirstOrDefault();
            if (ratings == null) return new();
            var predictedAcc = ai.GetAIAcc(mapdata, bpm, njs, timescale);
            var lack = new LackMapCalculation
            {
                PassRating = ratings.Pass,
                TechRating = ratings.Tech * 10,
                LowNoteNerf = ratings.Nerf
                //LinearRating = ratings.Linear,
                //MultiRating = ratings.Multi
            };
            AccRating ar = new();
            var accRating = ar.GetRating(predictedAcc, ratings.Pass, ratings.Tech);
            accRating *= ratings.Nerf;
            lack = ModifyRatings(lack, njs * timescale, timescale);
            Curve curve = new();
            var pointList = curve.GetCurve(predictedAcc, accRating, lack);
            var star = curve.ToStars(0.96, accRating, lack, pointList);
            RatingResult result = new()
            {
                PredictedAcc = predictedAcc,
                AccRating = accRating,
                LackMapCalculation = lack,
                PointList = pointList,
                StarRating = star
            };
            return result;
        }

        //NJS buff for >24 njs
        public LackMapCalculation ModifyRatings(LackMapCalculation ratings, double njs, double timescale)
        {
            {
                double buff = 1f;
                if (njs > 24)
                {
                    buff = 1 + 0.01 * (njs - 24);
                }

                ratings.PassRating *= buff;
                ratings.TechRating *= buff;
            }

            return ratings;
        }
    }
}