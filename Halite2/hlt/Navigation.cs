using System;
using System.Collections.Generic;
using System.Linq;

namespace Halite2.hlt
{
    public class Navigation
    {
        public static ThrustMove NavigateShipToDock(GameMap gameMap, Ship ship, Entity dockTarget, int maxThrust) {
            var targetPos = ship.GetClosestPoint(dockTarget);

            return NavigateShipTowardsTarget(gameMap, ship, targetPos, maxThrust, true);
        }

        public static ThrustMove NavigateShipTowardsTarget(
            GameMap gameMap,
            Ship ship,
            Position targetPos,
            int maxThrust,
            bool avoidObstacles) {
            var distance = ship.GetDistanceTo(targetPos);
            var angleRad = ship.OrientTowardsInRad(targetPos);

            // TODO: Avoid overlapping ships, don't slam into the second one.
            List<KeyValuePair<Entity, double>> objectsInTheWay;
            if (avoidObstacles) {
                ThrustMove newMove = null;
                bool firstTry = true;
                do{
                    objectsInTheWay = gameMap.ObjectsBetween(ship, targetPos).OrderBy(e => e.Value).ToList();
                    if(objectsInTheWay.Any()) {
                        var originalTarget = targetPos;

                        DebugLog.AddLog($"Objects in the way: {objectsInTheWay.Count()}");
                        DebugLog.AddLog($"Objects in the way: {objectsInTheWay.First().Key}");
                        newMove = GetBestPosition(ship, targetPos, objectsInTheWay.First().Key, maxThrust, newMove?.ClockWise);
                        var x = distance * Math.Cos(newMove.Angle * Math.PI/180);
                        var y = distance * Math.Sin(newMove.Angle * Math.PI/180);
                        var testTarget = new Position(ship.XPos + x, ship.YPos + y);
                        if (gameMap.ObjectsBetween(ship, testTarget).Any() && firstTry) {
                            DebugLog.AddLog($"Something else in the way of route.");
                            var opposite = GetBestPosition(ship, originalTarget, objectsInTheWay.First().Key, maxThrust, !newMove?.ClockWise);
                            if (!gameMap.ObjectsBetween(ship, targetPos).Any()) {
                                newMove = opposite;
                            }
                        }
                        x = newMove.Thrust * Math.Cos(newMove.Angle * Math.PI/180);
                        y = newMove.Thrust * Math.Sin(newMove.Angle * Math.PI/180);
                        targetPos = new Position(ship.XPos + x, ship.YPos + y);
                        firstTry = false;
                    }
                }while(objectsInTheWay.Any());
                if(newMove != null)
                    return newMove;
            }

            int thrust;
            if (distance < maxThrust)
                thrust = (int) distance;
            else
                thrust = maxThrust;

            var angleDeg = Util.AngleRadToDegClipped(angleRad);

            return new ThrustMove(ship, angleDeg, thrust);
        }        

        private static ThrustMove GetBestPosition(Ship ship, Position target, Entity obstacle, int maxThrust, bool? previousClockwise = null) {
            var clockwise = previousClockwise ?? true;
            if(!previousClockwise.HasValue){
                var shipToPivot = ship.GetDistanceTo(obstacle);
                var shipToTarget = ship.GetDistanceTo(target);
                var targetToPivot = target.GetDistanceTo(obstacle);

                var B = Math.Acos((shipToTarget * shipToTarget + shipToPivot * shipToPivot - targetToPivot * targetToPivot) / (2 * shipToTarget * shipToPivot));
                DebugLog.AddLog($"B: {B}");

                var A1 = Math.Asin(shipToPivot * Math.Sin(B) / obstacle.Radius);
                var A2 = Math.PI - A1;

                var closePoint = new Position(obstacle.Radius * Math.Cos(A1), obstacle.Radius * Math.Sin(A1));
                var farPoint = new Position(obstacle.Radius * Math.Cos(A2), obstacle.Radius * Math.Sin(A2));

                var directionToShip = Math.Atan2(closePoint.YPos - obstacle.YPos, closePoint.XPos - obstacle.XPos);
                var directionToTarget = Math.Atan2(farPoint.YPos - obstacle.YPos, farPoint.XPos - obstacle.XPos);

                var angle = directionToShip - directionToTarget;
                while (angle < 0) angle += 2 * Math.PI;
                while (angle > 2 * Math.PI) angle -= 2 * Math.PI;
                if (angle >= Math.PI) clockwise = false;
            }

            var radii = obstacle.Radius + Constants.FORECAST_FUDGE_FACTOR;
            var distanceBetweenCenters = ship.GetDistanceTo(obstacle);
            var distanceToBestPoint = Math.Sqrt(radii*radii + distanceBetweenCenters*distanceBetweenCenters);

            var angleToBestPoint = Math.Asin(radii/distanceToBestPoint);
            var angleToTarget = ship.OrientTowardsInRad(target);
            if(clockwise){
                angleToBestPoint += angleToTarget;
            }
            else{
                angleToBestPoint = angleToTarget - angleToBestPoint;
            }

            var distanceToTarget = ship.GetDistanceTo(target);
            int thrust = distanceToTarget > maxThrust ? maxThrust : (int)distanceToTarget;
            DebugLog.AddLog($"Thrust: {thrust}, Clockwise: {clockwise}");

            return new ThrustMove(ship, Util.AngleRadToDegClipped(angleToBestPoint), thrust, clockwise);
        }
    }
}