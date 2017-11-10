using System;
using System.Collections.Generic;
using System.Linq;

namespace Halite2.hlt
{
    public class Navigation
    {
        private static Dictionary<Ship, HashSet<Entity>> shipObstaclesChecked = new Dictionary<Ship, HashSet<Entity>>();

        public static void ClearState() {
            shipObstaclesChecked.Clear();
        }

        public static ThrustMove NavigateShipToDock(GameMap gameMap, Ship ship, Entity dockTarget, int maxThrust) {
            return NavigateShipTowardsTarget(gameMap, ship, dockTarget, maxThrust, true);
        }

        public static ThrustMove NavigateShipTowardsTarget(GameMap gameMap, Ship ship, Position targetPos, int maxThrust, bool avoidObstacles) {
            var distance = ship.GetDistanceTo(targetPos);
            var angleRad = ship.OrientTowardsInRad(targetPos);

            if (avoidObstacles && gameMap.ObjectsBetween(ship, targetPos).Any()) {
                var graph = new Graph();
                shipObstaclesChecked[ship] = new HashSet<Entity>();
                BuildGraph(gameMap, ship, ship, targetPos, graph, new Dictionary<Entity, int>());
                //DebugLog.AddLog("Graph:");
                //foreach (var keyValuePair in graph.vertices) {
                //    DebugLog.AddLog($"\t({keyValuePair.Key.XPos},{keyValuePair.Key.YPos})");
                //    foreach (var valuePair in keyValuePair.Value) {
                //        DebugLog.AddLog($"\t\t({valuePair.Key.XPos},{valuePair.Key.YPos})/{valuePair.Value}");
                //    }
                //}
                //DebugLog.AddLog($"Finding shortest path from ({ship.XPos},{ship.YPos}) to ({targetPos.XPos},{targetPos.YPos})");
                try {
                    var shortestPath = graph.shortest_path(ship, targetPos);

                    shortestPath.Reverse();
                    //foreach (var position in shortestPath) {
                    //    DebugLog.AddLog($"\t({position.XPos},{position.YPos})");
                    //}
                    var newTarget = shortestPath[0];

                    distance = ship.GetDistanceTo(newTarget);
                    angleRad = ship.OrientTowardsInRad(newTarget);
                }
                catch (Exception ex) {
                    return null;
                }
            }
            else {
                distance = ship.GetDistanceTo(ship.GetClosestPoint(targetPos));
            }

            distance = (int) Math.Ceiling(distance);

            int thrust;
            if (distance < maxThrust)
                thrust = (int)distance;
            else
                thrust = maxThrust;

            var angleDeg = Util.AngleRadToDegClipped(angleRad);

            return new ThrustMove(ship, angleDeg, thrust);
        }

        private static void BuildGraph(GameMap gameMap, Ship ship, Ship startPosition, Position endPosition, Graph graph, Dictionary<Entity, int> entitiesCheckedThisRoute) {
            //DebugLog.AddLog($"Checking obstacles between (x - {startPosition.XPos})^2 + (y - {startPosition.YPos})^2 = {startPosition.Radius}^2 and (x - {endPosition.XPos})^2 +(y - {endPosition.YPos})^2 = {endPosition.Radius}^2");
            var obstacles = gameMap.ObjectsBetween(startPosition, endPosition).OrderBy(o => o.Value).ToList();
            if (obstacles.Any()) {
                var nearestObstacle = obstacles.First().Key;
                //DebugLog.AddLog($"Nearest obstacle:{nearestObstacle};");
                //DebugLog.AddLog($"Nearest obstacle:(x - {nearestObstacle.XPos})^2 + (y - {nearestObstacle.YPos})^2 = {nearestObstacle.Radius}^2");
                if (nearestObstacle == endPosition) {
                    //DebugLog.AddLog("True target found, adding final vertices.");
                    graph.add_vertex(startPosition, new Dictionary<Position, double> {{endPosition, startPosition.GetDistanceTo(endPosition)}});
                    graph.add_vertex(endPosition, new Dictionary<Position, double>());
                    return;
                }
                
                var thisroute = new Dictionary<Entity, int>(entitiesCheckedThisRoute);
                if (thisroute.ContainsKey(nearestObstacle)) {
                    thisroute[nearestObstacle]++;
                }
                else {
                    thisroute[nearestObstacle] = 1;
                }

                //DebugLog.AddLog($"Tries{thisroute.Sum(e => e.Value)}");
                if (thisroute.Sum(e => e.Value) > Constants.MAX_NAVIGATION_CORRECTIONS) {
                    // We've adjusted the route way too much along this path, stop here.
                    graph.add_vertex(startPosition, new Dictionary<Position, double>());
                    return;
                }

                var tangents = GetTangents(startPosition, endPosition, nearestObstacle).Where(t => {
                    var objectsBetweenShipAndTangent = gameMap.ObjectsBetween(startPosition, t).Any(t2 => t2.Key != nearestObstacle);
                    //if(objectsBetweenShipAndTangent)
                        //DebugLog.AddLog($"Objects between ship and tanget:(x - {t.XPos})^2 + (y - {t.YPos})^2 = {t.Radius*t.Radius}");
                    //objectsBetweenShipAndTangent = objectsBetweenShipAndTangent && startPosition.GetDistanceTo(t) > 1;
                    return !objectsBetweenShipAndTangent;
                }).ToList();
                //DebugLog.AddLog($"Adding shortest path for {startPosition}: {String.Join(",", tangents.Select(t => t.ToString()))}");
                graph.add_vertex(startPosition, tangents.ToDictionary(t => (Position)t, startPosition.GetDistanceTo));

                foreach (var tangent in tangents) {
                    BuildGraph(gameMap, ship, tangent, endPosition, graph, thisroute);
                }
            }
            else {
                //DebugLog.AddLog("No obstacles found, adding final vertices.");
                graph.add_vertex(startPosition, new Dictionary<Position, double> {{endPosition, startPosition.GetDistanceTo(endPosition)}});
                graph.add_vertex(endPosition, new Dictionary<Position, double>());
            }
        }

        private static Ship[] GetTangents(Ship ship, Position target, Entity obstacle) {
            var radii = obstacle.Radius + ship.Radius + .15;
            //DebugLog.AddLog($"Radii:{radii} Ship:{ship.Radius}, Obstacle:{obstacle.Radius}");
            var shipToObstacle = ship.GetDistanceTo(obstacle);
            //DebugLog.AddLog($"Distance between centers: {shipToObstacle}");
            var shipToTangent = Math.Sqrt(shipToObstacle*shipToObstacle - radii*radii);
            //DebugLog.AddLog($"Ship to tangent: {shipToTangent}");
            var shipAngleFromCenterToTangent = Math.Asin(radii/shipToObstacle);
            //DebugLog.AddLog($"Ship angle from center to tangent: {shipAngleFromCenterToTangent}");
            var shipAngleToObstacle = ship.OrientTowardsInRad(obstacle);

            var targetToObstacle = target.GetDistanceTo(obstacle);
            var targetToTangent = Math.Sqrt(targetToObstacle*targetToObstacle - radii*radii);
            var targetAngleFromCenterToTangent = Math.Asin(radii/targetToObstacle);
            var targetAngleToObstacle = target.OrientTowardsInRad(obstacle);

            if (double.IsNaN(shipToTangent) || double.IsNaN(targetToTangent)) {
                return new Ship[0];
            }

            var shipAngleRight = shipAngleToObstacle + shipAngleFromCenterToTangent;
            var shipAngleLeft = shipAngleToObstacle - shipAngleFromCenterToTangent;
            //DebugLog.AddLog($"Ship angles: {shipAngleLeft} , {shipAngleRight}");

            var targetAngleRight = targetAngleToObstacle - targetAngleFromCenterToTangent;
            var targetAngleLeft = targetAngleToObstacle + targetAngleFromCenterToTangent;
            
            //DebugLog.AddLog($"Ship tangents:(x - {ship.XPos + shipToTangent * Math.Cos(shipAngleRight)})^2 + (y - {ship.YPos + shipToTangent * Math.Sin(shipAngleRight)})^2 = {ship.Radius}^2," +
                            //$"(x - {ship.XPos + shipToTangent * Math.Cos(shipAngleLeft)})^2 + (y - {ship.YPos + shipToTangent * Math.Sin(shipAngleLeft)})^2 = {ship.Radius}^2");
            //DebugLog.AddLog($"Obstacle tangents:(x - {target.XPos + targetToTangent * Math.Cos(targetAngleRight)})^2 + (y - {target.YPos + targetToTangent * Math.Sin(targetAngleRight)})^2 = {ship.Radius}^2,(x - {target.XPos + targetToTangent * Math.Cos(targetAngleLeft)})^2 + (y - {target.YPos + targetToTangent * Math.Sin(targetAngleLeft)})^2 = {ship.Radius}^2");

            var x11 = ship.XPos;
            var x12 = ship.XPos + shipToTangent * Math.Cos(shipAngleRight);
            var y11 = ship.YPos;
            var y12 = ship.YPos + shipToTangent * Math.Sin(shipAngleRight);

            var A1 = y12-y11;
            var B1 = x11-x12;
            var C1 = A1*x11 + B1*y11;
            
            var x21 = target.XPos;
            var x22 = target.XPos + targetToTangent * Math.Cos(targetAngleRight);
            var y21 = target.YPos;
            var y22 = target.YPos + targetToTangent * Math.Sin(targetAngleRight);

            var A2 = y22 - y21;
            var B2 = x21 - x22;
            var C2 = A2*x21 + B2*y21;

            var det = A1*B2 - A2*B1;
            
            var x = (B2*C1 - B1*C2)/det;
            var y = (A1*C2 - A2*C1)/det;
            
            //DebugLog.AddLog($"Finding intersection of ({x11},{y11}),({x12},{y12}) and ({x21},{y21}),({x22},{y22})");
            
            var point1 = new Ship(0, ship.Id, x, y, ship.Health, ship.DockingStatus, ship.DockedPlanet, ship.DockingProgress, ship.WeaponCooldown);
            
            x11 = ship.XPos;
            x12 = ship.XPos + shipToTangent * Math.Cos(shipAngleLeft);
            y11 = ship.YPos;
            y12 = ship.YPos + shipToTangent * Math.Sin(shipAngleLeft);

            A1 = y12-y11;
            B1 = x11-x12;
            C1 = A1*x11 + B1*y11;
            
            x21 = target.XPos;
            x22 = target.XPos + targetToTangent * Math.Cos(targetAngleLeft);
            y21 = target.YPos;
            y22 = target.YPos + targetToTangent * Math.Sin(targetAngleLeft);

            A2 = y22 - y21;
            B2 = x21 - x22;
            C2 = A2*x21 + B2*y21;

            det = A1*B2 - A2*B1;
            
            x = (B2*C1 - B1*C2)/det;
            y = (A1*C2 - A2*C1)/det;

            var point2 = new Ship(0, ship.Id, x, y, ship.Health, ship.DockingStatus, ship.DockedPlanet, ship.DockingProgress, ship.WeaponCooldown);
            //DebugLog.AddLog($"Found positions to bypass:(x - {point1.XPos})^2 + (y - {point1.YPos})^2 = {ship.Radius}^2, (x - {point2.XPos})^2 + (y - {point2.YPos})^2 = {ship.Radius}^2");

            return new [] {point1, point2};
        }
    }
        
    class Graph
    {
        public Dictionary<Position, Dictionary<Position, double>> vertices = new Dictionary<Position, Dictionary<Position, double>>();

        public void add_vertex(Position name, Dictionary<Position, double> edges)
        {
            vertices[name] = edges;
        }

        public List<Position> shortest_path(Position start, Position finish)
        {
            var previous = new Dictionary<Position, Position>();
            var distances = new Dictionary<Position, double>();
            var nodes = new List<Position>();

            List<Position> path = null;

            foreach (var vertex in vertices)
            {
                if (vertex.Key == start)
                {
                    distances[vertex.Key] = 0;
                }
                else
                {
                    distances[vertex.Key] = double.MaxValue;
                }

                nodes.Add(vertex.Key);
            }

            while (nodes.Count != 0)
            {
                nodes.Sort((x, y) => distances[x].CompareTo(distances[y]));

                var smallest = nodes[0];
                nodes.Remove(smallest);

                if (smallest == finish)
                {
                    path = new List<Position>();
                    while (previous.ContainsKey(smallest))
                    {
                        path.Add(smallest);
                        smallest = previous[smallest];
                    }

                    break;
                }

                if (distances[smallest] == double.MaxValue)
                {
                    break;
                }

                foreach (var neighbor in vertices[smallest])
                {
                    var alt = distances[smallest] + neighbor.Value;
                    if (alt < distances[neighbor.Key])
                    {
                        distances[neighbor.Key] = alt;
                        previous[neighbor.Key] = smallest;
                    }
                }
            }

            return path;
        }
    }
}