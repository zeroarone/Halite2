using System.Collections.Generic;
using System.Linq;

namespace Halite2.hlt
{
    public class Planet : Entity
    {
        private readonly int currentProduction;
        private readonly IList<int> dockedShips;
        private readonly int dockingSpots;

        public Ship ClosestUnclaimedShip {
            get {
                var shipWithDistance = ClosestUnclaimedShipAndDistance();
                if (shipWithDistance.Value == 0) return null;
                return (Ship) shipWithDistance.Key;
            }
        }

        public double ClosestUnclaimedShipDistance {
            get {
                var distance = ClosestUnclaimedShipAndDistance().Value;
                return distance == 0 ? double.MaxValue : distance;
            }
        }

        public List<KeyValuePair<Entity, double>> NearbyEnemies { get; set; }
        public double Points { get; set; }

        public int RemainingProduction { get; }

        public List<KeyValuePair<Entity, double>> ShipsByDistance { get; set; }

        public Planet(int owner, int id, double xPos, double yPos, int health,
            double radius, int dockingSpots, int currentProduction,
            int remainingProduction, List<int> dockedShips)
            : base(owner, id, xPos, yPos, health, radius) {
            this.dockingSpots = dockingSpots;
            this.currentProduction = currentProduction;
            RemainingProduction = remainingProduction;
            this.dockedShips = dockedShips;
        }

        private KeyValuePair<Entity, double> ClosestUnclaimedShipAndDistance() {
            return ShipsByDistance.FirstOrDefault(s => {
                var ship = (Ship) s.Key;
                //if (!IsFull() && GetOwner() == ship.GetOwner()) {
                //    var nextShipProducedInTurns = GetDockedShips().Count == 0 ? Int32.MaxValue : (72 - GetCurrentProduction()) / (Constants.BASE_PRODUCTIVITY * GetDockedShips().Count) % 6;
                //    var timeToTravel = (ship.GetDistanceTo(ship.GetClosestPoint(this)) - Constants.DOCK_RADIUS) / 7;
                //    if (timeToTravel >= nextShipProducedInTurns) {
                //        return false;
                //    }
                //}
                return ship.DockingStatus== DockingStatus.Undocked && !ship.Claimed;
            });
        }

        public int GetCurrentProduction() { return currentProduction; }

        public int GetDockingSpots() { return dockingSpots; }

        public IList<int> GetDockedShips() { return dockedShips; }

        public bool IsFull() { return dockedShips.Count == dockingSpots; }

        public void ClaimDockingSpot(int entity) { dockedShips.Add(entity); }

        public bool IsOwned() { return Owner != -1; }

        public bool IsOwnedBy(int player) { return Owner == player; }

        public override string ToString() {
            return "Planet[" +
                   base.ToString() +
                   ", remainingProduction=" + RemainingProduction +
                   ", currentProduction=" + currentProduction +
                   ", dockingSpots=" + dockingSpots +
                   ", dockedShips=" + dockedShips +
                   "]";
        }
    }
}