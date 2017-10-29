using System.Collections.Generic;
using System.Linq;

namespace Halite2.hlt {
    public class Planet: Entity {

        private int remainingProduction;
        private int currentProduction;
        private int dockingSpots;
        private IList<int> dockedShips;
        private static Dictionary<int, List<Ship>> shipsClaimed = new Dictionary<int, List<Ship>>();
        private static Dictionary<int, bool> ownedPreviousRound = new Dictionary<int, bool>();

        public Planet(int owner, int id, double xPos, double yPos, int health,
                      double radius, int dockingSpots, int currentProduction,
                      int remainingProduction, List<int> dockedShips)
        :base(owner, id, xPos, yPos, health, radius)
        {
            this.dockingSpots = dockingSpots;
            this.currentProduction = currentProduction;
            this.remainingProduction = remainingProduction;
            this.dockedShips = dockedShips;
            if(!shipsClaimed.ContainsKey(id)){
                shipsClaimed.Add(id, new List<Ship>());
            }
            // Clear out the claimed ships, we need to start over on the claims.
            if(ownedPreviousRound.ContainsKey(id) && ownedPreviousRound[id] && owner == 0){
                shipsClaimed[id] = new List<Ship>();
            }
            ownedPreviousRound[id] = owner != 0;
        }

        public static Dictionary<int,List<Ship>> ShipsClaimed => shipsClaimed;

        public double ClosestUnclaimedShipDistance { 
            get {
                var distance = ClosestUnclaimedShipAndDistance().Key;
                return distance == 0 ? double.MaxValue : distance;
            }
        }

        public Ship ClosestUnclaimedShip{
            get{
                var shipWithDistance = ClosestUnclaimedShipAndDistance();
                if(shipWithDistance.Key == 0){
                    return null;
                }
                return (Ship)shipWithDistance.Value;
            }
        }

        private KeyValuePair<double, Entity> ClosestUnclaimedShipAndDistance(){
            return ShipsByDistance.FirstOrDefault(s => {
                    var ship = (Ship)s.Value;
                    return ship.GetDockingStatus() == Ship.DockingStatus.Undocked && !ship.Claimed;
                });
        }

        public void AddShipClaim(Ship ship){
            shipsClaimed[GetId()].Add(ship);
        }

        public List<KeyValuePair<double, Entity>> ShipsByDistance {get;set;}

        public List<KeyValuePair<double, Entity>> NearbyEnemies {get;set;}

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