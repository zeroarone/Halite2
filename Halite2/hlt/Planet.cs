using System;
using System.Collections.Generic;
using System.Linq;

namespace Halite2.hlt {
    public class Planet: Entity {

        private int remainingProduction;
        private int currentProduction;
        private int dockingSpots;
        private IList<int> dockedShips;

        public Planet(int owner, int id, double xPos, double yPos, int health,
                      double radius, int dockingSpots, int currentProduction,
                      int remainingProduction, List<int> dockedShips)
        :base(owner, id, xPos, yPos, health, radius)
        {
            this.dockingSpots = dockingSpots;
            this.currentProduction = currentProduction;
            this.remainingProduction = remainingProduction;
            this.dockedShips = dockedShips;
        }
        
        public double ClosestUnclaimedShipDistance { 
            get {
                var distance = ClosestUnclaimedShipAndDistance().Value;
                return distance == 0 ? double.MaxValue : distance;
            }
        }

        public Ship ClosestUnclaimedShip{
            get{
                var shipWithDistance = ClosestUnclaimedShipAndDistance();
                if(shipWithDistance.Value == 0){
                    return null;
                }
                return (Ship)shipWithDistance.Key;
            }
        }

        private KeyValuePair<Entity, double> ClosestUnclaimedShipAndDistance(){
            return ShipsByDistance.FirstOrDefault(s => {
                    var ship = (Ship)s.Key;
                    return ship.GetDockingStatus() == Ship.DockingStatus.Undocked && !ship.Claimed;
                });
        }

        public List<KeyValuePair<Entity, Double>> ShipsByDistance {get;set;}

        public List<KeyValuePair<Entity, Double>> NearbyEnemies {get;set;}

        public int RemainingProduction => remainingProduction;

        public int GetCurrentProduction() {
            return currentProduction;
        }

        public int GetDockingSpots() {
            return dockingSpots;
        }

        public IList<int> GetDockedShips() {
            return dockedShips;
        }

        public bool IsFull() {
            return dockedShips.Count == dockingSpots;
        }

        public void ClaimDockingSpot(int entity){
            dockedShips.Add(entity);
        }

        public bool IsOwned() {
            return GetOwner() != -1;
        }

        public bool IsOwnedBy(int player) { return GetOwner() == player; }
        
        public override string ToString() {
            return "Planet[" +
                    base.ToString() +
                    ", remainingProduction=" + remainingProduction +
                    ", currentProduction=" + currentProduction +
                    ", dockingSpots=" + dockingSpots +
                    ", dockedShips=" + dockedShips +
                    "]";
        }
    }
}