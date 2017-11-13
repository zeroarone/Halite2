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

            CalculateNewPosition();
        }
        public int PlanetId { get; }
        public ClaimType Type { get; }
        public Move Move { 
            get{
                return move;
            } 
            set{
                move = value;
                CalculateNewPosition();
            }
        }
        private void  CalculateNewPosition(){
            var thrustMove = Move as ThrustMove;
            if(thrustMove != null){
                DebugLog.AddLog($"PreviousPosition:(x - {Move.Ship.XPos})^2 + (y - {Move.Ship.YPos})^2 = {Move.Ship.Radius}^2");
                Move.Ship.XPos += thrustMove.Thrust * Math.Cos(thrustMove.Angle * Math.PI/180);
                Move.Ship.YPos += thrustMove.Thrust * Math.Sin(thrustMove.Angle * Math.PI/180);
                DebugLog.AddLog($"NewPosition:(x - {Move.Ship.XPos})^2 + (y - {Move.Ship.YPos})^2 = {Move.Ship.Radius}^2");
            }
        }
    }
}