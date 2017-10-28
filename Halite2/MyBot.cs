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

                    foreach (Ship ship in gameMap.GetMyPlayer().GetShips().Values) {
                        if (ship.GetDockingStatus() != Ship.DockingStatus.Undocked) {
                            continue;
                        }

                        var moveMade = false;

                        Planet closestPlanet = null;
                        double closestPlanetDistance = Double.MaxValue;

                        foreach (Planet planet in gameMap.GetAllPlanets().Values) {
                            // Fill up the planet first.
                            if (planet.IsOwned() && !planet.IsOwnedBy(gameMap.GetMyPlayerId())) {
                                continue;
                            }
                            // Favor conquering a single planet over spreading out.
                            if (ship.CanDock(planet) && !planet.IsFull()) {
                                    planet.ClaimDockingSpot(ship.GetId());
                                    moveList.Add(new DockMove(ship, planet));
                                    moveMade = true;
                                    break;
                            }

                            // Don't try to go toward an already full planet.
                            if(planet.IsFull()){
                                continue;
                            }

                            var distance = planet.GetDistanceTo(ship);
                            if (distance < closestPlanetDistance) {
                                closestPlanet = planet;
                                closestPlanetDistance = distance;
                            }
                        }

                        if(moveMade)
                            continue;

                        DebugLog.AddLog($"{ship}{closestPlanet}");

                        if (closestPlanet != null) {
                            ThrustMove newThrustMove =
                                Navigation.NavigateShipToDock(gameMap, ship, closestPlanet, Constants.MAX_SPEED);
                            if (newThrustMove != null) {
                                DebugLog.AddLog("Thrusting toward closestPlanet");
                                // Claim the docking spot, so we don't send more ships than needed toward an empty planet.
                                closestPlanet.ClaimDockingSpot(ship.GetId());
                                moveList.Add(newThrustMove);
                                continue;
                            }
                        }

                        // There are no unowned planets, or we've already claimed them all, just attack the other player's planets.
                        if (gameMap.GetAllPlanets().Values.All(p => p.IsOwned())) {
                            closestPlanetDistance = Double.MaxValue;
                            closestPlanet = null;
                            foreach (Planet planet in gameMap.GetAllPlanets().Values.Where(p => !p.IsOwnedBy(gameMap.GetMyPlayer().GetId())))
                            {
                                var distance = planet.GetDistanceTo(ship);
                                if (distance < closestPlanetDistance)
                                {
                                    closestPlanet = planet;
                                    closestPlanetDistance = distance;
                                }
                            }
                            if (closestPlanet != null) {
                                var closestShipDistance = Double.MaxValue;
                                Ship closestShip = null;
                                foreach (var dockedShip in closestPlanet.GetDockedShips().Select(s => gameMap.GetShip(s)))
                                {
                                    var shipDistance = ship.GetDistanceTo(dockedShip);
                                    if(shipDistance < closestShipDistance){
                                        closestShipDistance = shipDistance;
                                        closestShip = dockedShip;
                                    }
                                }

                                if(closestShipDistance < ship.GetRadius()){
                                    moveList.Add(new Move(Move.MoveType.Noop, ship));
                                    continue;
                                }

                                DebugLog.AddLog($"{closestShipDistance}:{ship.GetRadius()}:{Math.Min(Constants.MAX_SPEED, (int)Math.Floor(closestShipDistance - ship.GetRadius()))}");
                                DebugLog.AddLog(closestShip.ToString());
                                ThrustMove newThrustMove = Navigation.NavigateShipTowardsTarget(gameMap, ship,
                                    closestShip, Math.Min(Constants.MAX_SPEED, (int)Math.Floor(Math.Max(closestShipDistance - ship.GetRadius() - 1, 1))), true, Constants.MAX_NAVIGATION_CORRECTIONS,
                                    Math.PI / 180.0);
                                if (newThrustMove != null) {
                                    moveList.Add(newThrustMove);
                                }
                            }
                        }
                    }

                    Networking.SendMoves(moveList);
                }
            }
            catch (Exception ex) {
                DebugLog.AddLog(ex.StackTrace);
                Networking.SendMoves(new List<Move>());
            }
        }
    }
}
