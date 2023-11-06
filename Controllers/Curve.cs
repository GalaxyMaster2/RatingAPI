namespace RatingAPI.Controllers
{
    public class Curve
    {
        public List<(double, double)> baseCurve = new()
        {
                (1.0, 7.424),
                (0.999, 6.241),
                (0.9975, 5.158),
                (0.995, 4.010),
                (0.9925, 3.241),
                (0.99, 2.700),
                (0.9875, 2.303),
                (0.985, 2.007),
                (0.9825, 1.786),
                (0.98, 1.618),
                (0.9775, 1.490),
                (0.975, 1.392),
                (0.9725, 1.315),
                (0.97, 1.256),
                (0.965, 1.167),
                (0.96, 1.094),
                (0.955, 1.039),
                (0.95, 1.000),
                (0.94, 0.931),
                (0.93, 0.867),
                (0.92, 0.813),
                (0.91, 0.768),
                (0.9, 0.729),
                (0.875, 0.650),
                (0.85, 0.581),
                (0.825, 0.522),
                (0.8, 0.473),
                (0.75, 0.404),
                (0.7, 0.345),
                (0.65, 0.296),
                (0.6, 0.256),
                (0.0, 0.000), };

        public List<Point> GetCurve(double predictedAcc, double accRating, LackMapCalculation lackRatings, string characteristic, double timescale)
        {
            Point point = new();
            var curve = point.ToPoints(baseCurve).ToList();

            var patternNerf = 1 - 0.1 * lackRatings.PatternRating;
            var patternBuff = 1 + 0.1 * lackRatings.PatternRating;
            var linearNerf = 1 - lackRatings.LinearRating / 100 * lackRatings.PassRating;
            var oneSaberNerf = 0.95;
            var sfBuff = 1 + lackRatings.TechRating / 200;
            var fsBuff = 1 + lackRatings.TechRating / 500;
            var accBuff = 1 + 0.025 * (8 - accRating);
            for (int i = 0; i < curve.Count; i++)
            {
                if (accRating <= 8 && curve[i].x >= predictedAcc) curve[i].y *= accBuff;
                if (timescale == 1.5) curve[i].y *= sfBuff;
                if (timescale == 1.2) curve[i].y *= fsBuff;
                if (curve[i].x > 0.95) curve[i].y *= patternBuff;
                else if (curve[i].x < 0.95) curve[i].y *= patternNerf;
                curve[i].y *= linearNerf;
                if (characteristic == "OneSaber") curve[i].y *= oneSaberNerf;
            }

            return curve;
        }
    }
}
