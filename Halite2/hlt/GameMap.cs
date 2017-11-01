using System;
using System.Collections.Generic;
using System.Linq;

namespace Halite2.hlt {

    public class GameMap {
        private int width, height;
        private List<Player> players;
        private IList<Player> playersUnmodifiable;
        private Dictionary<int, Planet> planets;
        Dictionary<int, Ship> ships;
        private List<Ship> allShips;
        private IList<Ship> allShipsUnmodifiable;

        // used only during parsing to reduce memory allocations
        private List<Ship> currentShips = new List<Ship>();

        public GameMap(int width, int height, int playerId) {
            this.width = width;
            this.height = height;
            this.MyPlayerId = playerId;
            players = new List<Player>(Constants.MAX_PLAYERS);
            playersUnmodifiable = players.AsReadOnly();
            planets = new Dictionary<int, Planet>();
            allShips = new List<Ship>();
            allShipsUnmodifiable = allShips.AsReadOnly();
        }

        public int GetHeight() {
            return height;
        }

        public int GetWidth() {
            return width;
        }

        public int MyPlayerId { get; }

        public IList<Player> GetAllPlayers() {
            return playersUnmodifiable;
        }

        public Player GetMyPlayer() => playersUnmodifiable[MyPlayerId];

        public Ship GetShip(int playerId, int entityId) {
            return players[playerId].GetShip(entityId);
        }

        public Ship GetShip(int entityId){
            return ships[entityId];
        }

        public Planet GetPlanet(int entityId) {
            return planets[entityId];
        }

        public Dictionary<int, Planet> GetAllPlanets() {
            return planets;
        }

        public IList<Ship> GetAllShips() {
            return allShipsUnmodifiable;
        }

        public List<Entity> ObjectsBetween(Position start, Position target) {
            List<Entity> entitiesFound = new List<Entity>();

            AddEntitiesBetween(entitiesFound, start, target, planets.Values.ToList<Entity>());
            AddEntitiesBetween(entitiesFound, start, target, allShips.ToList<Entity>());

            return entitiesFound;
        }

        private static void AddEntitiesBetween(List<Entity> entitiesFound,
                                               Position start, Position target,
                                               ICollection<Entity> entitiesToCheck) {

            foreach (Entity entity in entitiesToCheck) {
                if (entity.Equals(start) || entity.Equals(target)) {
                    continue;
                }
                if (Collision.segmentCircleIntersect(start, target, entity, Constants.FORECAST_FUDGE_FACTOR)) {
                    entitiesFound.Add(entity);
                }
            }
        }

        public Dictionary<Entity, double> NearbyEntitiesByDistance(Entity entity) {
            Dictionary<Entity, double> entityByDistance = new Dictionary<Entity, double>();

            foreach (Planet planet in planets.Values) {
                if (planet.Equals(entity)) {
                    continue;
                }
                entityByDistance[planet] = entity.GetDistanceTo(planet);
            }

            foreach (Ship ship in allShips) {
                if (ship.Equals(entity)) {
                    continue;
                }
                entityByDistance[ship] = entity.GetDistanceTo(ship);
            }

            return entityByDistance;
        }

        public GameMap UpdateMap(Metadata mapMetadata) {
            int numberOfPlayers = MetadataParser.ParsePlayerNum(mapMetadata);

            players.Clear();
            planets.Clear();
            allShips.Clear();

            // update players info
            for (int i = 0; i < numberOfPlayers; ++i) {
                currentShips.Clear();
                Dictionary<int, Ship> currentPlayerShips = new Dictionary<int, Ship>();
                int playerId = MetadataParser.ParsePlayerId(mapMetadata);

                Player currentPlayer = new Player(playerId, currentPlayerShips);
                MetadataParser.PopulateShipList(currentShips, playerId, mapMetadata);
                allShips.AddRange(currentShips);

                foreach (Ship ship in currentShips) {
                    currentPlayerShips[ship.GetId()] = ship;
                }
                players.Add(currentPlayer);
            }

            ships = allShips.ToDictionary(s => s.GetId());

            int numberOfPlanets = int.Parse(mapMetadata.Pop());

            for (int i = 0; i < numberOfPlanets; ++i) {
                List<int> dockedShips = new List<int>();
                Planet planet = MetadataParser.NewPlanetFromMetadata(dockedShips, mapMetadata);
                planets[planet.GetId()] = planet;
            }

            if (!mapMetadata.IsEmpty()) {
                throw new InvalidOperationException("Failed to parse data from Halite game engine. Please contact maintainers.");
            }

            return this;
        }
    }

}
