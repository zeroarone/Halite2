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
                
                var sortedPlanets = new List<Planet>();
                claims = new Dictionary<int, Claim>();
                claimedPorts = new Dictionary<int, int>();

                var step = 0;
                while(true) {
                    DebugLog.AddLog($"New Move: {step++}----------------------------------------------------------------------");
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

                    RunStatefulMoves(sortedPlanets, gameMap, claimedPorts);
                    CalculateMoves(sortedPlanets, gameMap);

                    Networking.SendMoves(claims.Values.Select(v => v.Move));
                }
            }
            catch (Exception ex) {
                DebugLog.AddLog($"{ex.Message}: {ex.StackTrace}");
                Networking.SendMoves(new List<Move>());
            }
        }

        private static void RunStatefulMoves(List<Planet> planets, GameMap map, Dictionary<int, int> ports) {
            // Handle statefulness here
            List<int> claimsToRemove = new List<int>();
            foreach (var kvp in claims) {
                var ship = map.GetShip(kvp.Key);
                var claim = kvp.Value;
                var planet = map.GetPlanet(claim.PlanetId);

                if (ship == null) {
                    claimsToRemove.Add(kvp.Key);
                }
                else {
                    ship.Claim = claim.Type;

                    Move updatedMove = null;
                    switch (claim.Type) {
                        case ClaimType.Attack:
                            if (planet.IsOwned) {
                                if(planet.Points > Constants.ATTACK_THRESHOLD || planets.All(p => p.Points <= Constants.ATTACK_THRESHOLD))
                                    updatedMove = NavigateToAttack(map, planet, ship);
                            }
                            else
                                claimsToRemove.Add(ship.Id);
                            break;
                        case ClaimType.Expand:
                            if (planet.IsOwned && planet.Owner != map.MyPlayerId) {
                                if (planet.Points > Constants.ATTACK_THRESHOLD)
                                {
                                    updatedMove = NavigateToAttack(map, planet, ship);
                                }
                            }
                            else {
                                updatedMove = NavigateToDock(map, planet, ship);
                                ports[planet.Id]++;
                            }
                            break;
                        case ClaimType.Defend:
                            if(planet.IsOwnedBy(map.MyPlayerId)){
                                updatedMove = NavigateToDefend(map, planet, ship);
                            }
                            break;
                    }

                    if (updatedMove != null) {
                        claim.Move = updatedMove;
                    }
                    else {
                        claimsToRemove.Add(ship.Id);
                    }
                }
            }
            foreach (var shipId in claimsToRemove) {
                claims.Remove(shipId);
                var ship = map.GetShip(shipId);
                if (ship != null) {
                    ship.Claim = ClaimType.None;
                }
            }
        }

        private static int PlanetComparer(Planet p1, Planet p2) { return p2.Points.CompareTo(p1.Points); }

        private static void CalculateMoves(List<Planet> sortedPlanets, GameMap map) {
            //Defend
            var planet = sortedPlanets.FirstOrDefault(p => p.IsOwnedBy(map.MyPlayerId) && p.Points <=0);
            if(planet != null){
                var newMove = NavigateToDefend(map, planet);
                if (newMove != null) {
                    DebugLog.AddLog("Move found, defending.");
                    var claim = new Claim(planet.Id, ClaimType.Defend, newMove);
                    claims[newMove.Ship.Id] = claim;
                    planet.AlterPoints(claim);
                    newMove.Ship.Claim = ClaimType.Defend;
                    CalculateMoves(sortedPlanets, map);
                }
            }

            //Counter interrupted docking is done in the stateful moves above.
            //Expand
            planet = sortedPlanets.FirstOrDefault(p => {
                var unclaimedPorts = p.GetAvailableDockingPorts(map.MyPlayerId);
                unclaimedPorts -= claimedPorts[p.Id];
                return unclaimedPorts > 0 && p.GetClosestUnclaimedShip(ClaimType.Expand) != null;
            });
            if (planet != null) {
                var newMove = NavigateToDock(map, planet);
                if (newMove != null) {
                    DebugLog.AddLog("Move found, expanding.");
                    claims[newMove.Ship.Id] = new Claim(planet.Id, ClaimType.Expand, newMove);
                    claimedPorts[planet.Id]++;
                    newMove.Ship.Claim = ClaimType.Expand;
                    CalculateMoves(sortedPlanets, map);
                }
            }
            //Attack
            var attackPlanets = sortedPlanets.Where(p => p.IsOwned && !p.IsOwnedBy(map.MyPlayerId));
            planet = attackPlanets.FirstOrDefault(p => p.Points > Constants.ATTACK_THRESHOLD);
            if (planet == null)
                planet = attackPlanets.FirstOrDefault();

            if (planet != null) {
                var newMove = NavigateToAttack(map, planet);
                if (newMove != null) {
                    DebugLog.AddLog("Move found, attacking.");
                    claims[newMove.Ship.Id] = new Claim(planet.Id, ClaimType.Attack, newMove);
                    newMove.Ship.Claim = ClaimType.Attack;
                    CalculateMoves(sortedPlanets, map);
                }
            }
        }

        private static Move NavigateToDock(GameMap map, Planet planet, Ship ship = null) {
            if(ship == null)
                ship = planet.GetClosestUnclaimedShip(ClaimType.Expand);
            else {
                DebugLog.AddLog($"Docking with already docking ship.");
            }
            
            if (ship == null)
                return null;
            DebugLog.AddLog($"Docking planet {planet.Id} with ship {ship.Id}");

            if (ship.CanDock(planet)) {
                return new DockMove(ship, planet);
            }
            
            var clockwise = ShouldGoClockwise(map, ship, planet);

            return Navigation.NavigateShipToDock(map, ship, planet, Constants.MAX_SPEED, clockwise);
        }

        private static Move NavigateToAttack(GameMap map, Planet planet, Ship ship = null) {
            if (ship == null)
                ship = planet.GetClosestUnclaimedShip(ClaimType.Attack);
            else {
                DebugLog.AddLog($"Attacking with already attacking ship: {ship.Id}");
            }

            if (ship == null)
                return null;
            DebugLog.AddLog($"Attacking planet {planet.Id} with ship {ship.Id}");

            var closestShipDistance = Double.MaxValue;
            Ship closestShip = null;
            foreach (var dockedShip in planet.DockedShips.Select(s => map.GetShip(s))) {
                var shipDistance = ship.GetDistanceTo(dockedShip);
                if (shipDistance < closestShipDistance) {
                    closestShipDistance = shipDistance;
                    closestShip = dockedShip;
                }
            }

            if (closestShipDistance < Constants.WEAPON_RADIUS / 2) {
                return new Move(MoveType.Noop, ship);
            }
            var clockwise = ShouldGoClockwise(map, ship, closestShip);

            Move move = Navigation.NavigateShipTowardsTarget(map, ship, closestShip, Math.Min(Constants.MAX_SPEED, 
                (int) Math.Floor(Math.Max(closestShipDistance - Constants.WEAPON_RADIUS / 2, 1))), true, Constants.MAX_NAVIGATION_CORRECTIONS, Math.PI / 180.0 * (clockwise ? -1 : 1));

            if (move == null) {
                DebugLog.AddLog("NOOP, Trying to attack.");
                move = new Move(MoveType.Noop, ship);
            }

            return move;
        }

        private static Move NavigateToDefend(GameMap map, Planet planet, Ship ship = null) {
            if (ship == null)
               ship = planet.GetClosestUnclaimedShip(ClaimType.Defend);

            if (ship == null)
               return null;

            var closestShip = (Ship) map.NearbyEntitiesByDistance(planet).OrderBy(kvp => kvp.Value).First(kvp => !kvp.Key.IsOwnedBy(map.MyPlayerId) && kvp.Key is Ship).Key;
            var closestShipDistance = ship.GetDistanceTo(closestShip);

            if (closestShipDistance < Constants.WEAPON_RADIUS / 1) {
               return new Move(MoveType.Noop, ship);
            }
            else {
               var clockwise = ShouldGoClockwise(map, ship, closestShip);

               return Navigation.NavigateShipTowardsTarget(map, ship,
                   closestShip, Math.Min(Constants.MAX_SPEED, (int) Math.Floor(Math.Max(closestShipDistance - Constants.WEAPON_RADIUS / 2, 1))), true, Constants.MAX_NAVIGATION_CORRECTIONS,
                   Math.PI / 180.0 * (clockwise ? -1 : 1));               
            }
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