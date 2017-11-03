using System;
using System.Collections.Generic;
using System.Linq;

namespace Halite2.hlt
{
    public class GameMap
    {
        private readonly List<Ship> allShips;
        // used only during parsing to reduce memory allocations
        private readonly List<Ship> currentShips = new List<Ship>();
        private readonly List<Player> players;
        Dictionary<int, Ship> ships;

        public Dictionary<int, Planet> AllPlanets { get; }
        public IList<Player> AllPlayers { get; }
        public IList<Ship> AllShips { get; }
        public int Height { get; }
        public Player MyPlayer => AllPlayers[MyPlayerId];
        public int MyPlayerId { get; }
        public int Width { get; }

        public GameMap(int width, int height, int playerId) {
            Width = width;
            Height = height;
            MyPlayerId = playerId;
            players = new List<Player>(Constants.MAX_PLAYERS);
            AllPlayers = players.AsReadOnly();
            AllPlanets = new Dictionary<int, Planet>();
            allShips = new List<Ship>();
            AllShips = allShips.AsReadOnly();
        }

        public Ship GetShip(int playerId, int entityId) { return players[playerId].GetShip(entityId); }

        public Ship GetShip(int entityId) {
            Ship ship;
            ships.TryGetValue(entityId, out ship);
            return ship;
        }

        public Planet GetPlanet(int entityId) { return AllPlanets[entityId]; }

        public List<Entity> ObjectsBetween(Position start, Position target) {
            var entitiesFound = new List<Entity>();

            AddEntitiesBetween(entitiesFound, start, target, AllPlanets.Values.ToList<Entity>());
            AddEntitiesBetween(entitiesFound, start, target, allShips.ToList<Entity>());

            return entitiesFound;
        }

        private static void AddEntitiesBetween(List<Entity> entitiesFound, Position start, Position target, ICollection<Entity> entitiesToCheck) {
            foreach (var entity in entitiesToCheck) {
                if (entity.Equals(start) || entity.Equals(target)) continue;
                if (Collision.SegmentCircleIntersect(start, target, entity, Constants.FORECAST_FUDGE_FACTOR)) entitiesFound.Add(entity);
            }
        }

        public Dictionary<Entity, double> NearbyEntitiesByDistance(Entity entity) {
            var entityByDistance = new Dictionary<Entity, double>();

            foreach (var planet in AllPlanets.Values) {
                if (planet.Equals(entity)) continue;
                entityByDistance[planet] = entity.GetDistanceTo(planet);
            }

            foreach (var ship in allShips) {
                if (ship.Equals(entity)) continue;
                entityByDistance[ship] = entity.GetDistanceTo(ship);
            }

            return entityByDistance;
        }

        public GameMap UpdateMap(Metadata mapMetadata) {
            var numberOfPlayers = MetadataParser.ParsePlayerNum(mapMetadata);

            players.Clear();
            AllPlanets.Clear();
            allShips.Clear();

            // update players info
            for (var i = 0; i < numberOfPlayers; ++i) {
                currentShips.Clear();
                var currentPlayerShips = new Dictionary<int, Ship>();
                var playerId = MetadataParser.ParsePlayerId(mapMetadata);

                var currentPlayer = new Player(playerId, currentPlayerShips);
                MetadataParser.PopulateShipList(currentShips, playerId, mapMetadata);
                allShips.AddRange(currentShips);

                foreach (var ship in currentShips) currentPlayerShips[ship.Id] = ship;
                players.Add(currentPlayer);
            }

            ships = allShips.ToDictionary(s => s.Id);

            var numberOfPlanets = int.Parse(mapMetadata.Pop());

            for (var i = 0; i < numberOfPlanets; ++i) {
                var dockedShips = new List<int>();
                var planet = MetadataParser.NewPlanetFromMetadata(dockedShips, mapMetadata);
                AllPlanets[planet.Id] = planet;
            }

            if (!mapMetadata.IsEmpty()) throw new InvalidOperationException("Failed to parse data from Halite game engine. Please contact maintainers.");

            return this;
        }
    }
}