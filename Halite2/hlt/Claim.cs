using System;
using Halite2.hlt;

namespace Halite2
{
    public class Claim
    {
        private Move move;

        public Claim(int planetId, ClaimType type, Move move) {
            PlanetId = planetId;
            Type = type;
            Move = move;

            CalculatePosition();
        }

        private void CalculatePosition() {
            var thrustMove = Move as ThrustMove;
            if (thrustMove == null) return;
            
            var x = thrustMove.Thrust * Math.Cos(thrustMove.Angle * Math.PI/180);
            var y = thrustMove.Thrust * Math.Sin(thrustMove.Angle * Math.PI/180);
            move.Ship.XPos += x;
            move.Ship.YPos += y;
        }

        public int PlanetId { get; }
        public ClaimType Type { get; }
        public Move Move { 
            get{
                return move;
            } 
            set{
                move = value;
                CalculatePosition();
            }
        }
    }
}