using System;
using System.Collections.Generic;
using System.Linq;

namespace Halite2.hlt {
    public class Planet: Entity {

        private int remainingProduction;
        private int currentProduction;
        private int dockingSpots;
        private IList<int> dockedShips;

        public static Dictionary<int, Dictionary<int, Ship>> ClaimedDockingPorts = new Dictionary<int, Dictionary<int, Ship>>();

        public Planet(int owner, int id, double xPos, double yPos, int health,
                      double radius, int dockingSpots, int currentProduction,
                      int remainingProduction, List<int> dockedShips)
        :base(owner, id, xPos, yPos, health, radius)
        {
            this.dockingSpots = dockingSpots;
            this.currentProduction = currentProduction;
            this.remainingProduction = remainingProduction;
            this.dockedShips = dockedShips;

            if(!ClaimedDockingPorts.ContainsKey(id)){
                ClaimedDockingPorts.Add(id, new Dictionary<int, Ship>());
            }
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
        public Ship DefendingShip { get; set; }

        public int GetCurrentProduction() {
            return currentProduction;
        }

        public Position GetClosestEmptyDockingPort(Ship ship){
            if(ClaimedDockingPorts[GetId()].Any(kvp => kvp.Value.GetId() == ship.GetId()))
                return DockingPorts[ClaimedDockingPorts[GetId()].Where(kvp => kvp.Value.GetId() == ship.GetId()).Select(kvp => kvp.Key).Single()];
                

            Position closestPosition = null;
            double closestDistance = Double.MaxValue;
            for(int i = 0; i < GetDockingSpots(); i++){
                if(!ClaimedDockingPorts[GetId()].ContainsKey(i)){
                    var distance = ship.GetDistanceTo(DockingPorts[i]);
                    if(distance < closestDistance){
                        closestDistance = distance;
                        closestPosition = DockingPorts[i];
                    }
                }
            }

            return closestPosition;
        }

        private List<Position> dockingPorts;
        public List<Position> DockingPorts{
            get{
                if(dockingPorts == null){
                    dockingPorts = new List<Position>(GetDockingSpots());
                    for(int i = 0; i < GetDockingSpots(); i++){
                        var port = new Position(GetXPos() + GetRadius()*Math.Cos((i*2*Math.PI)/GetDockingSpots()), GetYPos() + GetRadius()*Math.Sin((i*2*Math.PI)/GetDockingSpots()));
                        dockingPorts.Add(port);
                    }
                }
                return dockingPorts;                
            }
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

        public void ClaimDockingSpot(Ship ship, Position dockingPort){
            dockedShips.Add(ship.GetId());
            for(int i = 0; i < DockingPorts.Count; i++){
                if(DockingPorts[i] == dockingPort){
                    if(!ClaimedDockingPorts[GetId()].ContainsKey(i))
                        ClaimedDockingPorts[GetId()].Add(i, ship);
                    return;
                }                 
            }
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