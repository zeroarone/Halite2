using System;
using System.Collections.Generic;
using System.Linq;
using Halite2.hlt;

namespace Halite2
{
    public class MyBot
    {
        static Dictionary<int, Claim> claims;
        private static Dictionary<int, int> claimedPorts;

        public static void Main(string[] args) {
            try {
                var name = args.Length > 0 ? args[0] : "Ze-rone";

                var networking = new Networking();
                var gameMap = networking.Initialize(name);

                var moveList = new List<Move>();
                var sortedPlanets = new List<Planet>();
                claims = new Dictionary<int, Claim>();
                claimedPorts = new Dictionary<int, int>();

                var step = 0;
                while(true) {
                    DebugLog.AddLog($"New Move: {step++}----------------------------------------------------------------------");
                    moveList.Clear();
                    gameMap.UpdateMap(Networking.ReadLineIntoMetadata());

                    sortedPlanets.Clear();
                    
                    foreach (var planet in gameMap.AllPlanets.Select(kvp => kvp.Value)) {
                        claimedPorts[planet.Id] = 0;
                        sortedPlanets.Add(planet);
                        foreach (var ship in gameMap.NearbyEntitiesByDistance(planet).Where(e => e.Key.GetType() == typeof(Ship) && ((Ship) e.Key).DockingStatus == DockingStatus.Undocked))
                            planet.Points += planet.DockingSpots/ ship.Value * (ship.Key.Owner == gameMap.MyPlayerId ? 1 : -1);
                        planet.ShipsByDistance = gameMap.NearbyEntitiesByDistance(planet).Where(e => e.Key.GetType() == typeof(Ship) && e.Key.Owner == gameMap.MyPlayerId).OrderBy(kvp => kvp.Value).ToList();
                    }

                    sortedPlanets.Sort(PlanetComparer);

                    CalculateMoves(sortedPlanets, moveList, gameMap);

                    Networking.SendMoves(moveList);
                }
            }
            catch (Exception ex) {
                DebugLog.AddLog($"{ex.Message}: {ex.StackTrace}");
                Networking.SendMoves(new List<Move>());
            }
        }

        private static int PlanetComparer(Planet p1, Planet p2) { return p2.Points.CompareTo(p1.Points); }

        private static void CalculateMoves(List<Planet> sortedPlanets, List<Move> moveList, GameMap map) {
            //Defend
            //Counter interrupted docking
            //Expand
            var planet = sortedPlanets.FirstOrDefault(p => {
                var unclaimedPorts = p.GetAvailableDockingPorts(map.MyPlayerId);
                unclaimedPorts -= claimedPorts[p.Id];
                return unclaimedPorts > 0;
            });
            if (planet != null) {
                var newMove = NavigateToDock(map, planet);
                if (newMove != null) {
                    claims[newMove.Ship.Id] = new Claim(planet.Id, ClaimType.Expand);
                    claimedPorts[planet.Id]++;
                    newMove.Ship.Claim();
                    moveList.Add(newMove);
                    CalculateMoves(sortedPlanets, moveList, map);
                }
            }
            //Attack
        }

        private static Move NavigateToDock(GameMap map, Planet planet) {
            var ship = planet.GetClosestUnclaimedShip;

            if (ship == null)
                return null;

            if (ship.CanDock(planet)) {
                return new DockMove(ship, planet);
            }
            
            var clockwise = ShouldGoClockwise(map, ship, planet);

            return Navigation.NavigateShipToDock(map, ship, planet, Constants.MAX_SPEED, clockwise);
        }

        private static void NavigateToAttack(List<Planet> unOwnedPlanets, List<Move> moveList, GameMap map) {
            foreach (var planetToAttack in unOwnedPlanets)
            foreach (var ship in planetToAttack.ShipsByDistance.Where(s => !((Ship) s.Key).Claimed).Select(s => (Ship) s.Key)) {
                if (ship == null) break;

                var closestShipDistance = Double.MaxValue;
                Ship closestShip = null;
                foreach (var dockedShip in planetToAttack.DockedShips.Select(s => map.GetShip(s))) {
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
                    var goLeft = ShouldGoClockwise(map, ship, closestShip);

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
            //if (ship == null)
            //    ship = planetToDefend.GetClosestUnclaimedShip;

            //if (ship == null)
            //    return;

            //beingAttacked[planetToDefend]--;

            //var closestShip = (Ship) planetToDefend.NearbyEnemies.FirstOrDefault().Key;
            //if (closestShip == null) {
            //    DebugLog.AddLog("ERROR: No closest ship!");
            //    return;
            //}

            //var closestShipDistance = ship.GetDistanceTo(closestShip);

            //if (closestShipDistance < ship.Radius) {
            //    moveList.Add(new Move(MoveType.Noop, ship));
            //}
            //else {
            //    var goLeft = ShouldGoClockwise(map, ship, closestShip);

            //    var newThrustMove = Navigation.NavigateShipTowardsTarget(map, ship,
            //        closestShip, Math.Min(Constants.MAX_SPEED, (int) Math.Floor(Math.Max(closestShipDistance - Constants.WEAPON_RADIUS / 2, 1))), true, Constants.MAX_NAVIGATION_CORRECTIONS,
            //        Math.PI / 180.0 * (goLeft ? -1 : 1));
            //    if (newThrustMove != null) moveList.Add(newThrustMove);
            //}

            //if (makeNextMove) {
            //    ship.Claim();
            //    CalculateMoves(ownedPlanets, moveList, map);
            //}
        }

        private static bool ShouldGoClockwise(GameMap map, Ship ship, Position target) {
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