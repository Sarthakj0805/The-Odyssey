using System;
using UnityEngine;

namespace Odyssey.Combat
{
    /// <summary>Component wrapper around <see cref="HealthModel"/>; implements <see cref="IDamageable"/>.</summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private Team team = Team.Enemy;
        [SerializeField] private float maxHealth = 30f;
        [SerializeField] private float iFramesOnHit = 0.1f;

        public HealthModel Model { get; private set; }
        public Team Team => team;
        public bool IsAlive => Model != null && !Model.IsDead;

        /// <summary>Raised for every applied hit, after HP changed.</summary>
        public event Action<DamageInfo, float> Hit;
        public event Action<DamageInfo> Died;

        private DamageInfo _lastHit;

        public void Configure(Team t, float max, float iFrames)
        {
            team = t; maxHealth = max; iFramesOnHit = iFrames;
            Model = new HealthModel(max);
            Model.Died += () => Died?.Invoke(_lastHit);
        }

        private void Awake()
        {
            if (Model == null) Configure(team, maxHealth, iFramesOnHit);
        }

        private void Update() => Model.Tick(Time.deltaTime);

        public bool TakeDamage(DamageInfo info)
        {
            if (Model == null || info.SourceTeam == team) return false;
            _lastHit = info;
            float applied = Model.ApplyDamage(info.Amount, iFramesOnHit);
            if (applied <= 0f) return false;
            Hit?.Invoke(info, applied);
            return true;
        }
    }
}
