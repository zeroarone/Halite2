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
                for (;;) {
                    DebugLog.AddLog("New Move ----------------------------------------------------------------------");
                    moveList.Clear();
                    gameMap.UpdateMap(Networking.ReadLineIntoMetadata());

                    var emptyPlanets = gameMap.GetAllPlanets().Values.Any(p => !p.IsOwned());

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
                            // Favor spreading out over conquering a single planet until all planets are taken.
                            if (ship.CanDock(planet) && !planet.IsFull()) {
                                    moveList.Add(new DockMove(ship, planet));
                                    moveMade = true;
                                    break;
                            }

                            // Now find closest unowned planet.
                            if(planet.IsOwned())
                                continue;

                            var distance = planet.GetDistanceTo(ship);
                            if (distance < closestPlanetDistance) {
                                closestPlanet = planet;
                                closestPlanetDistance = distance;
                            }
                        }

                        if(moveMade)
                            continue;

                        if (closestPlanet != null) {
                            ThrustMove newThrustMove =
                                Navigation.NavigateShipToDock(gameMap, ship, closestPlanet, Constants.MAX_SPEED);
                            if (newThrustMove != null) {
                                moveList.Add(newThrustMove);
                                continue;
                            }
                        }

                        // There are no unowned planet, just attack the other player's planets.
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
                                ThrustMove newThrustMove = Navigation.NavigateShipTowardsTarget(gameMap, ship,
                                    closestShip, Math.Min(Constants.MAX_SPEED, (int)Math.Floor(Math.Max(closestShipDistance - ship.GetRadius() - 1, 0))), true, Constants.MAX_NAVIGATION_CORRECTIONS,
                                    Math.PI / 180.0);
                                if (newThrustMove != null) {
                                    moveList.Add(newThrustMove);
                                }
                            }
                        }
                    }

                    //DebugLog.AddLog(String.Join(",", moveList.Select(m => $"{m.GetShip().GetId()}:{m.GetMoveType()}")));

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
