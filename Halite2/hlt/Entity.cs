namespace Halite2.hlt
{

    public class Entity : Position
    {
        private readonly double radius;

        public int Id { get; }
        public int Owner { get; }
        public int Health { get; }
        public override double Radius => radius;
        public bool IsOwnedBy(int player) { return Owner == player; }

        public Entity(int owner, int id, double xPos, double yPos, int health, double radius) : base(xPos, yPos) {
            Owner = owner;
            Id = id;
            Health = health;
            this.radius = radius;
        }

        public override string ToString() {
            return $"Entity[{base.ToString()}, owner={Owner}, id={Id}, health={Health}, radius={radius}]";
        }
    }
}