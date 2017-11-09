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
                DebugLog.AddLog("Graph:");
                foreach (var keyValuePair in graph.vertices) {
                    DebugLog.AddLog($"\t({keyValuePair.Key.XPos},{keyValuePair.Key.YPos})");
                    foreach (var valuePair in keyValuePair.Value) {
                        DebugLog.AddLog($"\t\t({valuePair.Key.XPos},{valuePair.Key.YPos})/{valuePair.Value}");
                    }
                }
                DebugLog.AddLog($"Finding shortest path from ({ship.XPos},{ship.YPos}) to ({targetPos.XPos},{targetPos.YPos})");
                try {
                    var shortestPath = graph.shortest_path(ship, targetPos);

                    shortestPath.Reverse();
                    foreach (var position in shortestPath) {
                        DebugLog.AddLog($"\t({position.XPos},{position.YPos})");
                    }
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
            DebugLog.AddLog($"Checking obstacles between ({startPosition.XPos},{startPosition.YPos}) and ({endPosition.XPos},{endPosition.YPos})");
            var obstacles = gameMap.ObjectsBetween(startPosition, endPosition).OrderBy(o => o.Value).ToList();
            if (obstacles.Any()) {
                var nearestObstacle = obstacles.First().Key;
                DebugLog.AddLog($"Nearest obstacle:{nearestObstacle};");
                if (nearestObstacle == endPosition) {
                    DebugLog.AddLog("True target found, adding final vertices.");
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

                DebugLog.AddLog($"Tries{thisroute.Sum(e => e.Value)}");
                if (thisroute.Sum(e => e.Value) > Constants.MAX_NAVIGATION_CORRECTIONS) {
                    // We've adjusted the route way too much along this path, stop here.
                    graph.add_vertex(startPosition, new Dictionary<Position, double>());
                    return;
                }

                var tangents = GetTangents(startPosition, endPosition, nearestObstacle).Where(t => {
                    var objectsBetweenShipAndTangent = gameMap.ObjectsBetween(startPosition, t).Any(t2 => t2.Key != nearestObstacle);
                    if(objectsBetweenShipAndTangent)
                        DebugLog.AddLog($"Objects between ship and tanget:(x - {t.XPos})^2 + (y - {t.YPos})^2 = {t.Radius*t.Radius}");
                    return !objectsBetweenShipAndTangent;
                }).ToList();
                DebugLog.AddLog($"Adding shortest path for {startPosition}: {String.Join(",", tangents.Select(t => t.ToString()))}");
                graph.add_vertex(startPosition, tangents.ToDictionary(t => (Position)t, startPosition.GetDistanceTo));

                foreach (var tangent in tangents) {
                    BuildGraph(gameMap, ship, tangent, endPosition, graph, thisroute);
                }
            }
            else {
                DebugLog.AddLog("No obstacles found, adding final vertices.");
                graph.add_vertex(startPosition, new Dictionary<Position, double> {{endPosition, startPosition.GetDistanceTo(endPosition)}});
                graph.add_vertex(endPosition, new Dictionary<Position, double>());
            }
        }

        private static Ship[] GetTangents(Ship ship, Position target, Entity obstacle) {
            var radii = obstacle.Radius + Constants.FORECAST_FUDGE_FACTOR + .1;
            //DebugLog.AddLog($"Radii:{radii} Ship:{ship.Radius}, Obstacle:{obstacle.Radius}");
            var shipToObstacle = ship.GetDistanceTo(obstacle);
            //DebugLog.AddLog($"Distance between centers: {distanceBetweenCenters}");
            var shipToTangent = Math.Sqrt(radii*radii + shipToObstacle*shipToObstacle);
            DebugLog.AddLog($"Ship to tangent: {shipToTangent}");
            var shipAngleToTangent = Math.Asin(radii/shipToTangent);
            var shipAngleToObstacle = ship.OrientTowardsInRad(obstacle);

            var targetToObstacle = target.GetDistanceTo(obstacle);
            var targetToTangent = Math.Sqrt(radii*radii + targetToObstacle*targetToObstacle);
            var targetAngleToTangent = Math.Asin(radii/targetToTangent);
            var targetAngleToObstacle = target.OrientTowardsInRad(obstacle);

            var shipAngleRight = shipAngleToObstacle + shipAngleToTangent;
            var shipAngleLeft = shipAngleToObstacle - shipAngleToTangent;
            var targetAngleRight = targetAngleToObstacle - targetAngleToTangent;
            var targetAngleLeft = targetAngleToObstacle + targetAngleToTangent;
            
            DebugLog.AddLog($"Ship tangents:({ship.XPos + shipToTangent * Math.Cos(shipAngleRight)}," +$"{ship.YPos + shipToTangent * Math.Sin(shipAngleRight)})," +
                            $"({ship.XPos + shipToTangent * Math.Cos(shipAngleLeft)},{ship.YPos + shipToTangent * Math.Sin(shipAngleLeft)})");
            DebugLog.AddLog($"Obstacle tangents:({target.XPos + targetToTangent * Math.Cos(targetAngleRight)},{target.YPos + targetToTangent * Math.Sin(targetAngleRight)}),({target.XPos + targetToTangent * Math.Cos(targetAngleLeft)},{target.YPos + targetToTangent * Math.Sin(targetAngleLeft)})");

            var shipToTarget = ship.GetDistanceTo(target);
            //DebugLog.AddLog($"shiptotarget: {shipToTarget}, shipAngletoTangent:{shipAngleToTangent*(180/Math.PI)}, targetAngleToTangent: {targetAngleToTangent*(180/Math.PI)}");
            var distanceRight = shipToTarget / Math.Sin(Math.PI - shipAngleToTangent - targetAngleToTangent) * Math.Sin(targetAngleToTangent);
            //var distanceLeft = shipToTarget / (Math.PI - shipAngleLeft - targetAngleRight) * Math.Sin(targetAngleRight);

            DebugLog.AddLog($"Distance right: {distanceRight}");
            //DebugLog.AddLog($"Distance left: {distanceLeft}");
            var x = ship.XPos + distanceRight * Math.Cos(shipAngleRight);
            var y = ship.YPos + distanceRight * Math.Sin(shipAngleRight);

            //DebugLog.AddLog($"Old Ship tangents:({ship.XPos + shipToTangent * Math.Cos(shipAngleToTangent + shipAngleToObstacle)},{ship.YPos + shipToTangent * Math.Sin(shipAngleToTangent + shipAngleToObstacle)}),({ship.XPos + shipToTangent * Math.Cos(shipAngleToObstacle - shipAngleToTangent)},{ship.YPos + shipToTangent * Math.Sin(shipAngleToObstacle - shipAngleToTangent)})");
            
            var point1 = new Ship(0, ship.Id, x, y, ship.Health, ship.DockingStatus, ship.DockedPlanet, ship.DockingProgress, ship.WeaponCooldown);
            
            x = ship.XPos + distanceRight * Math.Cos(shipAngleLeft);
            y = ship.YPos + distanceRight * Math.Sin(shipAngleLeft);

            var point2 = new Ship(0, ship.Id, x, y, ship.Health, ship.DockingStatus, ship.DockedPlanet, ship.DockingProgress, ship.WeaponCooldown);
            DebugLog.AddLog($"Found positions to bypass:({point1.XPos},{point1.YPos}),({point2.XPos},{point2.YPos})");

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