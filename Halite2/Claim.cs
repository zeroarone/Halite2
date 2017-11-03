namespace Halite2
{
    public class Claim
    {
        public Claim(int planetId, ClaimType type) {
            PlanetId = planetId;
            Type = type;
        }
        public int PlanetId { get; }
        public ClaimType Type { get; }
    }
}