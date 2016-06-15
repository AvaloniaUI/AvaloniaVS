// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See license.md file in the project root for full license information.

using System.Windows;

namespace AvaloniaVS.Controls.Internals
{
    /// <summary>
    /// Encapsulates a line equation
    /// </summary>
    internal struct LineEquation
    {
        // line equation y=mx+b

        private readonly double _m;
        private readonly double _b;

        private static double CalculateB(double m, double x, double y)
        {
            var b = y - (m * x);
            return b;
        }

        public Point IntersectWithHorizontalLine(double providedX, double y)
        {
            if (double.IsInfinity(_m))
            {
                return new Point(providedX, y);
            }

            var x = (y - _b) / _m;
            return new Point(x, y);
        }

        public LineEquation(Point p1, Point p2)
        {
            _m = (p2.Y - p1.Y) / (p2.X - p1.X);
            _b = CalculateB(_m, p1.X, p1.Y);
        }

        public Point IntersectWithVerticalLine(double providedY, double x)
        {
            if (double.IsInfinity(_m))
            {
                return new Point(x, providedY);
            }

            var y = _m * x + _b;
            return new Point(x, y);
        }
    }
}
