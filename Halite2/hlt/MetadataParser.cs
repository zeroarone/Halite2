using System.Collections.Generic;

namespace Halite2.hlt
{
    public class MetadataParser
    {
        public static void PopulateShipList(List<Ship> shipsOutput, int owner, Metadata shipsMetadata) {
            var numberOfShips = long.Parse(shipsMetadata.Pop());

            for (var i = 0; i < numberOfShips; ++i) shipsOutput.Add(NewShipFromMetadata(owner, shipsMetadata));
        }

        private static Ship NewShipFromMetadata(int owner, Metadata metadata) {
            var id = int.Parse(metadata.Pop());
            var xPos = double.Parse(metadata.Pop());
            var yPos = double.Parse(metadata.Pop());
            var health = int.Parse(metadata.Pop());

            // Ignoring velocity(x,y) which is always (0,0) in current version.
            metadata.Pop();
            metadata.Pop();

            var dockingStatus = (DockingStatus) int.Parse(metadata.Pop());
            var dockedPlanet = int.Parse(metadata.Pop());
            var dockingProgress = int.Parse(metadata.Pop());
            var weaponCooldown = int.Parse(metadata.Pop());

            return new Ship(owner, id, xPos, yPos, health, dockingStatus, dockedPlanet, dockingProgress, weaponCooldown);
        }

        public static Planet NewPlanetFromMetadata(List<int> dockedShips, Metadata metadata) {
            var id = int.Parse(metadata.Pop());
            var xPos = double.Parse(metadata.Pop());
            var yPos = double.Parse(metadata.Pop());
            var health = int.Parse(metadata.Pop());

            var radius = double.Parse(metadata.Pop());
            var dockingSpots = int.Parse(metadata.Pop());
            var currentProduction = int.Parse(metadata.Pop());
            var remainingProduction = int.Parse(metadata.Pop());

            var hasOwner = int.Parse(metadata.Pop());
            var ownerCandidate = int.Parse(metadata.Pop());
            int owner;
            if (hasOwner == 1) owner = ownerCandidate;
            else owner = -1; // ignore ownerCandidate

            var dockedShipCount = int.Parse(metadata.Pop());
            for (var i = 0; i < dockedShipCount; ++i) dockedShips.Add(int.Parse(metadata.Pop()));

            return new Planet(owner, id, xPos, yPos, health, radius, dockingSpots, currentProduction, remainingProduction, dockedShips);
        }

        public static int ParsePlayerNum(Metadata metadata) { return int.Parse(metadata.Pop()); }

        public static int ParsePlayerId(Metadata metadata) { return int.Parse(metadata.Pop()); }
    }
}