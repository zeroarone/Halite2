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
                    foreach(Ship ship in gameMap.GetAllShips().Where(s => s.GetDockingStatus() == Ship.DockingStatus.Docking)){
                        DebugLog.AddLog($"Docked planet: {ship.GetDockedPlanet()}");
                        gameMap.GetPlanet(ship.GetDockedPlanet()).ClaimDockingSpot(ship.GetId());
                    }
                    ownedPlanets.Sort(PlanetComparer);
                    unOwnedPlanets.Sort(PlanetComparer);

                    CalculateMoves(ownedPlanets, unOwnedPlanets, moveList, gameMap);

                    // foreach (Ship ship in gameMap.GetMyPlayer().GetShips().Values) {
                    //     if (ship.GetDockingStatus() != Ship.DockingStatus.Undocked) {
                    //         continue;
                    //     }

                    //     var moveMade = false;

                    //     Planet closestPlanet = null;
                    //     double closestPlanetDistance = Double.MaxValue;

                    //     foreach (Planet planet in gameMap.GetAllPlanets().Values) {
                    //         // Fill up the planet first.
                    //         if (planet.IsOwned() && !planet.IsOwnedBy(gameMap.GetMyPlayerId())) {
                    //             continue;
                    //         }
                    //         // Favor conquering a single planet over spreading out.
                    //         if (ship.CanDock(planet) && !planet.IsFull()) {
                    //                 planet.ClaimDockingSpot(ship.GetId());
                    //                 moveList.Add(new DockMove(ship, planet));
                    //                 moveMade = true;
                    //                 break;
                    //         }

                    //         // Don't try to go toward an already full planet.
                    //         if(planet.IsFull()){
                    //             continue;
                    //         }

                    //         var distance = planet.GetDistanceTo(ship);
                    //         if (distance < closestPlanetDistance) {
                    //             closestPlanet = planet;
                    //             closestPlanetDistance = distance;
                    //         }
                    //     }

                    //     if(moveMade)
                    //         continue;

                    //     DebugLog.AddLog($"{ship}{closestPlanet}");

                    //     if (closestPlanet != null) {
                    //         ThrustMove newThrustMove =
                    //             Navigation.NavigateShipToDock(gameMap, ship, closestPlanet, Constants.MAX_SPEED);
                    //         if (newThrustMove != null) {
                    //             DebugLog.AddLog("Thrusting toward closestPlanet");
                    //             // Claim the docking spot, so we don't send more ships than needed toward an empty planet.
                    //             closestPlanet.ClaimDockingSpot(ship.GetId());
                    //             moveList.Add(newThrustMove);
                    //             continue;
                    //         }
                    //     }

                    //     // There are no unowned planets, or we've already claimed them all, just attack the other player's planets.
                    //     var allOwned = gameMap.GetAllPlanets().Values.All(p => p.IsOwned());
                    //     var allClaimed = gameMap.GetAllPlanets().Where(p => p.Value.IsOwnedBy(gameMap.GetMyPlayerId())).All(p => p.Value.IsFull());
                    //     if (allOwned || allClaimed) {
                    //         DebugLog.AddLog($"{allOwned}:{allClaimed}");
                    //         closestPlanetDistance = Double.MaxValue;
                    //         closestPlanet = null;
                    //         foreach (Planet planet in gameMap.GetAllPlanets().Values.Where(p => p.IsOwned() && !p.IsOwnedBy(gameMap.GetMyPlayer().GetId())))
                    //         {
                    //             var distance = planet.GetDistanceTo(ship);
                    //             if (distance < closestPlanetDistance)
                    //             {
                    //                 closestPlanet = planet;
                    //                 closestPlanetDistance = distance;
                    //             }
                    //         }
                    //         if (closestPlanet != null) {
                    //             var closestShipDistance = Double.MaxValue;
                    //             Ship closestShip = null;
                    //             foreach (var dockedShip in closestPlanet.GetDockedShips().Select(s => gameMap.GetShip(s)))
                    //             {
                    //                 var shipDistance = ship.GetDistanceTo(dockedShip);
                    //                 if(shipDistance < closestShipDistance){
                    //                     closestShipDistance = shipDistance;
                    //                     closestShip = dockedShip;
                    //                 }
                    //             }

                    //             if(closestShipDistance < ship.GetRadius()){
                    //                 moveList.Add(new Move(Move.MoveType.Noop, ship));
                    //                 continue;
                    //             }

                    //             DebugLog.AddLog($"{closestShipDistance}:{ship.GetRadius()}:{Math.Min(Constants.MAX_SPEED, (int)Math.Floor(closestShipDistance - ship.GetRadius()))}");
                    //             DebugLog.AddLog(closestShip.ToString());
                    //             ThrustMove newThrustMove = Navigation.NavigateShipTowardsTarget(gameMap, ship,
                    //                 closestShip, Math.Min(Constants.MAX_SPEED, (int)Math.Floor(Math.Max(closestShipDistance - ship.GetRadius() - 1, 1))), true, Constants.MAX_NAVIGATION_CORRECTIONS,
                    //                 Math.PI / 180.0);
                    //             if (newThrustMove != null) {
                    //                 moveList.Add(newThrustMove);
                    //             }
                    //         }
                    //     }
                    // }

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

        private static void NavigateToDock(Planet planetToDock, List<Planet> ownedPlanets, List<Planet> unOwnedPlanets, List<Move> moveList, GameMap map, bool makeNextMove = true){
            DebugLog.AddLog($"Preparing to Dock: {planetToDock.GetId()}, Open Spots: {planetToDock.GetDockingSpots() - planetToDock.GetDockedShips().Count}");
            var ship = planetToDock.ClosestUnclaimedShip;
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
            ship.Claim();

            if(makeNextMove)
                MakeNextMove(ownedPlanets, unOwnedPlanets, moveList, map);
        }

        private static void NavigateToAttack(Planet planetToAttack, List<Planet> ownedPlanets, List<Planet> unOwnedPlanets, List<Move> moveList, GameMap map){
            var ship = planetToAttack.ClosestUnclaimedShip;
            if(ship == null)
                return;

            ship.Claim();
                
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
                closestShip, Math.Min(Constants.MAX_SPEED, (int)Math.Floor(Math.Max(closestShipDistance - ship.GetRadius() - 1, 1))), true, Constants.MAX_NAVIGATION_CORRECTIONS,
                Math.PI / 180.0);
            if (newThrustMove != null) {
                moveList.Add(newThrustMove);
            }
            
            MakeNextMove(ownedPlanets, unOwnedPlanets, moveList, map);
        }
    }
}
