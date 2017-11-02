using System;
using System.Collections.Generic;
using System.Linq;
using Halite2.hlt;

namespace Halite2
{
    public class MyBot
    {

        public static void Main(string[] args) {
            try {
                string name = args.Length > 0 ? args[0] : "Ze-rone";
                
                Networking networking = new Networking();
                GameMap gameMap = networking.Initialize(name);

                Dictionary<int, int> ownedPlanets = new Dictionary<int, int>();
                List<Move> moveList = new List<Move>();
                int step = 1;
                for (;;) {
                    DebugLog.AddLog($"New Move: {step++}----------------------------------------------------------------------");
                    moveList.Clear();
                    gameMap.UpdateMap(Networking.ReadLineIntoMetadata());

                    List<Planet> sortedPlanets = new List<Planet>();
                    Dictionary<Planet, int> beingAttacked = new Dictionary<Planet, int>();

                    foreach(Planet planet in gameMap.GetAllPlanets().Select(kvp => kvp.Value)){
                        if (ownedPlanets.ContainsKey(planet.GetId()) && ownedPlanets[planet.GetId()] != planet.GetOwner() && ownedPlanets[planet.GetId()] != -1) {
                            ClearClaimedDockingPorts(gameMap);
                        }

                        ownedPlanets[planet.GetId()] = planet.GetOwner();

                        sortedPlanets.Add(planet);
                        if(planet.IsOwnedBy(gameMap.GetMyPlayerId())){
                            var dockedShips = planet.GetDockedShips().Select(s => gameMap.GetShip(s));
                            planet.NearbyEnemies = new List<KeyValuePair<double, Entity>>();
                            foreach(var ship in dockedShips){
                                planet.NearbyEnemies.AddRange(gameMap.NearbyEntitiesByDistance(ship).Where(e => e.Value.GetType() == typeof(Ship) && e.Value.GetOwner() != gameMap.GetMyPlayerId() && e.Key < Constants.WEAPON_RADIUS + 1).OrderBy(kvp => kvp.Key));                                
                            }
                            planet.NearbyEnemies.Sort((kvp1, kvp2) => kvp1.Key.CompareTo(kvp2.Key));
                            
                            if(planet.NearbyEnemies.Count > 0){
                                DebugLog.AddLog("Found attacking enemies, defending.");
                                beingAttacked.Add(planet, planet.NearbyEnemies.Count * 2);
                            }
                        }
                        else if(planet.IsOwned()){
                            Planet.ClaimedDockingPorts[planet.GetId()].Clear();
                        }
                        planet.ShipsByDistance = gameMap.NearbyEntitiesByDistance(planet).Where(e => e.Value.GetType() == typeof(Ship) && e.Value.GetOwner() == gameMap.GetMyPlayerId()).OrderBy(kvp => kvp.Key).ToList();
                    }

                    foreach(var planetClaims in Planet.ClaimedDockingPorts){
                        var dockingPortsToUnclaim = new List<int>();
                        foreach(var dockAndShip in planetClaims.Value){
                            var ship = gameMap.GetShip(dockAndShip.Value.GetId());
                            if(ship == null){
                                dockingPortsToUnclaim.Add(dockAndShip.Key);
                                continue;
                            }
                            if(ship.GetDockingStatus() == Ship.DockingStatus.Undocked)
                                NavigateToDock(beingAttacked, gameMap.GetPlanet(planetClaims.Key), sortedPlanets, moveList, gameMap, false, ship);
                        }

                        foreach(var s in dockingPortsToUnclaim){
                            DebugLog.AddLog($"Unclaiming dock {s}");
                            planetClaims.Value.Remove(s);
                        }
                    }

                    MakeNextMove(beingAttacked, sortedPlanets, moveList, gameMap);

                    Networking.SendMoves(moveList);
                }
            }
            catch (Exception ex) {
                DebugLog.AddLog($"{ex.Message}: {ex.StackTrace}");
                Networking.SendMoves(new List<Move>());
            }
        }

        private static void ClearClaimedDockingPorts(GameMap gameMap) {
            foreach(var planetClaims in Planet.ClaimedDockingPorts){
                var dockingPortsToUnclaim = new List<int>();
                foreach(var dockAndShip in planetClaims.Value){
                    var ship = gameMap.GetShip(dockAndShip.Value.GetId());
                    if(ship == null){
                        continue;
                    }
                    if(ship.GetDockingStatus() == Ship.DockingStatus.Undocked)
                        dockingPortsToUnclaim.Add(dockAndShip.Key);
                }

                foreach(var s in dockingPortsToUnclaim){
                    DebugLog.AddLog($"Unclaiming dock {s}");
                    planetClaims.Value.Remove(s);
                }
            }
        }

        private static int PlanetComparer(Planet p1, Planet p2) => p1.ClosestUnclaimedShipDistance.CompareTo(p2.ClosestUnclaimedShipDistance);

        private static double percentToConquer = .8;
        private static void CalculateMoves (Dictionary<Planet, int> beingAttacked, List<Planet> sortedPlanets, List<Move> moveList, GameMap map){
            // Defend
            var attackedPlanet = beingAttacked.FirstOrDefault(kvp => kvp.Value > 0);
            if(attackedPlanet.Key != null){
                NavigateToDefend(beingAttacked, attackedPlanet.Key, sortedPlanets, moveList, map);
                return;
            }

            // Try to fill up our planets or capture unowned planets.
            var planetToDock = sortedPlanets.FirstOrDefault(p => (!p.IsOwned() || p.IsOwnedBy(map.GetMyPlayerId())) && !p.IsFull());
            if(planetToDock != null){
                DebugLog.AddLog($"PlanetToDock: {planetToDock.GetId()} : {planetToDock.ClosestUnclaimedShipDistance}");

                NavigateToDock(beingAttacked, planetToDock, sortedPlanets, moveList, map);
                return;
            }

            var planet = sortedPlanets.FirstOrDefault(p => p.IsOwned() && !p.IsOwnedBy(map.GetMyPlayerId()));
            if(planet != null){
                DebugLog.AddLog($"Attack: {planet.GetId()}");
                NavigateToAttack(beingAttacked, planet, sortedPlanets, moveList, map);
            }
        }

        private static void MakeNextMove(Dictionary<Planet, int> beingAttacked, List<Planet> sortedPlanets, List<Move> moveList, GameMap map){            
            sortedPlanets.Sort(PlanetComparer);
            CalculateMoves(beingAttacked, sortedPlanets, moveList, map);
        }

        private static void NavigateToDock(Dictionary<Planet, int> beingAttacked, Planet planetToDock, List<Planet> sortedPlanets, List<Move> moveList, GameMap map, bool makeNextMove = true, Ship ship = null){
            DebugLog.AddLog($"Preparing to Dock: {planetToDock.GetId()}, Open Spots: {planetToDock.GetDockingSpots() - planetToDock.GetDockedShips().Count}");
            if(ship == null)
                ship = planetToDock.ClosestUnclaimedShip;

            if(ship == null)
                return;

            var dockingSpot = planetToDock.GetClosestEmptyDockingPort(ship);

            DebugLog.AddLog($"Ship: {ship}");
            DebugLog.AddLog($"DockingPort: {dockingSpot}");

            if (ship.CanDock(dockingSpot)) {
                DebugLog.AddLog("Docking with planet");
                moveList.Add(new DockMove(ship, planetToDock));
            }
            else {
                DebugLog.AddLog("Navigating to dock");
                var counterClockWise = ShouldGoCounterClockWise(map, ship, dockingSpot);
                DebugLog.AddLog($"Going {(counterClockWise ? "counter-clockwise" : "clockwise")}");

                var move = Navigation.NavigateShipToDock(map, ship, dockingSpot, Constants.MAX_SPEED, Math.PI / 180.0 * (counterClockWise ? -1 : 1));
                if(move != null){
                    moveList.Add(move);
                }
                else{
                    moveList.Add(new ThrustMove(ship, ship.OrientTowardsInDeg(planetToDock) + 90, Constants.MAX_SPEED));
                }
            }

            planetToDock.ClaimDockingSpot(ship, dockingSpot);
               
            ship.Claim(planetToDock);
            if(makeNextMove){             
                MakeNextMove(beingAttacked, sortedPlanets, moveList, map);
            }
        }

        private static void NavigateToAttack(Dictionary<Planet, int> beingAttacked, Planet planetToAttack, List<Planet> sortedPlanets, List<Move> moveList, GameMap map, bool makeNextMove = true, Ship ship = null){
            if(ship == null)
                ship = planetToAttack.ClosestUnclaimedShip;

            if(ship == null)
                return;

            var closestShipDistance = Double.MaxValue;
            Ship closestShip = null;
            foreach (var dockedShip in planetToAttack.GetDockedShips().Select(s => map.GetShip(s)))
            {
                var shipDistance = ship.GetDistanceTo(dockedShip);
                if(shipDistance < closestShipDistance){
                    closestShipDistance = shipDistance;
                    closestShip = dockedShip;
                }
            }

            if(closestShipDistance < Constants.WEAPON_RADIUS / 2){
                moveList.Add(new Move(Move.MoveType.Noop, ship));
            }
            else {
                var goLeft = ShouldGoCounterClockWise(map, ship, closestShip);

                ThrustMove newThrustMove = Navigation.NavigateShipTowardsTarget(map, ship,
                    closestShip, Math.Min(Constants.MAX_SPEED, (int)Math.Floor(Math.Max(closestShipDistance - Constants.WEAPON_RADIUS / 2, 1))), true, Constants.MAX_NAVIGATION_CORRECTIONS,
                    Math.PI / 180.0 * (goLeft ? -1 : 1));
                if (newThrustMove != null) {
                    moveList.Add(newThrustMove);
                }
            }
                
            if(makeNextMove){
                ship.Claim(planetToAttack);
                MakeNextMove(beingAttacked, sortedPlanets, moveList, map);
            }
        }

        private static void NavigateToDefend(Dictionary<Planet, int> beingAttacked, Planet planetToDefend, List<Planet> sortedPlanets, List<Move> moveList, GameMap map, bool makeNextMove = true, Ship ship = null){
            if(ship == null)
                ship = planetToDefend.ClosestUnclaimedShip;

            if(ship == null)
                return;

            beingAttacked[planetToDefend]--;

            Ship closestShip = (Ship)planetToDefend.NearbyEnemies.FirstOrDefault().Value;
            if(closestShip == null)
                return;

            var closestShipDistance = ship.GetDistanceTo(closestShip);

            if(closestShipDistance < ship.GetRadius()){
                moveList.Add(new Move(Move.MoveType.Noop, ship));
            }
            else {
                var goLeft = ShouldGoCounterClockWise(map, ship, closestShip);

                ThrustMove newThrustMove = Navigation.NavigateShipTowardsTarget(map, ship,
                    closestShip, Math.Min(Constants.MAX_SPEED, (int)Math.Floor(Math.Max(closestShipDistance - Constants.WEAPON_RADIUS/2, 1))), true, Constants.MAX_NAVIGATION_CORRECTIONS,
                    Math.PI / 180.0 * (goLeft ? -1 : 1));
                if (newThrustMove != null) {
                    moveList.Add(newThrustMove);
                }
            }
            
            if(makeNextMove){
                ship.Claim(planetToDefend);
                MakeNextMove(beingAttacked, sortedPlanets, moveList, map);
            }
        }

        private static bool ShouldGoCounterClockWise(GameMap map, Ship ship, Position target){
            var goLeft = false;

            var obstacles = map.ObjectsBetween(ship, target);
            
            if (obstacles.Any()) {
                var obstacle = obstacles.OrderBy(o => o.GetDistanceTo(ship)).First();
                DebugLog.AddLog($"Obstacle found: {obstacle}");
                DebugLog.AddLog($"Starting point: {ship}");
                DebugLog.AddLog($"Ending point: {target}");

                var closestPointToShip = obstacle.GetClosestPoint(ship);
                var closestPointToTarget = obstacle.GetClosestPoint(target);
                        
                var directionToShip = Math.Atan2(closestPointToShip.GetYPos() - obstacle.GetYPos(), closestPointToShip.GetXPos() - obstacle.GetXPos());
                var directionToTarget = Math.Atan2(closestPointToTarget.GetYPos() - obstacle.GetYPos(), closestPointToTarget.GetXPos() - obstacle.GetXPos());

                var angle = directionToShip - directionToTarget;
                while(angle < 0){
                    angle += 2*Math.PI;
                }
                while(angle > 2 * Math.PI){
                    angle -= 2*Math.PI;
                }

                if(angle > Math.PI){
                    goLeft = true;
                }
            }

            return goLeft;
        }
    }
}
