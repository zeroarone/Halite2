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
                
                List<Move> moveList = new List<Move>();
                int step = 0;
                for (;;) {
                    DebugLog.AddLog($"New Move: {step++}----------------------------------------------------------------------");
                    moveList.Clear();
                    gameMap.UpdateMap(Networking.ReadLineIntoMetadata());

                    List<Planet> sortedPlanets = new List<Planet>();
                    Dictionary<Planet, int> beingAttacked = new Dictionary<Planet, int>();

                    foreach(Planet planet in gameMap.GetAllPlanets().Select(kvp => kvp.Value)){
                        sortedPlanets.Add(planet);
                        if(planet.IsOwnedBy(gameMap.MyPlayerId)){
                            var dockedShips = planet.GetDockedShips().Select(s => gameMap.GetShip(s));
                            planet.NearbyEnemies = new List<KeyValuePair<Entity, double>>();
                            foreach(var ship in dockedShips){
                                planet.NearbyEnemies.AddRange(gameMap.NearbyEntitiesByDistance(ship).Where(e => e.Key.GetType() == typeof(Ship) && e.Key.GetOwner() != gameMap.MyPlayerId && e.Value <= Constants.WEAPON_RADIUS).OrderBy(kvp => kvp.Value));                                
                            }
                            planet.NearbyEnemies.Sort((kvp1, kvp2) => kvp1.Value.CompareTo(kvp2.Value));
                            
                            if(planet.NearbyEnemies.Count > 0){
                                DebugLog.AddLog("Found attacking enemies, defending.");
                                beingAttacked.Add(planet, planet.NearbyEnemies.Count * 2);
                            }
                        }
                        else{
                            Planet.ClaimedDockingPorts[planet.GetId()].Clear();
                        }
                        planet.ShipsByDistance = gameMap.NearbyEntitiesByDistance(planet).Where(e => e.Key.GetType() == typeof(Ship) && e.Key.GetOwner() == gameMap.MyPlayerId).OrderBy(kvp => kvp.Value).ToList();
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
                            planetClaims.Value.Remove(s);
                        }
                    }

                    sortedPlanets.Sort(PlanetComparer);

                    CalculateMoves(beingAttacked, sortedPlanets, moveList, gameMap);

                    Networking.SendMoves(moveList);
                }
            }
            catch (Exception ex) {
                DebugLog.AddLog($"{ex.Message}: {ex.StackTrace}");
                Networking.SendMoves(new List<Move>());
            }
        }

        private static int PlanetComparer(Planet p1, Planet p2) => p1.ClosestUnclaimedShipDistance.CompareTo(p2.ClosestUnclaimedShipDistance);

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

            DebugLog.AddLog($"Docking with ship: {ship}");
            var dockingSpot = planetToDock.GetClosestEmptyDockingPort(ship);
            DebugLog.AddLog($"Docking spot: {dockingSpot}");

            if(ship.CanDock(dockingSpot)){
                DebugLog.AddLog("Docking with planet");
                moveList.Add(new DockMove(ship, planetToDock));
            }
            else{
                DebugLog.AddLog($"Navigating to dock {dockingSpot}");
                var goLeft = ShouldGoLeft(map, planetToDock, ship, dockingSpot);
                var move = Navigation.NavigateShipToDock(map, ship, dockingSpot, Constants.MAX_SPEED, Math.PI / 180.0 * (goLeft ? -1 : 1));
                if(move != null){
                    moveList.Add(move);
                }
                else{
                    moveList.Add(new ThrustMove(ship, ship.OrientTowardsInDeg(planetToDock) + 90, Constants.MAX_SPEED));
                }
            }

            planetToDock.ClaimDockingSpot(ship, dockingSpot);
       
            ship.Claim();
            if(makeNextMove){         
                MakeNextMove(beingAttacked, sortedPlanets, moveList, map);
            }
        }

        private static bool ShouldGoLeft(GameMap map, Planet planetToGoAround, Ship ship, Position positionToReach){
            var goLeft = false;
            if(map.ObjectsBetween(ship, positionToReach).Contains(planetToGoAround)){
                var closestPoint = planetToGoAround.GetClosestPoint(ship);
                        
                var directionClosest = Math.Atan2(closestPoint.GetYPos() - planetToGoAround.GetYPos(), closestPoint.GetXPos() - planetToGoAround.GetXPos());
                var directionDocking = Math.Atan2(positionToReach.GetYPos() - planetToGoAround.GetYPos(), positionToReach.GetXPos() - planetToGoAround.GetXPos());

                var angle = directionClosest - directionDocking;
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

        private static void NavigateToAttack(Dictionary<Planet, int> beingAttacked, Planet planetToAttack, List<Planet> sortedPlanets, List<Move> moveList, GameMap map) {
            var ship = planetToAttack.ClosestUnclaimedShip;

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

            if (closestShipDistance < Constants.WEAPON_RADIUS - 1) {
                moveList.Add(new Move(Move.MoveType.Noop, ship));
            }
            else {
               var goLeft = ShouldGoLeft(map, planetToAttack, ship, closestShip);

                ThrustMove newThrustMove = Navigation.NavigateShipTowardsTarget(map, ship,
                    closestShip,
                    Math.Min(Constants.MAX_SPEED,
                        (int) Math.Floor(Math.Max(closestShipDistance - Constants.WEAPON_RADIUS - 1, 1))), true,
                    Constants.MAX_NAVIGATION_CORRECTIONS, Math.PI / 180.0 * (goLeft ? -1 : 1));
                if (newThrustMove != null) {
                    moveList.Add(newThrustMove);
                }
                else {
                    moveList.Add(
                        new ThrustMove(ship, ship.OrientTowardsInDeg(planetToAttack) + 90, Constants.MAX_SPEED));
                }
            }

            ship.Claim();
            MakeNextMove(beingAttacked, sortedPlanets, moveList, map);
        }

        private static void NavigateToDefend(Dictionary<Planet, int> beingAttacked, Planet planetToDefend, List<Planet> sortedPlanets, List<Move> moveList, GameMap map) {
            var ship = planetToDefend.ClosestUnclaimedShip;

            if (ship == null)
                return;

            if (planetToDefend.DefendingShip == null) {
                DebugLog.AddLog(
                    $"Defending {planetToDefend.GetId()} with {ship.GetId()} at distance {ship.GetDistanceTo(ship.GetClosestPoint(planetToDefend))}");
                planetToDefend.DefendingShip = ship;
            }

            if (beingAttacked.ContainsKey(planetToDefend))
                beingAttacked[planetToDefend]--;

            DebugLog.AddLog($"{(planetToDefend.NearbyEnemies?.Any() ?? false)}");
            if (!(planetToDefend.NearbyEnemies?.Any() ?? false)) {
                DebugLog.AddLog("No nearby enemies.");
                if (ship.GetDistanceTo(ship.GetClosestPoint(planetToDefend)) > Constants.DEFEND_DISTANCE) {
                    DebugLog.AddLog("Moving toward planet to defend.");
                    ThrustMove newThrustMove = Navigation.NavigateShipTowardsTarget(map, ship, planetToDefend,
                        Constants.MAX_SPEED, true, Constants.MAX_NAVIGATION_CORRECTIONS, Math.PI / 180.0);
                    if (newThrustMove != null) {
                        DebugLog.AddLog("Actually moving.");
                        moveList.Add(newThrustMove);
                    }
                }
            }
            else {
                DebugLog.AddLog($"Closest ship to defend from: {planetToDefend.NearbyEnemies.FirstOrDefault().Key}");
                Ship closestShip = (Ship) planetToDefend.NearbyEnemies.FirstOrDefault().Key;

                var closestShipDistance = ship.GetDistanceTo(closestShip);

                if (closestShipDistance < Constants.WEAPON_RADIUS) {
                    moveList.Add(new Move(Move.MoveType.Noop, ship));
                }
                else {
                    ThrustMove newThrustMove = Navigation.NavigateShipTowardsTarget(map, ship,
                        closestShip,
                        Math.Min(Constants.MAX_SPEED,
                            (int) Math.Floor(Math.Max(closestShipDistance - Constants.WEAPON_RADIUS - 1, 1))), true,
                        Constants.MAX_NAVIGATION_CORRECTIONS,
                        Math.PI / 180.0);
                    if (newThrustMove != null) {
                        moveList.Add(newThrustMove);
                    }
                    else {
                        moveList.Add(new ThrustMove(ship, ship.OrientTowardsInDeg(planetToDefend) + 90,
                            Constants.MAX_SPEED));
                    }
                }
            }

            ship.Claim();
            MakeNextMove(beingAttacked, sortedPlanets, moveList, map);
        }

        private static void CalculateMoves (Dictionary<Planet, int> beingAttacked, List<Planet> sortedPlanets, List<Move> moveList, GameMap map){
            // Defend
            // var attackedPlanet = beingAttacked.FirstOrDefault(kvp => kvp.Value > 0);
            // if(attackedPlanet.Key != null){
            //     NavigateToDefend(beingAttacked, attackedPlanet.Key, sortedPlanets, moveList, map);
            //     return;
            // }

            // if (sortedPlanets.All(p => p.IsOwned())) {
            //     var planetToDefend = sortedPlanets.FirstOrDefault(p => p.IsOwnedBy(map.MyPlayerId) && (p.DefendingShip == null || map.GetShip(p.DefendingShip.GetId()) == null));
            //     if (planetToDefend != null) {
            //         NavigateToDefend(beingAttacked, planetToDefend, sortedPlanets, moveList, map);
            //         return;
            //     }
            // }

            // Try to fill up our planets or capture unowned planets.
            var planetToDock = sortedPlanets.FirstOrDefault(p => (!p.IsOwned() || p.IsOwnedBy(map.MyPlayerId)) && !p.IsFull());
            if(planetToDock != null){
                DebugLog.AddLog($"PlanetToDock: {planetToDock.GetId()} : {planetToDock.ClosestUnclaimedShipDistance}");

                NavigateToDock(beingAttacked, planetToDock, sortedPlanets, moveList, map);
                return;
            }

            var planet = sortedPlanets.FirstOrDefault(p => p.IsOwned() && !p.IsOwnedBy(map.MyPlayerId));
            if(planet != null){
                DebugLog.AddLog($"Attack: {planet.GetId()}");
                NavigateToAttack(beingAttacked, planet, sortedPlanets, moveList, map);
            }
        }
    }
}
