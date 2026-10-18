using UnityEngine;

namespace Odyssey.Core
{
    /// <summary>Physics layers by name (created by the editor builder). Falls back to Default if missing.</summary>
    public static class Layers
    {
        public const string GroundName = "Ground", PlayerName = "Player", EnemyName = "Enemy", ProjectileName = "Projectile";

        public static int Ground => Get(GroundName);
        public static int Player => Get(PlayerName);
        public static int Enemy => Get(EnemyName);
        public static int Projectile => Get(ProjectileName);

        public static LayerMask GroundMask => Ground == 0 ? (LayerMask)Physics2D.DefaultRaycastLayers : (LayerMask)(1 << Ground);

        private static int Get(string name)
        {
            int l = LayerMask.NameToLayer(name);
            return l < 0 ? 0 : l;
        }

        /// <summary>Player and enemies pass through each other; enemies don't hit their own rocks.</summary>
        public static void ConfigureCollisionMatrix()
        {
            if (Player != 0 && Enemy != 0)
            {
                Physics2D.IgnoreLayerCollision(Player, Enemy, true);
                Physics2D.IgnoreLayerCollision(Enemy, Enemy, true);
            }
            if (Projectile != 0)
            {
                if (Enemy != 0) Physics2D.IgnoreLayerCollision(Projectile, Enemy, true);
                Physics2D.IgnoreLayerCollision(Projectile, Projectile, true);
            }
        }
    }
}
