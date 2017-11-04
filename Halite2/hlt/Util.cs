using System;

namespace Halite2.hlt
{
    public class Util
    {
        public static int AngleRadToDegClipped(double angleRad) {
            var degUnclipped = (long) Math.Round(angleRad / Math.PI * 180);
            // Make sure return value is in [0, 360) as required by game engine.
            return (int) ((degUnclipped % 360L + 360L) % 360L);
        }

        // Returns true if the lines intersect, otherwise false.
        public static bool LinesIntersect(double p0_x, double p0_y, double p1_x, double p1_y, double p2_x, double p2_y, double p3_x, double p3_y)
        {
            double s1_x, s1_y, s2_x, s2_y;
            s1_x = p1_x - p0_x;     
            s1_y = p1_y - p0_y;
            s2_x = p3_x - p2_x;     
            s2_y = p3_y - p2_y;

            double s, t;
            s = (-s1_y * (p0_x - p2_x) + s1_x * (p0_y - p2_y)) / (-s2_x * s1_y + s1_x * s2_y);
            t = ( s2_x * (p0_y - p2_y) - s2_y * (p0_x - p2_x)) / (-s2_x * s1_y + s1_x * s2_y);

            if (s >= 0 && s <= 1 && t >= 0 && t <= 1)
            {
                return true;
            }

            return false; // No collision
        }

        public static double EPSILON = 0.000001;

        /**
        * Calculate the cross product of two points.
        * @param a first point
        * @param b second point
        * @return the value of the cross product
        */
        public static double crossProduct(Position a, Position b) {
            return a.XPos * b.YPos - b.XPos * a.YPos;
        }

        /**
        * Check if bounding boxes do intersect. If one bounding box
        * touches the other, they do intersect.
        * @param a first bounding box
        * @param b second bounding box
        * @return <code>true</code> if they intersect,
        *         <code>false</code> otherwise.
        */
        public static bool doBoundingBoxesIntersect(Position[] a, Position[] b) {
            return a[0].XPos <= b[1].XPos && a[1].XPos >= b[0].XPos && a[0].YPos <= b[1].YPos && a[1].YPos >= b[0].YPos;
        }

        /**
        * Checks if a Point is on a line
        * @param a line (interpreted as line, although given as line
        *                segment)
        * @param b point
        * @return <code>true</code> if point is on line, otherwise
        *         <code>false</code>
        */
        public static bool isPointOnLine(LineSegment a, Position b) {
            // Move the image, so that a.first is on (0|0)
            LineSegment aTmp = new LineSegment(new Position(0, 0), new Position(
                    a.second.XPos - a.first.XPos, a.second.YPos - a.first.YPos));
            Position bTmp = new Position(b.XPos - a.first.XPos, b.YPos - a.first.YPos);
            double r = crossProduct(aTmp.second, bTmp);
            return Math.Abs(r) < EPSILON;
        }

        /**
        * Checks if a point is right of a line. If the point is on the
        * line, it is not right of the line.
        * @param a line segment interpreted as a line
        * @param b the point
        * @return <code>true</code> if the point is right of the line,
        *         <code>false</code> otherwise
        */
        public static bool isPointRightOfLine(LineSegment a, Position b) {
            // Move the image, so that a.first is on (0|0)
            LineSegment aTmp = new LineSegment(new Position(0, 0), new Position(
                    a.second.XPos - a.first.XPos, a.second.YPos - a.first.YPos));
            Position bTmp = new Position(b.XPos - a.first.XPos, b.YPos - a.first.YPos);
            return crossProduct(aTmp.second, bTmp) < 0;
        }

        /**
        * Check if line segment first touches or crosses the line that is
        * defined by line segment second.
        *
        * @param first line segment interpreted as line
        * @param second line segment
        * @return <code>true</code> if line segment first touches or
        *                           crosses line second,
        *         <code>false</code> otherwise.
        */
        public static bool lineSegmentTouchesOrCrossesLine(LineSegment a,
                LineSegment b) {
            return isPointOnLine(a, b.first)
                    || isPointOnLine(a, b.second)
                    || (isPointRightOfLine(a, b.first) ^ isPointRightOfLine(a,
                            b.second));
        }

        /**
        * Check if line segments intersect
        * @param a first line segment
        * @param b second line segment
        * @return <code>true</code> if lines do intersect,
        *         <code>false</code> otherwise
        */
        public static bool doLinesIntersect(LineSegment a, LineSegment b) {
            Position[] box1 = a.getBoundingBox();
            Position[] box2 = b.getBoundingBox();
            return doBoundingBoxesIntersect(box1, box2)
                    && lineSegmentTouchesOrCrossesLine(a, b)
                    && lineSegmentTouchesOrCrossesLine(b, a);
        }
    }

    public class LineSegment{
        public Position first {get;set;}
        public Position second {get;set;}
        public LineSegment(Position first, Position second){
            this.first = first;
            this.second = second;
        }
        public Position[] getBoundingBox() {
        Position[] result = new Position[2];
        result[0] = new Position(Math.Min(first.XPos, second.XPos), Math.Min(first.YPos, second.YPos));
        result[1] = new Position(Math.Max(first.XPos, second.XPos), Math.Max(first.YPos, second.YPos));
        return result;
    }
    }
}