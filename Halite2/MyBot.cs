using System;
using Halite2.hlt;
using System.Collections.Generic;
using System.Linq;

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

                    foreach (Ship ship in gameMap.GetMyPlayer().GetShips().Values) {
                        if (ship.GetDockingStatus() != Ship.DockingStatus.Undocked) {
                            continue;
                        }

                        var moveMade = false;

                        Planet closestPlanet = null;
                        double closestPlanetDistance = Double.MaxValue;
                        foreach (Planet planet in gameMap.GetAllPlanets().Values) {
                            if (planet.IsOwned()) {
                                continue;
                            }

                            if (ship.CanDock(planet)) {
                                moveList.Add(new DockMove(ship, planet));
                                moveMade = true;
                                break;
                            }

                            var distance = planet.GetDistanceTo(ship);
                            DebugLog.AddLog($"Distance to planet: {distance}");
                            if (distance < closestPlanetDistance) {
                                closestPlanet = planet;
                                closestPlanetDistance = distance;
                            }
                        }

                        if (closestPlanet != null && !moveMade) {
                            DebugLog.AddLog($"Moving toward closest planet.");
                            ThrustMove newThrustMove =
                                Navigation.NavigateShipToDock(gameMap, ship, closestPlanet, Constants.MAX_SPEED);
                            if (newThrustMove != null) {
                                moveList.Add(newThrustMove);
                                continue;
                            }
                        }

                        if (gameMap.GetAllPlanets().Values.All(p => p.IsOwned()))
                            foreach (Planet planet in gameMap.GetAllPlanets().Values
                                .Where(p => !p.IsOwnedBy(gameMap.GetMyPlayer().GetId()))) {
                                ThrustMove newThrustMove = Navigation.NavigateShipTowardsTarget(gameMap, ship,
                                    planet, Constants.MAX_SPEED, false, Constants.MAX_NAVIGATION_CORRECTIONS,
                                    Math.PI / 180.0);
                                if (newThrustMove != null) {
                                    moveList.Add(newThrustMove);
                                    break;
                                }
                            }
                    }

                    DebugLog.AddLog(String.Join(",", moveList.Select(m => $"{m.GetShip().GetId()}:{m.GetMoveType()}")));

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
