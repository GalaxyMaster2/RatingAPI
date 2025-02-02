﻿using MathNet.Numerics.Interpolation;

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

            double accBuff = 0;
            if (accRating <= 8)
            {
                accBuff = 0.05 * (8 - accRating);
            }

            double multiBuff = 0.2 * lackRatings.MultiRating;
            double multiNerf = 0;
            if (lackRatings.MultiRating > 0.1)
            {
                multiNerf = -2 * Math.Log(lackRatings.MultiRating * 10, 1.666) / 100;
            }
            else
            {
                multiNerf = -0.02 * lackRatings.MultiRating;
            }


            double linearNerf = -2 * lackRatings.LinearRating / 100 * lackRatings.PassRating;

            double unlinearBuff = 0;
            if (lackRatings.LinearRating <= 0.20)
            {
                unlinearBuff = 1 * (0.2 - lackRatings.LinearRating);
            }

            double pivot = predictedAcc - 0.01;
            double upperBound = 1;
            double lowerBound = 0.80;
            foreach (var p in baseCurve)
            {
                double newY = p.y;
                if (p.x >= pivot)
                {
                    double xDist = (p.x - pivot) / (upperBound - pivot);
                    if (p.x > upperBound) xDist = 1;
                    newY *= 1 + accBuff * xDist;
                    newY *= 1 + unlinearBuff * xDist;
                    newY *= 1 + multiBuff * xDist;
                    newY *= 1 + linearNerf * xDist;
                }
                else
                {
                    double xDist = (p.x - pivot) / (lowerBound - pivot);
                    if (p.x < lowerBound) xDist = 1;
                    newY *= 1 + multiNerf * xDist;
                }


                points.Add(new(p.x, newY));
            }

            Point point = new();
            List<Point> curve = point.ToPoints(points).ToList();
            curve = curve.OrderBy(x => x.x).Reverse().ToList();

            return curve;
        }

        public double ToStars(double acc, double accRating, LackMapCalculation ratings, List<Point> curve)
        {
            double passPP = 15f * MathF.Exp(MathF.Pow((float)ratings.PassRating, 1 / 2.62f)) - 30f;
            if (double.IsInfinity(passPP) || double.IsNaN(passPP) || double.IsNegativeInfinity(passPP) || passPP < 0)
            {
                passPP = 0;
            }
            double accPP = Curve2(acc, curve) * accRating * 34f;
            double techPP = MathF.Exp((float)(1.9 * acc)) * 1.08f * ratings.TechRating;

            double pp = 650f * MathF.Pow((float)(passPP + accPP + techPP), 1.3f) / MathF.Pow(650f, 1.3f);

            return pp / 52;
        }

        public double Curve2(double acc, List<Point> curve)
        {
            int i = 0;
            for (; i < curve.Count; i++)
            {
                if (curve[i].x <= acc)
                {
                    break;
                }
            }

            if (i == 0)
            {
                i = 1;
            }

            double middle_dis = (acc - curve[i - 1].x) / (curve[i].x - curve[i - 1].x);
            return (float)(curve[i - 1].y + middle_dis * (curve[i].y - curve[i - 1].y));
        }
    }
}
