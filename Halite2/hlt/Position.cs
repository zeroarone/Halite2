using System;

namespace Halite2.hlt {

    public class Position {

        private double xPos;
        private double yPos;

        public Position(double xPos, double yPos) {
            this.xPos = xPos;
            this.yPos = yPos;
        }

        public double GetXPos() {
            return xPos;
        }

        public double GetYPos() {
            return yPos;
        }

        public double GetDistanceTo(Position target) {
            double dx = xPos - target.GetXPos();
            double dy = yPos - target.GetYPos();
            return Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2));
        }

        public virtual double GetRadius() {
            return 0;
        }

        public int OrientTowardsInDeg(Position target) {
            return Util.AngleRadToDegClipped(OrientTowardsInRad(target));
        }

        public double OrientTowardsInRad(Position target) {
            double dx = target.GetXPos() - xPos;
            double dy = target.GetYPos() - yPos;

            return Math.Atan2(dy, dx) + 2 * Math.PI;
        }

        public Position GetClosestPoint(Position target) {
            double radius = target.GetRadius() + Constants.MIN_DISTANCE_FOR_CLOSEST_POINT;
            double angleRad = target.OrientTowardsInRad(this);

            double x = target.GetXPos() + radius * Math.Cos(angleRad);
            double y = target.GetYPos() + radius * Math.Sin(angleRad);

            return new Position(x, y);
        }

        protected bool Equals(Position other) {
            return xPos.Equals(other.xPos) && yPos.Equals(other.yPos);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Position) obj);
        }

        public override int GetHashCode() {
            unchecked {
                return (xPos.GetHashCode() * 397) ^ yPos.GetHashCode();
            }
        }

        public static bool operator ==(Position left, Position right) { return Equals(left, right); }
        public static bool operator !=(Position left, Position right) { return !Equals(left, right); }

        public override string ToString() {
            return "Position(" + xPos + ", " + yPos + ")";
        }
    }
}
