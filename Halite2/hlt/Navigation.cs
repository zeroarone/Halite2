using System;
using System.Linq;

namespace Halite2.hlt
{
    public class Navigation
    {
        public static ThrustMove NavigateShipToDock(GameMap gameMap, Ship ship, Entity dockTarget, int maxThrust, bool goLeft) {
            var maxCorrections = Constants.MAX_NAVIGATION_CORRECTIONS;
            var avoidObstacles = true;
            var angularStepRad = Math.PI / 180.0 * (goLeft ? -1 : 1);
            var targetPos = ship.GetClosestPoint(dockTarget);

            return NavigateShipTowardsTarget(gameMap, ship, targetPos, maxThrust, avoidObstacles, maxCorrections, angularStepRad);
        }

        public static ThrustMove NavigateShipTowardsTarget(
            GameMap gameMap,
            Ship ship,
            Position targetPos,
            int maxThrust,
            bool avoidObstacles,
            int maxCorrections,
            double angularStepRad) {
            if (maxCorrections <= 0)
                return null;

            var distance = ship.GetDistanceTo(targetPos);
            var angleRad = ship.OrientTowardsInRad(targetPos);

            if (avoidObstacles && gameMap.ObjectsBetween(ship, targetPos).Any()) {
                var newTargetDx = Math.Cos(angleRad + angularStepRad) * distance;
                var newTargetDy = Math.Sin(angleRad + angularStepRad) * distance;
                var newTarget = new Position(ship.XPos+ newTargetDx, ship.YPos+ newTargetDy);

                return NavigateShipTowardsTarget(gameMap, ship, newTarget, maxThrust, true, maxCorrections - 1, angularStepRad);
            }

            int thrust;
            if (distance < maxThrust)
                thrust = (int) distance;
            else
                thrust = maxThrust;

            var angleDeg = Util.AngleRadToDegClipped(angleRad);

            return new ThrustMove(ship, angleDeg, thrust);
        }

        public static ThrustMove CrashIntoOpposingPlanets(
            GameMap gameMap,
            Ship ship,
            Position targetPos,
            int maxThrust,
            int maxCorrections,
            double angularStepRad) {
            if (maxCorrections <= 0)
                return null;

            var distance = ship.GetDistanceTo(targetPos);
            var angleRad = ship.OrientTowardsInRad(targetPos);

            if (gameMap.ObjectsBetween(ship, targetPos).Any(o => o.Owner == ship.Owner)) {
                var newTargetDx = Math.Cos(angleRad + angularStepRad) * distance;
                var newTargetDy = Math.Sin(angleRad + angularStepRad) * distance;
                var newTarget = new Position(ship.XPos+ newTargetDx, ship.YPos+ newTargetDy);

                return CrashIntoOpposingPlanets(gameMap, ship, newTarget, maxThrust, maxCorrections - 1, angularStepRad);
            }

            int thrust;
            if (distance < maxThrust)
                thrust = (int) distance;
            else
                thrust = maxThrust;

            var angleDeg = Util.AngleRadToDegClipped(angleRad);

            return new ThrustMove(ship, angleDeg, thrust);
        }
    }
}