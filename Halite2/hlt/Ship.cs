using System;

namespace Halite2.hlt
{
    public class Ship : Entity
    {
        public enum DockingStatus { Undocked = 0, Docking = 1, Docked = 2, Undocking = 3 }

        private DockingStatus dockingStatus;
        private int dockedPlanet;
        private int dockingProgress;
        private int weaponCooldown;
        private bool claimed;

        public Ship(int owner, int id, double xPos, double yPos,
                    int health, DockingStatus dockingStatus, int dockedPlanet,
                    int dockingProgress, int weaponCooldown)
            : base(owner, id, xPos, yPos, health, Constants.SHIP_RADIUS)
        {
            this.dockingStatus = dockingStatus;
            this.dockedPlanet = dockedPlanet;
            this.dockingProgress = dockingProgress;
            this.weaponCooldown = weaponCooldown;
        }

        public int GetWeaponCooldown()
        {
            return weaponCooldown;
        }

        public DockingStatus GetDockingStatus()
        {
            return dockingStatus;
        }

        public int GetDockingProgress()
        {
            return dockingProgress;
        }

        public int GetDockedPlanet()
        {
            return dockedPlanet;
        }

        public bool CanDock(Planet planet)
        {
            return GetDistanceTo(planet) <= Constants.DOCK_RADIUS + planet.GetRadius();
        }

        public override string ToString()
        {
            return "Ship[" +
                    base.ToString() +
                    ", dockingStatus=" + dockingStatus +
                    ", dockedPlanet=" + dockedPlanet +
                    ", dockingProgress=" + dockingProgress +
                    ", weaponCooldown=" + weaponCooldown +
                    "]";
        }

        public void Claim(Planet planet){
            if(!claimed){
                DebugLog.AddLog($"Ship: {GetId()} Claimed!");
                claimed = true;
            }
            else{
                throw new Exception("Claimed already claimed entity.");
            }
        }

        public void ClaimStateless(){
            claimed = true;
            DebugLog.AddLog($"Ship: {GetId()} Claimed For One Round!");
        }

        public bool Claimed{get{return claimed;}}
    }
}
