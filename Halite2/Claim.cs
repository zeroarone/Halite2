using Halite2.hlt;

namespace Halite2
{
    public class Claim
    {
        public Claim(int planetId, ClaimType type, Move move) {
            PlanetId = planetId;
            Type = type;
            Move = move;
        }
        public int PlanetId { get; }
        public ClaimType Type { get; }
        public Move Move { get; set; }
    }
}