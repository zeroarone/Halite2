using System;
using System.Reflection.PortableExecutable;

namespace Halite2.hlt
{
    public class Collision
    {
        /// <summary>
        /// Test whether a given line segment intersects a circular area.
        /// </summary>
        /// <param name="start">The start of the segment.</param>
        /// <param name="end">The end of the segment.</param>
        /// <param name="circle">The circle to test against.</param>
        /// <param name="fudge">An additional safety zone to leave when looking for collisions. (Probably set it to ship radius 0.5)</param>
        /// <returns>true if the segment intersects, false otherwise</returns>
        public static double SegmentCircleIntersect(Position start, Position end, Entity circle, double fudge) {
            // Parameterize the segment as start + t * (end - start),
            // and substitute into the equation of a circle
            // Solve for t
            double circleRadius = circle.Radius;
            double startX = start.XPos;
            double startY = start.YPos;
            double endX = end.XPos;
            double endY = end.YPos;
            double centerX = circle.XPos;
            double centerY = circle.YPos;
            double dx = endX - startX;
            double dy = endY - startY;

            double a = square(dx) + square(dy);

            double b = -2 * (square(startX) - (startX * endX)
                             - (startX * centerX) + (endX * centerX)
                             + square(startY) - (startY * endY)
                             - (startY * centerY) + (endY * centerY));

            if (a == 0.0) {
                // Start and end are the same point
                return start.GetDistanceTo(circle) <= circleRadius + fudge ? 0 : -1;
            }

            // Time along segment when closest to the circle (vertex of the quadratic)
            double t = Math.Min(-b / (2 * a), 1.0);
            if (t < 0) {
                return -1;
            }

            double closestX = startX + dx * t;
            double closestY = startY + dy * t;
            var position = new Position(closestX, closestY);
            double closestDistance = position.GetDistanceTo(circle);
            return closestDistance <= circleRadius + fudge ? t : -1;
        }

        public static double square(double num) { return num * num; }
    }
}