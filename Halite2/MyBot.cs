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
                                planet.NearbyEnemies.AddRange(gameMap.NearbyEntitiesByDistance(ship).Where(e => e.Key.GetType() == typeof(Ship) && e.Key.GetOwner() != gameMap.MyPlayerId && e.Value < Constants.SHIP_RADIUS + 1).OrderBy(kvp => kvp.Value));                                
                            }
                            planet.NearbyEnemies.Sort((kvp1, kvp2) => kvp1.Value.CompareTo(kvp2.Key));
                            
                            if(planet.NearbyEnemies.Count > 0){
                                DebugLog.AddLog("Found attacking enemies, defending.");
                                beingAttacked.Add(planet, planet.NearbyEnemies.Count * 2);
                            }
                        }
                        planet.ShipsByDistance = gameMap.NearbyEntitiesByDistance(planet).Where(e => e.Key.GetType() == typeof(Ship) && e.Key.GetOwner() == gameMap.MyPlayerId).OrderBy(kvp => kvp.Value).ToList();
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

        private static double percentToConquer = .8;
        private static void CalculateMoves (Dictionary<Planet, int> beingAttacked, List<Planet> sortedPlanets, List<Move> moveList, GameMap map){
            // Defend
            var attackedPlanet = beingAttacked.FirstOrDefault(kvp => kvp.Value > 0);
            if(attackedPlanet.Key != null){
                NavigateToDefend(beingAttacked, attackedPlanet.Key, sortedPlanets, moveList, map);
                return;
            }

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

            if(closestShipDistance < ship.GetRadius()){
                moveList.Add(new Move(Move.MoveType.Noop, ship));
            }
            else{
                ThrustMove newThrustMove = Navigation.NavigateShipTowardsTarget(map, ship,
                    closestShip, Math.Min(Constants.MAX_SPEED, (int)Math.Floor(Math.Max(closestShipDistance - ship.GetRadius() - 1, 0))), true, Constants.MAX_NAVIGATION_CORRECTIONS,
                    Math.PI / 180.0);
                if (newThrustMove != null) {
                    moveList.Add(newThrustMove);
                }
                else {
                    moveList.Add(new ThrustMove(ship, ship.OrientTowardsInDeg(planetToAttack) + 90, Constants.MAX_SPEED));
                }
                
                if(makeNextMove){
                    ship.Claim(planetToAttack);
                    MakeNextMove(beingAttacked, sortedPlanets, moveList, map);
                }
            }
        }

        private static void NavigateToDefend(Dictionary<Planet, int> beingAttacked, Planet planetToDefend, List<Planet> sortedPlanets, List<Move> moveList, GameMap map, bool makeNextMove = true, Ship ship = null){
            if(ship == null)
                ship = planetToDefend.ClosestUnclaimedShip;

            if(ship == null)
                return;

            beingAttacked[planetToDefend]--;

            Ship closestShip = (Ship)planetToDefend.NearbyEnemies.FirstOrDefault().Key;
            if(closestShip == null)
                return;

            var closestShipDistance = ship.GetDistanceTo(closestShip);

            if(closestShipDistance < ship.GetRadius()){
                moveList.Add(new Move(Move.MoveType.Noop, ship));
            }
            else{
                ThrustMove newThrustMove = Navigation.NavigateShipTowardsTarget(map, ship,
                    closestShip, Math.Min(Constants.MAX_SPEED, (int)Math.Floor(Math.Max(closestShipDistance - ship.GetRadius() - 1, 0))), true, Constants.MAX_NAVIGATION_CORRECTIONS,
                    Math.PI / 180.0);
                if (newThrustMove != null) {
                    moveList.Add(newThrustMove);
                }
                else {
                    moveList.Add(new ThrustMove(ship, ship.OrientTowardsInDeg(planetToDefend) + 90, Constants.MAX_SPEED));
                }
            }
            
            if(makeNextMove){
                ship.Claim(planetToDefend);
                MakeNextMove(beingAttacked, sortedPlanets, moveList, map);
            }
        }
    }
}
