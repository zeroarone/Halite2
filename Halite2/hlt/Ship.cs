using System;

namespace Halite2.hlt
{
    public class Ship : Entity
    {
        public Ship(int owner, int id, double xPos, double yPos,
                    int health, DockingStatus dockingStatus, int dockedPlanet,
                    int dockingProgress, int weaponCooldown)
            : base(owner, id, xPos, yPos, health, Constants.SHIP_RADIUS)
        {
            this.DockingStatus = dockingStatus;
            this.DockedPlanet = dockedPlanet;
            this.DockingProgress = dockingProgress;
            this.WeaponCooldown = weaponCooldown;
        }

        public int WeaponCooldown { get; }

        public DockingStatus DockingStatus { get; }

        public int DockingProgress { get; }

        public int DockedPlanet { get; }
        
        public ClaimType Claim { get; set; }

        public bool CanDock(Planet planet)
        {
            return GetDistanceTo(planet) <= Constants.DOCK_RADIUS + planet.Radius;
        }

        public override string ToString()
        {
            return "Ship[" +
                    base.ToString() +
                    ", dockingStatus=" + DockingStatus +
                    ", dockedPlanet=" + DockedPlanet +
                    ", dockingProgress=" + DockingProgress +
                    ", weaponCooldown=" + WeaponCooldown +
                    "]";
        }
    }
}
