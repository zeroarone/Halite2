using System;

namespace Halite2.hlt
{
    public class Ship : Entity
    {
        

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

        public int WeaponCooldown {
            get {
                return weaponCooldown;
            }
        }

        public DockingStatus DockingStatus {
            get {
                return dockingStatus;
            }
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
            return GetDistanceTo(planet) <= Constants.DOCK_RADIUS + planet.Radius;
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

        public void Claim(){
            if(!claimed){
                DebugLog.AddLog($"Ship: {Id} Claimed!");
                claimed = true;
            }
            else{
                throw new Exception("Claimed already claimed entity.");
            }
        }
        
        public bool Claimed{get{return claimed;}}
    }
}
