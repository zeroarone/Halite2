using System;
using System.Collections.Generic;
using System.Linq;

namespace Halite2.hlt
{
    public class Planet : Entity
    {
        public int CurrentProduction { get; }
        public IList<int> DockedShips { get; }
        public int DockingSpots { get; }
        public bool IsFull => DockedShips.Count == DockingSpots;
        public bool IsOwned => Owner != -1;
        public double Points { get; set; }
        public int RemainingProduction { get; }
        public List<KeyValuePair<Entity, double>> ShipsByDistance { get; set; }

        public Planet(int owner, int id, double xPos, double yPos, int health,
            double radius, int dockingSpots, int currentProduction,
            int remainingProduction, List<int> dockedShips)
            : base(owner, id, xPos, yPos, health, radius) {
            DockingSpots = dockingSpots;
            CurrentProduction = currentProduction;
            RemainingProduction = remainingProduction;
            DockedShips = dockedShips;
        }

        public Ship GetClosestUnclaimedShip {
            get {
                var shipWithDistance = GetClosestUnclaimedShipAndDistance;
                if (shipWithDistance.Value == 0) return null;
                return (Ship) shipWithDistance.Key;
            }
        }

        private KeyValuePair<Entity, double> GetClosestUnclaimedShipAndDistance {
            get {
                return ShipsByDistance.FirstOrDefault(s => {
                    var ship = (Ship) s.Key;
                    return ship.DockingStatus == DockingStatus.Undocked && !ship.Claimed;
                });
            }
        }

        public int FramesToNextSpawn => DockedShips.Count == 0 ? Int32.MaxValue : (int)Math.Ceiling(((double)Constants.RESOURCES_FOR_SHIP_PRODUCTION - CurrentProduction) / (Constants.BASE_PRODUCTIVITY * DockedShips.Count));

        public bool IsOwnedBy(int player) { return Owner == player; }

        public int GetAvailableDockingPorts(int playerId) {
            if (IsOwnedBy(playerId) || !IsOwned)
                return DockingSpots - DockedShips.Count;
            return 0;
        }

        public override string ToString() {
            return "Planet[" +
                   base.ToString() +
                   ", remainingProduction=" + RemainingProduction +
                   ", currentProduction=" + CurrentProduction +
                   ", dockingSpots=" + DockingSpots +
                   ", dockedShips=" + DockedShips +
                   "]";
        }
    }
}