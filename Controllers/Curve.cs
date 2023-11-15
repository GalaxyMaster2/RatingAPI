using MathNet.Numerics.Interpolation;

namespace RatingAPI.Controllers
{
    public class Curve
    {
        public List<(double x, double y)> baseCurve = new()
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
                (0.0, 0.000)};

        public List<Point> GetCurve(double predictedAcc, double accRating, LackMapCalculation lackRatings)
        {
            List<(double x, double y)> points = new();

            double buff = 1;
            if(lackRatings.LinearRating <= 0.20)
            {
                buff = 1 + 0.5 * (0.2 - lackRatings.LinearRating);
            }

            foreach (var p in baseCurve)
            {
                double newY = p.y;
                if (p.x >= predictedAcc - 0.01)
                {
                    if (accRating <= 8) newY *= 1 + 0.025 * (8 - accRating);
                    newY *= buff;
                    newY *= 1 + 0.1 * lackRatings.PatternRating;
                    newY *= (1 - lackRatings.LinearRating / 100 * lackRatings.PassRating);
                }
                else
                {
                    newY *= 1 - 0.1 * lackRatings.PatternRating;
                }


                points.Add(new(p.x, newY));
            }

            List<double> xValues = points.Select(point => point.x).ToList();
            List<double> yValues = points.Select(point => point.y).ToList();
            
            IInterpolation interpolation = CubicSpline.InterpolateNatural(xValues, yValues);

            List<(double, double)> modifiedList = new();

            foreach (var p in baseCurve)
            {
                double x = p.x;
                double interpolatedY = interpolation.Interpolate(x);
                modifiedList.Add((x, interpolatedY));
            }

            Point point = new();
            List<Point> curve = point.ToPoints(modifiedList).ToList();
            curve = curve.OrderBy(x => x.x).Reverse().ToList();

            return curve;
        }
    }
}
