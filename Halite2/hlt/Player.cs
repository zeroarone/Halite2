using System.Collections.Generic;

namespace Halite2.hlt
{
    public class Player
    {
        private readonly Dictionary<int, Ship> ships;

        public int Id { get; }

        public IDictionary<int, Ship> Ships => ships;

        public Player(int id, Dictionary<int, Ship> ships) {
            Id = id;
            this.ships = ships;
        }

        public Ship GetShip(int entityId) { return ships[entityId]; }
    }
}