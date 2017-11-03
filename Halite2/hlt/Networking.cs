using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Halite2.hlt
{
    public class Networking
    {
        private static char UNDOCK_KEY = 'u';
        private static char DOCK_KEY = 'd';
        private static char THRUST_KEY = 't';

        public static void SendMoves(IEnumerable<Move> moves) {
            StringBuilder moveString = new StringBuilder();

            foreach (Move move in moves) {
                switch (move.Type) {
                    case MoveType.Noop:
                        continue;
                    case MoveType.Undock:
                        moveString.Append(UNDOCK_KEY)
                            .Append(" ")
                            .Append(move.Ship.Id)
                            .Append(" ");
                        break;
                    case MoveType.Dock:
                        moveString.Append(DOCK_KEY)
                            .Append(" ")
                            .Append(move.Ship.Id)
                            .Append(" ")
                            .Append(((DockMove) move).DestinationId)
                            .Append(" ");
                        break;
                    case MoveType.Thrust:
                        moveString.Append(THRUST_KEY)
                            .Append(" ")
                            .Append(move.Ship.Id)
                            .Append(" ")
                            .Append(((ThrustMove) move).Thrust)
                            .Append(" ")
                            .Append(((ThrustMove) move).Angle)
                            .Append(" ");
                        break;
                }
            }
            Console.WriteLine(moveString);
        }

        private static String ReadLine() {
            try {
                StringBuilder builder = new StringBuilder();
                int buffer;

                for (; (buffer = Console.Read()) >= 0;) {
                    if (buffer == '\n') {
                        break;
                    }
                    if (buffer == '\r') {
                        // Ignore carriage return if on windows for manual testing.
                        continue;
                    }
                    builder = builder.Append((char) buffer);
                }
                return builder.ToString();
            }
            catch (Exception) {
                Environment.Exit(0);
                return null;
            }
        }

        public static Metadata ReadLineIntoMetadata() {
            var metadata = ReadLine();
            DebugLog.AddLog($"Metadata: '{metadata}'");
            return new Metadata(metadata.Trim().Split(' '));
            return new Metadata(ReadLine().Trim().Split(' '));
        }

        public GameMap Initialize(String botName) {
            int myId = int.Parse(ReadLine());
            DebugLog.Initialize(new StreamWriter(String.Format($"{botName}.log")));

            Metadata inputStringMapSize = ReadLineIntoMetadata();
            int width = int.Parse(inputStringMapSize.Pop());
            int height = int.Parse(inputStringMapSize.Pop());
            GameMap gameMap = new GameMap(width, height, myId);

            // Associate bot name
            Console.WriteLine(botName);

            Metadata inputStringMetadata = ReadLineIntoMetadata();
            gameMap.UpdateMap(inputStringMetadata);

            return gameMap;
        }
    }
}
