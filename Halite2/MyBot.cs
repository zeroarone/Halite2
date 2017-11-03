using System;
using System.Collections.Generic;
using System.Linq;
using Halite2.hlt;

namespace Halite2
{
    public class MyBot
    {
        private static double percentToConquer = .8;

        public static void Main(string[] args) {
            try {
                var name = args.Length > 0 ? args[0] : "Ze-rone";

                var networking = new Networking();
                var gameMap = networking.Initialize(name);

                var moveList = new List<Move>();
                var step = 1;
                for (;;) {
                    DebugLog.AddLog($"New Move: {step++}----------------------------------------------------------------------");
                    moveList.Clear();
                    gameMap.UpdateMap(Networking.ReadLineIntoMetadata());

                    var ownedPlanets = new List<Planet>();
                    var unOwnedPlanets = new List<Planet>();
                    var beingAttacked = new Dictionary<Planet, int>();

                    foreach (var planet in gameMap.AllPlanets.Select(kvp => kvp.Value)) {
                        foreach (var ship in gameMap.NearbyEntitiesByDistance(planet).Where(e => e.Key.GetType() == typeof(Ship) && ((Ship) e.Key).DockingStatus == DockingStatus.Undocked)) planet.Points += planet.GetDockingSpots() / ship.Value * (ship.Key.Owner == gameMap.MyPlayerId ? 1 : -1);
                        if (planet.IsOwnedBy(gameMap.MyPlayerId)) {
                            ownedPlanets.Add(planet);
                            var dockedShips = planet.GetDockedShips().Select(s => gameMap.GetShip(s));
                            planet.NearbyEnemies = new List<KeyValuePair<Entity, double>>();
                            foreach (var ship in dockedShips) planet.NearbyEnemies.AddRange(gameMap.NearbyEntitiesByDistance(ship).Where(e => e.Key.GetType() == typeof(Ship) && e.Key.Owner != gameMap.MyPlayerId && e.Value < Constants.WEAPON_RADIUS + 1).OrderBy(kvp => kvp.Value));
                            planet.NearbyEnemies.Sort((kvp1, kvp2) => kvp1.Value.CompareTo(kvp2.Value));

                            if (planet.NearbyEnemies.Count > 0) beingAttacked.Add(planet, planet.NearbyEnemies.Count * 2);
                        }
                        else {
                            unOwnedPlanets.Add(planet);
                        }
                        planet.ShipsByDistance = gameMap.NearbyEntitiesByDistance(planet).Where(e => e.Key.GetType() == typeof(Ship) && e.Key.Owner == gameMap.MyPlayerId).OrderBy(kvp => kvp.Value).ToList();
                    }

                    ownedPlanets.Sort(PlanetComparer);
                    unOwnedPlanets.Sort(PlanetComparer);

                    CalculateMoves(beingAttacked, ownedPlanets, unOwnedPlanets, moveList, gameMap);

                    Networking.SendMoves(moveList);
                }
            }
            catch (Exception ex) {
                DebugLog.AddLog($"{ex.Message}: {ex.StackTrace}");
                Networking.SendMoves(new List<Move>());
            }
        }

        private static int PlanetComparer(Planet p1, Planet p2) { return p2.Points.CompareTo(p1.Points); }

        private static void CalculateMoves(Dictionary<Planet, int> beingAttacked, List<Planet> ownedPlanets, List<Planet> unOwnedPlanets, List<Move> moveList, GameMap map) {
            // Defend
            var attackedPlanet = beingAttacked.FirstOrDefault(kvp => kvp.Value > 0);
            if (attackedPlanet.Key != null) {
                NavigateToDefend(beingAttacked, attackedPlanet.Key, ownedPlanets, unOwnedPlanets, moveList, map);
                return;
            }

            // Fill up already owned planets first.
            var unfilledPlanet = ownedPlanets.FirstOrDefault(p => !p.IsFull());
            if (unfilledPlanet != null) {
                DebugLog.AddLog($"Unfilled: {unfilledPlanet.Id}");
                NavigateToDock(beingAttacked, unfilledPlanet, ownedPlanets, unOwnedPlanets, moveList, map);
                return;
            }

            // Try to capture unowned planets next.
            var emptyPlanet = unOwnedPlanets.FirstOrDefault(p => !p.IsOwned() && !p.IsFull());
            if (emptyPlanet != null) {
                DebugLog.AddLog($"Empty: {emptyPlanet.Id}");
                NavigateToDock(beingAttacked, emptyPlanet, ownedPlanets, unOwnedPlanets, moveList, map);
                return;
            }
            NavigateToAttack(unOwnedPlanets, moveList, map);
        }

        private static void NavigateToDock(Dictionary<Planet, int> beingAttacked, Planet planetToDock, List<Planet> ownedPlanets, List<Planet> unOwnedPlanets, List<Move> moveList, GameMap map, bool makeNextMove = true, Ship ship = null) {
            DebugLog.AddLog($"Preparing to Dock: {planetToDock.Id}, Open Spots: {planetToDock.GetDockingSpots() - planetToDock.GetDockedShips().Count}");
            if (ship == null)
                ship = planetToDock.ClosestUnclaimedShip;

            if (ship == null)
                return;

            if (ship.CanDock(planetToDock)) {
                DebugLog.AddLog("Docking with planet");
                moveList.Add(new DockMove(ship, planetToDock));
            }
            else {
                DebugLog.AddLog("Navigating to dock");

                var goLeft = ShouldGoLeft(map, ship, planetToDock);

                var move = Navigation.NavigateShipToDock(map, ship, planetToDock, Constants.MAX_SPEED, true);
                if (move != null) moveList.Add(move);
            }

            planetToDock.ClaimDockingSpot(ship.Id);

            if (makeNextMove) {
                ship.Claim();
                CalculateMoves(beingAttacked, ownedPlanets, unOwnedPlanets, moveList, map);
            }
        }

        private static void NavigateToAttack(List<Planet> unOwnedPlanets, List<Move> moveList, GameMap map) {
            foreach (var planetToAttack in unOwnedPlanets)
            foreach (var ship in planetToAttack.ShipsByDistance.Where(s => !((Ship) s.Key).Claimed).Select(s => (Ship) s.Key)) {
                if (ship == null) break;

                var closestShipDistance = Double.MaxValue;
                Ship closestShip = null;
                foreach (var dockedShip in planetToAttack.GetDockedShips().Select(s => map.GetShip(s))) {
                    var shipDistance = ship.GetDistanceTo(dockedShip);
                    if (shipDistance < closestShipDistance) {
                        closestShipDistance = shipDistance;
                        closestShip = dockedShip;
                    }
                }

                if (closestShipDistance < Constants.WEAPON_RADIUS / 2) {
                    moveList.Add(new Move(MoveType.Noop, ship));
                }
                else {
                    var goLeft = ShouldGoLeft(map, ship, closestShip);

                    var newThrustMove = Navigation.NavigateShipTowardsTarget(map, ship,
                        closestShip, Math.Min(Constants.MAX_SPEED, (int) Math.Floor(Math.Max(closestShipDistance - Constants.WEAPON_RADIUS / 2, 1))), true, Constants.MAX_NAVIGATION_CORRECTIONS,
                        Math.PI / 180.0 * (goLeft ? -1 : 1));
                    if (newThrustMove != null) {
                        ship.Claim();
                        moveList.Add(newThrustMove);
                    }
                }
            }
        }

        private static void NavigateToDefend(Dictionary<Planet, int> beingAttacked, Planet planetToDefend, List<Planet> ownedPlanets, List<Planet> unOwnedPlanets, List<Move> moveList, GameMap map, bool makeNextMove = true, Ship ship = null) {
            if (ship == null)
                ship = planetToDefend.ClosestUnclaimedShip;

            if (ship == null)
                return;

            beingAttacked[planetToDefend]--;

            var closestShip = (Ship) planetToDefend.NearbyEnemies.FirstOrDefault().Key;
            if (closestShip == null) {
                DebugLog.AddLog("ERROR: No closest ship!");
                return;
            }

            var closestShipDistance = ship.GetDistanceTo(closestShip);

            if (closestShipDistance < ship.Radius) {
                moveList.Add(new Move(MoveType.Noop, ship));
            }
            else {
                var goLeft = ShouldGoLeft(map, ship, closestShip);

                var newThrustMove = Navigation.NavigateShipTowardsTarget(map, ship,
                    closestShip, Math.Min(Constants.MAX_SPEED, (int) Math.Floor(Math.Max(closestShipDistance - Constants.WEAPON_RADIUS / 2, 1))), true, Constants.MAX_NAVIGATION_CORRECTIONS,
                    Math.PI / 180.0 * (goLeft ? -1 : 1));
                if (newThrustMove != null) moveList.Add(newThrustMove);
            }

            if (makeNextMove) {
                ship.Claim();
                CalculateMoves(beingAttacked, ownedPlanets, unOwnedPlanets, moveList, map);
            }
        }

        private static bool ShouldGoLeft(GameMap map, Ship ship, Position target) {
            var goLeft = false;

            var obstacles = map.ObjectsBetween(ship, target);

            if (obstacles.Any()) {
                var pivot = obstacles.OrderBy(o => o.GetDistanceTo(ship)).First();
                var shipToPivot = ship.GetDistanceTo(pivot);
                var shipToTarget = ship.GetDistanceTo(target);
                var targetToPivot = target.GetDistanceTo(pivot);

                var B = Math.Acos(shipToTarget * shipToTarget + shipToPivot * shipToPivot - targetToPivot * targetToPivot) / (2 * shipToTarget * shipToPivot);

                var A1 = Math.Asin(shipToPivot * Math.Sin(B) / pivot.Radius);
                var A2 = Math.PI - A1;

                var closePoint = new Position(pivot.Radius * Math.Cos(A1), pivot.Radius * Math.Sin(A1));
                var farPoint = new Position(pivot.Radius * Math.Cos(A2), pivot.Radius * Math.Sin(A2));

                var directionToShip = Math.Atan2(closePoint.YPos - pivot.YPos, closePoint.XPos - pivot.XPos);
                var directionToTarget = Math.Atan2(farPoint.YPos - pivot.YPos, farPoint.XPos - pivot.XPos);

                var angle = directionToShip - directionToTarget;
                while (angle < 0) angle += 2 * Math.PI;
                while (angle > 2 * Math.PI) angle -= 2 * Math.PI;

                if (angle > Math.PI) goLeft = true;
            }

            return goLeft;
        }
    }
}