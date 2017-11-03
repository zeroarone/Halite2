namespace Halite2.hlt
{
    public class ThrustMove : Move
    {
        public int Angle { get; }
        public int Thrust { get; }

        public ThrustMove(Ship ship, int angleDeg, int thrust)
            : base(MoveType.Thrust, ship) {
            Thrust = thrust;
            Angle = angleDeg;
        }
    }
}