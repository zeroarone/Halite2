using System;
using Halite2.hlt;

namespace Halite2
{
    public class Claim
    {
        private Move move;
        private Position position;
        private bool recalculatePosition;

        public Claim(int planetId, ClaimType type, Move move) {
            PlanetId = planetId;
            Type = type;
            Move = move;
        }
        public int PlanetId { get; }
        public ClaimType Type { get; }
        public Move Move { 
            get{
                return move;
            } 
            set{
                move = value;
                recalculatePosition = true;
            }
        }
        public Position NewPosition{
            get{
                if(recalculatePosition){
                    var thrustMove = Move as ThrustMove;
                    if(thrustMove == null){
                        position = Move.Ship;
                    }
                    else{
                        var x = thrustMove.Thrust * Math.Cos(thrustMove.Angle * Math.PI/180);
                        var y = thrustMove.Thrust * Math.Sin(thrustMove.Angle * Math.PI/180);
                        position = new Position(Move.Ship.XPos + x, Move.Ship.YPos + y);
                    }
                }
                return position;
            }
        }
    }
}