namespace Halite2.hlt
{
    public class ThrustMove : Move
    {
        public int Angle { get; }
        public int Thrust { get; }
        public bool? ClockWise { get; set; }

        public ThrustMove(Ship ship, int angleDeg, int thrust, bool clockwise = false)
            : base(MoveType.Thrust, ship) {
            Thrust = thrust;
            Angle = angleDeg;
        }
    }
}