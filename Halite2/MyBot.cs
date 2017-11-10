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
        private static List<Move> moveList;

        public static void Main(string[] args) {
            try {
                var name = args.Length > 0 ? args[0] : "Ze-rone";

                var networking = new Networking();
                var gameMap = networking.Initialize(name);
                
                moveList = new List<Move>();
                var sortedPlanets = new List<Planet>();
                claims = new Dictionary<int, Claim>();
                claimedPorts = new Dictionary<int, int>();

                var step = 0;
                while(true) {
                    DebugLog.AddLog($"New Move: {step++}----------------------------------------------------------------------");
                    gameMap.UpdateMap(Networking.ReadLineIntoMetadata());

                    sortedPlanets.Clear();
                    moveList.Clear();
                    Navigation.ClearState();
                    
                    var ownAnyPlanets = gameMap.AllPlanets.Any(p => p.Value.IsOwnedBy(gameMap.MyPlayerId));

                    // TODO: Try to colonize planets that are close together first.
                    foreach (var planet in gameMap.AllPlanets.Select(kvp => kvp.Value)) {
                        claimedPorts[planet.Id] = 0;
                        sortedPlanets.Add(planet);
                        var entities = gameMap.NearbyEntitiesByDistance(planet);
                        
                        // Don't let this count for anything if we don't own any planets at all.
                        if(ownAnyPlanets){
                            foreach (var otherPlanet in entities.Where(e => e.Key.GetType() == typeof(Planet))){
                                planet.Points += 1 / (otherPlanet.Value * (otherPlanet.Key.Owner == gameMap.MyPlayerId || !((Planet)otherPlanet.Key).IsOwned ? 1 : -1));
                            }
                        }
                        foreach (var ship in entities.Where(e => e.Key.GetType() == typeof(Ship) && ((Ship) e.Key).DockingStatus == DockingStatus.Undocked))
                            planet.Points += 1 / ship.Value * (ship.Key.Owner == gameMap.MyPlayerId ? 1 : -1);
                        planet.ShipsByDistance = gameMap.NearbyEntitiesByDistance(planet).Where(e => e.Key.GetType() == typeof(Ship) && e.Key.Owner == gameMap.MyPlayerId).OrderBy(kvp => kvp.Value).ToList();

                        if(planet.IsOwnedBy(gameMap.MyPlayerId)){
                            var dockedShips = planet.DockedShips.Select(d => gameMap.GetShip(d));
                            var enemies = entities.Where(e => e.Key.Owner != -1 && e.Key.Owner != gameMap.MyPlayerId && e.Key is Ship && e.Value < Constants.WEAPON_RADIUS * 3).OrderBy(e => e.Value);
                            HashSet<Entity> enemyShips = new HashSet<Entity>();
                            foreach(var ds in dockedShips){
                                foreach(var enemy in enemies.Where(e => !enemyShips.Contains(e.Key))){
                                    if(ds.GetDistanceTo(enemy.Key) < Constants.WEAPON_RADIUS){
                                        enemyShips.Add(enemy.Key);
                                    }
                                }
                            }
                            planet.Attackers = enemyShips.Cast<Ship>().ToList();
                        }
                    }
                    
                    RunStatefulMoves(sortedPlanets, gameMap, claimedPorts);
                    CalculateMoves(sortedPlanets, gameMap);
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
                    DebugLog.AddLog($"Stateful move: {ship.Id}:{claim}:{planet.Id}");
                    ship.Claim = claim.Type;

                    Move updatedMove = null;
                    switch (claim.Type) {
                        case ClaimType.Attack:
                            if (planet.IsOwned) {
                                if (planet.Points > Constants.ATTACK_THRESHOLD || planets.All(p => p.Points <= Constants.ATTACK_THRESHOLD)) {
                                    updatedMove = NavigateToAttack(map, planet, ship);
                                }
                            }
                            break;
                        case ClaimType.Expand:
                            if (planet.IsOwned && planet.Owner != map.MyPlayerId) {
                                if (planet.Points > Constants.ATTACK_THRESHOLD)
                                {
                                    updatedMove = NavigateToAttack(map, planet, ship);
                                }
                            }
                            else if(ship.Health != Constants.MAX_SHIP_HEALTH){
                                updatedMove = NavigateToDefend(map, planet, ship);
                            }
                            else {
                                updatedMove = NavigateToDock(map, planet, ship);
                                ports[planet.Id]++;
                            }
                            break;
                        case ClaimType.Defend:
                            if(planet.IsOwnedBy(map.MyPlayerId) && planet.Attackers.Count > planet.Defenders / 2){
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

        private static void AvoidCollisions(){
            foreach(Claim claim in claims.Values){
                DebugLog.AddLog($"{claim.PlanetId},{claim.Move.Ship.Id}, {claim.Type}");
                moveList.Add(claim.Move);
            }
        }

        private static int PlanetComparer(Planet p1, Planet p2) { 
            return p2.Points.CompareTo(p1.Points); }

        private static void CalculateMoves(List<Planet> sortedPlanets, GameMap map) {
            sortedPlanets.Sort(PlanetComparer);

            //Defend
            var planet = sortedPlanets.FirstOrDefault(p => p.Attackers.Count > p.Defenders / 2);
            if(planet != null){
                var newMove = NavigateToDefend(map, planet);
                if (newMove != null) {
                    var claim = new Claim(planet.Id, ClaimType.Defend, newMove);
                    claims[newMove.Ship.Id] = claim;
                    newMove.Ship.Claim = ClaimType.Defend;
                    newMove.Ship.XPos = claim.NewPosition.XPos;
                    newMove.Ship.YPos = claim.NewPosition.YPos;
                    CalculateMoves(sortedPlanets, map);
                }
            }

            //Counter interrupted docking is done in the stateful moves above.
            //Expand
            planet = sortedPlanets.FirstOrDefault(p => {
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
                    newMove.Ship.XPos = claim.NewPosition.XPos;
                    newMove.Ship.YPos = claim.NewPosition.YPos;
                    CalculateMoves(sortedPlanets, map);
                }
            }
            //Attack
            var attackPlanets = sortedPlanets.Where(p => p.IsOwned && !p.IsOwnedBy(map.MyPlayerId)).ToList();
            foreach(var planetToAttack in attackPlanets){
                planet = planetToAttack;
                var newMove = NavigateToAttack(map, planet);
                if (newMove != null) {
                    DebugLog.AddLog("Found move, attacking");
                    var claim = new Claim(planet.Id, ClaimType.Attack, newMove);
                    claims[newMove.Ship.Id] = claim;
                    newMove.Ship.Claim = ClaimType.Attack;
                    newMove.Ship.XPos = claim.NewPosition.XPos;
                    newMove.Ship.YPos = claim.NewPosition.YPos;
                    planet.Points -= planet.DockingSpots / newMove.Ship.GetDistanceTo(planet);
                    CalculateMoves(sortedPlanets, map);
                    break;
                }
            }
        }

        private static Move NavigateToDock(GameMap map, Planet planet, Ship ship = null) {
            if(ship == null)
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

            var move = Navigation.NavigateShipTowardsTarget(map, ship, closestShip, Math.Min(Constants.MAX_SPEED, 
                (int) Math.Floor(Math.Max(closestShipDistance - Constants.WEAPON_RADIUS / 2, 1))), true);

            //if (ship.Health <= 127) {
            //    move = new ThrustMove(move.Ship, move.Angle, Constants.MAX_SPEED);
            //}

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
            if(planet.Attackers.Count > planet.Defenders / 2){
                closestShip = planet.Attackers[(planet.Defenders + planet.Defenders % 2 )/2 - 1];
                closestShipDistance = closestShip.GetDistanceTo(ship);
            }
            else{
                var closestEnemy = map.NearbyEntitiesByDistance(planet).OrderBy(kvp => kvp.Value).FirstOrDefault(kvp => kvp.Key is Ship && kvp.Key.Owner != map.MyPlayerId);
                closestShip = closestEnemy.Key as Ship;

                if(closestShip == null)
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