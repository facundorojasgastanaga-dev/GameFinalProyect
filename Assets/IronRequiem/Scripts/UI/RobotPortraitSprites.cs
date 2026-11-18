using UnityEngine;

namespace IronRequiem
{
    /// <summary>Preloaded portrait references. Keeps the v01 fields so its archived asset remains readable.</summary>
    public sealed class RobotPortraitSprites : ScriptableObject
    {
        public enum Pose { Front, Left, Right, Impact, Charge }
        public Sprite Neutral, LookLeft, LookRight, Impact, Critical, Attack, Overdrive, Offline;
        [SerializeField] DamageSet[] damageLevels;
        public bool HasDamageLevels => damageLevels != null && damageLevels.Length == 4;

        [System.Serializable]
        sealed class DamageSet
        {
            [SerializeField] Sprite front, left, right, impact, charge;
            public DamageSet(Sprite front, Sprite left, Sprite right, Sprite impact, Sprite charge)
            { this.front = front; this.left = left; this.right = right; this.impact = impact; this.charge = charge; }
            public Sprite Get(Pose pose)
            {
                switch (pose)
                {
                    case Pose.Left: return left;
                    case Pose.Right: return right;
                    case Pose.Impact: return impact;
                    case Pose.Charge: return charge;
                    default: return front;
                }
            }
        }

        public Sprite Get(int damage, Pose pose)
        {
            if (HasDamageLevels) return damageLevels[Mathf.Clamp(damage, 0, 3)].Get(pose);
            // Legacy data is retained for the v01 asset, never used by the installed v02 HUD.
            return damage >= 3 ? Critical : pose == Pose.Left ? LookLeft : pose == Pose.Right ? LookRight :
                pose == Pose.Impact ? Impact : Neutral;
        }

        /// <summary>Editor-time assignment: four groups of front/left/right/impact/charge, then attack/overdrive/offline.</summary>
        public void ConfigureV02(Sprite[] poses)
        {
            if (poses == null || poses.Length != 23) throw new System.ArgumentException("Expected all 23 portrait sprites.");
            damageLevels = new DamageSet[4];
            for (int i = 0; i < 4; i++)
            {
                int n = i * 5;
                damageLevels[i] = new DamageSet(poses[n], poses[n + 1], poses[n + 2], poses[n + 3], poses[n + 4]);
            }
            Neutral = poses[0]; LookLeft = poses[1]; LookRight = poses[2]; Impact = poses[3]; Critical = poses[15];
            Attack = poses[20]; Overdrive = poses[21]; Offline = poses[22];
        }
    }
}
