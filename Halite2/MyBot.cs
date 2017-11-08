using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Halite2.hlt;

namespace Halite2
{
    public class MyBot
    {
        static Dictionary<int, Claim> claims;
        private static Dictionary<int, int> claimedPorts;
        private static List<Move> moveList;

        public static void Main(string[] args) {
            try {
                var name = args.Length > 0 ? args[0] : "Ze-rone";

                var networking = new Networking();
                var map = networking.Initialize(name);

                moveList = new List<Move>();
                var sortedPlanets = new List<Planet>();
                claims = new Dictionary<int, Claim>();
                claimedPorts = new Dictionary<int, int>();

                var step = 0;
                while (true) {
                    DebugLog.AddLog($"New Move: {step++}----------------------------------------------------------------------");
                    map.UpdateMap(Networking.ReadLineIntoMetadata());

                    sortedPlanets.Clear();
                    moveList.Clear();

                    var ownAnyPlanets = map.AllPlanets.Any(p => p.Value.IsOwnedBy(map.MyPlayerId));
                    
                    foreach (var planet in map.AllPlanets.Select(kvp => kvp.Value)) {
                        claimedPorts[planet.Id] = 0;
                        sortedPlanets.Add(planet);
                        var entities = map.NearbyEntitiesByDistance(planet);

                        // Don't let this count for anything if we don't own any planets at all.
                        var allPlanets = entities.Where(e => e.Key.GetType() == typeof(Planet));
                        if (ownAnyPlanets) {
                            foreach (var otherPlanet in allPlanets) {
                                if (otherPlanet.Key.IsOwnedBy(map.MyPlayerId)) {
                                    planet.AttackPoints += ((Planet)otherPlanet.Key).DockedShips.Count / Math.Pow(otherPlanet.Value, 1);
                                }
                                else
                                    planet.AttackPoints += 1 / Math.Pow((otherPlanet.Value * (otherPlanet.Key.Owner == map.MyPlayerId || !((Planet) otherPlanet.Key).IsOwned ? 1 : -1)), 1);
                            }
                        }
                        foreach (var ship in entities.Where(e => e.Key.GetType() == typeof(Ship))) {
                            var shipWeight = 1 / ship.Value * (ship.Key.Owner == map.MyPlayerId ? 1 : -1);
                            planet.AttackPoints += shipWeight;
                            //planet.ExpansionPoints += shipWeight;

                            // if (ship.Value < Constants.DOCK_RADIUS && ship.Key.Health >= Constants.DOCKING_SHIP_HEALTH) {
                            //     claims.Remove(ship.Key.Id);
                            //     claims.Add(ship.Key.Id, new Claim(planet.Id, ClaimType.Expand, new DockMove((Ship)ship.Key, planet)));
                            // }
                        }
                        planet.ShipsByDistance = map.NearbyEntitiesByDistance(planet).Where(e => e.Key.GetType() == typeof(Ship) && e.Key.Owner == map.MyPlayerId).OrderBy(kvp => kvp.Value).ToList();

                        if (planet.IsOwnedBy(map.MyPlayerId)) {
                            var dockedShips = planet.DockedShips.Select(d => map.GetShip(d));
                            var enemies = entities.Where(e => e.Key.Owner != -1 && e.Key.Owner != map.MyPlayerId && e.Key is Ship && e.Value < Constants.WEAPON_RADIUS * 3).OrderBy(e => e.Value);
                            HashSet<Entity> enemyShips = new HashSet<Entity>();
                            foreach (var ds in dockedShips) {
                                foreach (var enemy in enemies.Where(e => !enemyShips.Contains(e.Key))) {
                                    if (ds.GetDistanceTo(enemy.Key) < Constants.WEAPON_RADIUS * 1.5) {
                                        enemyShips.Add(enemy.Key);
                                    }
                                }
                                planet.FramesToLive = dockedShips.Sum(d => d.Health) / (Constants.WEAPON_DAMAGE) * enemyShips.Count;
                            }
                            planet.Attackers = enemyShips.Cast<Ship>().ToList();
                        }
                        DebugLog.AddLog($"Planet {planet.Id}\r\tExpansion: {planet.ExpansionPoints}\r\tAttack: {planet.AttackPoints}");
                    }

                    RunStatefulMoves(sortedPlanets, map, claimedPorts);

                    sortedPlanets.Sort(ExpansionComparer);
                    //Defend
                    DefenseMove(sortedPlanets, map);

                    //Counter interrupted docking is done in the stateful moves above.
                    //Expand
                    ExpansionMove(sortedPlanets, map);

                    //Attack
                    AttackMove(sortedPlanets, map);
                    AvoidCollisions();


                    Networking.SendMoves(moveList);
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

                if (ship == null || ship.DockingStatus != DockingStatus.Undocked) {
                    claimsToRemove.Add(kvp.Key);
                }
                else {
                    ship.Claim = claim.Type;

                    Move updatedMove = null;
                    switch (claim.Type) {
                        case ClaimType.Attack:
                            if (planet.IsOwned) {
                                if (planet.AttackPoints > Constants.ATTACK_THRESHOLD || planets.All(p => p.AttackPoints <= Constants.ATTACK_THRESHOLD))
                                    updatedMove = NavigateToAttack(map, planet, ship);
                            }
                            else
                                claimsToRemove.Add(ship.Id);
                            break;
                        case ClaimType.Expand:
                            if (planet.IsOwned && planet.Owner != map.MyPlayerId) {
                                if (planet.AttackPoints > Constants.ATTACK_THRESHOLD) {
                                    updatedMove = NavigateToAttack(map, planet, ship);
                                }
                            }
                            else if (ship.Health <= Constants.DOCKING_SHIP_HEALTH) {
                                updatedMove = NavigateToDefend(map, planet, ship);
                            }
                            else {
                                updatedMove = NavigateToDock(map, planet, ship);
                                ports[planet.Id]++;
                            }
                            break;
                        case ClaimType.Defend:
                            if (planet.IsOwnedBy(map.MyPlayerId) && planet.Attackers.Count > planet.Defenders / 2) {
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

        private static void AvoidCollisions() {
            foreach (Claim claim in claims.Values) {
                // if (claim.Move.Type == MoveType.Thrust && ((ThrustMove) claim.Move).Thrust > 0) {
                //    var thrustMove = (ThrustMove) claim.Move;
                //    foreach (var other in claims.Values.Where(c => c != claim)) {
                //        var changed = false;
                //        var newDistance = claim.Move.Ship.GetDistanceTo(other.Move.Ship);
                //        while (newDistance < 2 & thrustMove.Thrust > 1) {
                //            changed = true;
                //            thrustMove = new ThrustMove(thrustMove.Ship, thrustMove.Angle, thrustMove.Thrust - 1);
                //            claim.Move = thrustMove;
                //            newDistance = claim.Move.Ship.GetDistanceTo(other.Move.Ship);
                //        }
                //        if (changed) break;
                //    }
                // }
                moveList.Add(claim.Move);
            }
        }

        private static int ExpansionComparer(Planet p1, Planet p2) { return p2.ExpansionPoints.CompareTo(p1.ExpansionPoints); }
        private static int AttackComparer(Planet p1, Planet p2) { return p2.AttackPoints.CompareTo(p1.AttackPoints); }
        
        private static void AttackMove(List<Planet> sortedPlanets, GameMap map) {
            var attackPlanets = sortedPlanets.Where(p => p.IsOwned && !p.IsOwnedBy(map.MyPlayerId)).ToList();
            attackPlanets.Sort(AttackComparer);
            foreach (var planet in attackPlanets) {
                var newMove = NavigateToAttack(map, planet);
                if (newMove != null) {
                    claims[newMove.Ship.Id] = new Claim(planet.Id, ClaimType.Attack, newMove);
                    newMove.Ship.Claim = ClaimType.Attack;
                    planet.AttackPoints -= planet.DockingSpots / newMove.Ship.GetDistanceTo(planet);
                    AttackMove(sortedPlanets, map);
                    break;
                }
            }
        }

        private static void ExpansionMove(List<Planet> sortedPlanets, GameMap map) {
            var planet = sortedPlanets.FirstOrDefault(p => {
                var unclaimedPorts = p.GetAvailableDockingPorts(map.MyPlayerId);
                //DebugLog.AddLog($"Planet:{p.Id};AvailablePorts:{unclaimedPorts}");
                unclaimedPorts -= claimedPorts[p.Id];
                //DebugLog.AddLog($"\tPortsClaimedThisRound:{claimedPorts[p.Id]}");
                return unclaimedPorts > 0 && p.GetClosestUnclaimedShip(ClaimType.Expand) != null;
            });
            if (planet != null) {
                var newMove = NavigateToDock(map, planet);
                if (newMove != null) {
                    var claim = new Claim(planet.Id, ClaimType.Expand, newMove);
                    claims[newMove.Ship.Id] = claim;
                    claimedPorts[planet.Id]++;
                    newMove.Ship.Claim = ClaimType.Expand;
                    claim.Move.Ship.XPos = claim.NewPosition.XPos;
                    claim.Move.Ship.YPos = claim.NewPosition.YPos;
                    ExpansionMove(sortedPlanets, map);
                }
            }
        }

        private static void DefenseMove(List<Planet> sortedPlanets, GameMap map) {
            var planet = sortedPlanets.FirstOrDefault(p => p.Attackers.Count > p.Defenders / 2);
            if (planet != null) {
                var newMove = NavigateToDefend(map, planet);
                if (newMove != null) {
                    var claim = new Claim(planet.Id, ClaimType.Defend, newMove);
                    claims[newMove.Ship.Id] = claim;
                    //planet.AlterPoints(claim);
                    newMove.Ship.Claim = ClaimType.Defend;
                    DefenseMove(sortedPlanets, map);
                }
            }
        }

        private static Move NavigateToDock(GameMap map, Planet planet, Ship ship = null) {
            if (ship == null)
                ship = planet.GetClosestUnclaimedShip(ClaimType.Expand);

            if (ship == null)
                return null;

            DebugLog.AddLog($"Docking planet {planet.Id} with ship {ship.Id}");

            if (ship.CanDock(planet)) {
                return new DockMove(ship, planet);
            }

            return Navigation.NavigateShipToDock(map, ship, planet, Constants.MAX_SPEED);
        }

        private static Move NavigateToAttack(GameMap map, Planet planet, Ship ship = null) {
            if (ship == null)
                ship = planet.GetClosestUnclaimedShip(ClaimType.Attack);

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

            Move move = Navigation.NavigateShipTowardsTarget(map, ship, closestShip, Math.Min(Constants.MAX_SPEED,
                (int) Math.Floor(Math.Max(closestShipDistance - Constants.WEAPON_RADIUS / 2, 1))), true);

            // TODO: Don't stop, try to find a new target for this ship.
            if (move == null) {
                return null;
            }

            return move;
        }

        private static Move NavigateToDefend(GameMap map, Planet planet, Ship ship = null) {
            if (ship == null)
                ship = planet.GetClosestUnclaimedShip(ClaimType.Defend);

            if (ship == null)
                return null;

            DebugLog.AddLog($"Defending planet {planet.Id} with ship {ship.Id}");

            planet.Defenders++;
            Ship closestShip = null;
            double closestShipDistance = Double.MaxValue;
            if (planet.Attackers.Count > planet.Defenders / 2) {
                closestShip = planet.Attackers[(planet.Defenders + planet.Defenders % 2) / 2 - 1];
                closestShipDistance = closestShip.GetDistanceTo(ship);
            }
            else {
                var closestEnemy = map.NearbyEntitiesByDistance(planet).OrderBy(kvp => kvp.Value).FirstOrDefault(kvp => kvp.Key is Ship && kvp.Key.Owner != map.MyPlayerId);
                closestShip = closestEnemy.Key as Ship;

                if (closestShip == null)
                    return null;

                closestShipDistance = closestEnemy.Value;
            }

            if (closestShipDistance < Constants.WEAPON_RADIUS / 1) {
                return new Move(MoveType.Noop, ship);
            }
            else {
                return Navigation.NavigateShipTowardsTarget(map, ship,
                    closestShip, Math.Min(Constants.MAX_SPEED, (int) Math.Floor(Math.Max(closestShipDistance - Constants.WEAPON_RADIUS / 2, 1))), true);
            }
        }
    }
}