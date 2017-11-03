namespace Halite2.hlt
{
    public class DockMove : Move
    {
        public long DestinationId { get; }

        public DockMove(Ship ship, Planet planet) : base(MoveType.Dock, ship) { DestinationId = planet.Id; }
    }
}