using System;

namespace Odyssey.Combat
{
    /// <summary>
    /// Pure C# health logic (no Unity types) so damage, invulnerability frames and
    /// death can be unit-tested. <see cref="Health"/> wraps it as a component.
    /// </summary>
    public sealed class HealthModel
    {
        public float Max { get; private set; }
        public float Current { get; private set; }
        public float InvulnerableFor { get; private set; }
        public bool IsDead => Current <= 0f;
        public bool IsInvulnerable => InvulnerableFor > 0f;

        public event Action<float, float> Changed;   // (current, max)
        public event Action<float> Damaged;          // amount actually applied
        public event Action Died;

        public HealthModel(float max)
        {
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max));
            Max = max; Current = max;
        }

        /// <param name="iFrames">invulnerability granted after a successful hit</param>
        /// <returns>damage actually applied (0 if ignored)</returns>
        public float ApplyDamage(float amount, float iFrames = 0f)
        {
            if (IsDead || IsInvulnerable || amount <= 0f) return 0f;
            float applied = Math.Min(amount, Current);
            Current -= applied;
            InvulnerableFor = iFrames;
            Damaged?.Invoke(applied);
            Changed?.Invoke(Current, Max);
            if (IsDead) Died?.Invoke();
            return applied;
        }

        public float Heal(float amount)
        {
            if (IsDead || amount <= 0f) return 0f;
            float before = Current;
            Current = Math.Min(Max, Current + amount);
            if (Current != before) Changed?.Invoke(Current, Max);
            return Current - before;
        }

        /// <summary>Change max HP (e.g. Hera boon). Keeps the same missing-HP amount.</summary>
        public void SetMax(float newMax)
        {
            if (newMax <= 0f) throw new ArgumentOutOfRangeException(nameof(newMax));
            float missing = Max - Current;
            Max = newMax;
            Current = Math.Max(IsDead ? 0f : 1f, Math.Min(Max, Max - missing));
            Changed?.Invoke(Current, Max);
        }

        public void ResetFull()
        {
            Current = Max; InvulnerableFor = 0f;
            Changed?.Invoke(Current, Max);
        }

        public void GrantInvulnerability(float seconds) => InvulnerableFor = Math.Max(InvulnerableFor, seconds);

        public void Tick(float dt)
        {
            if (InvulnerableFor > 0f) InvulnerableFor = Math.Max(0f, InvulnerableFor - dt);
        }
    }
}
