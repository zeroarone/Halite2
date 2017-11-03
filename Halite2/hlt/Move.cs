namespace Halite2.hlt
{
    public class Move
    {
        public Ship Ship { get; }
        public MoveType Type { get; }

        public Move(MoveType type, Ship ship) {
            Type = type;
            Ship = ship;
        }
    }
}