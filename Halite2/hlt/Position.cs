using System;

namespace Halite2.hlt
{
    public class Position
    {
        public virtual double Radius => 0;

        public double XPos { get; set; }
        public double YPos { get; set; }

        public Position(double xPos, double yPos) {
            XPos = xPos;
            YPos = yPos;
        }

        public double GetDistanceTo(Position target) {
            var dx = XPos - target.XPos;
            var dy = YPos - target.YPos;
            return Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2));
        }

        public int OrientTowardsInDeg(Position target) { return Util.AngleRadToDegClipped(OrientTowardsInRad(target)); }

        public double OrientTowardsInRad(Position target) {
            var dx = target.XPos - XPos;
            var dy = target.YPos - YPos;

            return Math.Atan2(dy, dx) + 2 * Math.PI;
        }

        public Position GetClosestPoint(Position target) {
            var radius = target.Radius + Constants.MIN_DISTANCE_FOR_CLOSEST_POINT;
            var angleRad = target.OrientTowardsInRad(this);

            var x = target.XPos + radius * Math.Cos(angleRad);
            var y = target.YPos + radius * Math.Sin(angleRad);

            return new Position(x, y);
        }

        protected bool Equals(Position other) { return XPos.Equals(other.XPos) && YPos.Equals(other.YPos); }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Position) obj);
        }

        public override int GetHashCode() {
            unchecked {
                return (XPos.GetHashCode() * 397) ^ YPos.GetHashCode();
            }
        }

        public static bool operator ==(Position left, Position right) { return Equals(left, right); }
        public static bool operator !=(Position left, Position right) { return !Equals(left, right); }

        public override string ToString() { return "Position(" + XPos + ", " + YPos + ")"; }
    }
}