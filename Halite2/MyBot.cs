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
                int step = 1;
                for (;;) {
                    DebugLog.AddLog($"New Move: {step++}----------------------------------------------------------------------");
                    moveList.Clear();
                    gameMap.UpdateMap(Networking.ReadLineIntoMetadata());

                    List<Planet> ownedPlanets = new List<Planet>();
                    List<Planet> unOwnedPlanets = new List<Planet>();

                    foreach(Planet planet in gameMap.GetAllPlanets().Select(kvp => kvp.Value)){
                        if(planet.IsOwnedBy(gameMap.GetMyPlayerId())){
                            ownedPlanets.Add(planet);
                        }
                        else{
                            unOwnedPlanets.Add(planet);
                        }
                        planet.ShipsByDistance = gameMap.NearbyEntitiesByDistance(planet).Where(e => e.Value.GetType() == typeof(Ship) && e.Value.GetOwner() == gameMap.GetMyPlayerId()).OrderBy(kvp => kvp.Key).ToList();
                    }

                    // To prevent wasted movements from ships, if a ship starts toward a planet it will continue to that planet to complete it's mission.
                    // If the planet is taken over then the bots are no longer "claimed" by it and they can redirect, this happens when we're attacking and in the
                    // constructor for the planet.
                    foreach(var kvp in Planet.ShipsClaimed){
                        var toRemove = new List<Ship>();
                        foreach(var ship in kvp.Value){
                            // Make sure the ship is still around.
                            var realShip = gameMap.GetAllShips().FirstOrDefault(s => s.GetId() == ship.GetId());
                            if(realShip == null){
                                toRemove.Add(ship);
                            }
                            else{
                                // Don't try to redock docking ships.
                                if(realShip.GetDockingStatus() != Ship.DockingStatus.Undocked)
                                    continue;
                                var planet = gameMap.GetPlanet(kvp.Key);
                                // We own this planet, or it is not owned, so we must have been flying to it to dock, continue doing so.
                                if(!planet.IsOwned() || planet.IsOwnedBy(gameMap.GetMyPlayerId())){
                                    NavigateToDock(planet, null, null, moveList, gameMap, false, realShip);
                                }
                                else{
                                    NavigateToAttack(planet, null, null, moveList, gameMap, false, realShip);
                                }
                                realShip.ClaimStateless();
                            }
                        }
                        foreach(var ship in toRemove){
                            kvp.Value.Remove(ship);
                        }
                    }
                    ownedPlanets.Sort(PlanetComparer);
                    unOwnedPlanets.Sort(PlanetComparer);

                    CalculateMoves(ownedPlanets, unOwnedPlanets, moveList, gameMap);

                    Networking.SendMoves(moveList);
                }
            }
            catch (Exception ex) {
                DebugLog.AddLog($"{ex.Message}: {ex.StackTrace}");
                Networking.SendMoves(new List<Move>());
            }
        }

        private static int PlanetComparer(Planet p1, Planet p2) => p1.ClosestUnclaimedShipDistance.CompareTo(p2.ClosestUnclaimedShipDistance);

        private static bool attackPhase = true;
        private static double percentToConquer = .8;
        private static void CalculateMoves (List<Planet> ownedPlanets, List<Planet> unOwnedPlanets, List<Move> moveList, GameMap map){
            if(unOwnedPlanets.All(p => p.IsOwned())){                
                attackPhase = true;
            }

            // Fill up already owned planets first.
            var unfilledPlanet = ownedPlanets.FirstOrDefault(p => attackPhase && !p.IsFull()/* || !attackPhase && p.GetDockedShips().Count < 1/*Math.Ceiling(p.GetDockingSpots() * percentToConquer)*/);
            if(unfilledPlanet != null){
                DebugLog.AddLog($"Unfilled: {unfilledPlanet.GetId()}");
                NavigateToDock(unfilledPlanet, ownedPlanets, unOwnedPlanets, moveList, map);
                return;
            }

            // Try to capture unowned planets next.
            var emptyPlanet = unOwnedPlanets.FirstOrDefault(p => attackPhase && !p.IsOwned() && !p.IsFull()/* || !attackPhase && !p.IsOwned() && p.GetDockedShips().Count < 1/*Math.Ceiling(p.GetDockingSpots() * percentToConquer)*/);
            if(emptyPlanet != null){
                DebugLog.AddLog($"Empty: {emptyPlanet.GetId()}");
                NavigateToDock(emptyPlanet, ownedPlanets, unOwnedPlanets, moveList, map);
                return;
            }

            var planet = unOwnedPlanets.FirstOrDefault();
            if(planet != null){
                DebugLog.AddLog($"Attack: {planet.GetId()}");
                NavigateToAttack(planet, ownedPlanets, unOwnedPlanets, moveList, map);
            }
        }

        private static void MakeNextMove(List<Planet> ownedPlanets, List<Planet> unOwnedPlanets, List<Move> moveList, GameMap map){            
            ownedPlanets.Sort(PlanetComparer);
            unOwnedPlanets.Sort(PlanetComparer);
            CalculateMoves(ownedPlanets, unOwnedPlanets, moveList, map);
        }

        private static void NavigateToDock(Planet planetToDock, List<Planet> ownedPlanets, List<Planet> unOwnedPlanets, List<Move> moveList, GameMap map, bool makeNextMove = true, Ship ship = null){
            DebugLog.AddLog($"Preparing to Dock: {planetToDock.GetId()}, Open Spots: {planetToDock.GetDockingSpots() - planetToDock.GetDockedShips().Count}");
            if(ship == null)
                ship = planetToDock.ClosestUnclaimedShip;

            if(ship == null)
                return;

            if(ship.CanDock(planetToDock)){
                DebugLog.AddLog("Docking with planet");
                moveList.Add(new DockMove(ship, planetToDock));
            }
            else{
                DebugLog.AddLog("Navigating to dock");
                var move = Navigation.NavigateShipToDock(map, ship, planetToDock, Constants.MAX_SPEED);
                if(move != null){
                    moveList.Add(move);
                }
            }

            planetToDock.ClaimDockingSpot(ship.GetId());

            if(makeNextMove){                
                ship.Claim(planetToDock);
                MakeNextMove(ownedPlanets, unOwnedPlanets, moveList, map);
            }
        }

        private static void NavigateToAttack(Planet planetToAttack, List<Planet> ownedPlanets, List<Planet> unOwnedPlanets, List<Move> moveList, GameMap map, bool makeNextMove = true, Ship ship = null){
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

            if(closestShipDistance < ship.GetRadius()){
                moveList.Add(new Move(Move.MoveType.Noop, ship));
            }

            ThrustMove newThrustMove = Navigation.NavigateShipTowardsTarget(map, ship,
                closestShip, Math.Min(Constants.MAX_SPEED, (int)Math.Floor(Math.Max(closestShipDistance - ship.GetRadius() - 1, 0))), true, Constants.MAX_NAVIGATION_CORRECTIONS,
                Math.PI / 180.0);
            if (newThrustMove != null) {
                moveList.Add(newThrustMove);
            }
            
            if(makeNextMove){
                ship.Claim(planetToAttack);
                MakeNextMove(ownedPlanets, unOwnedPlanets, moveList, map);
            }
        }
    }
}
