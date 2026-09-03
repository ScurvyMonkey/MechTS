using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Designer-tunable magnitudes for sabotage effects. Harvester and resource-node
    /// effects (destroy / drain) are absolute and need no magnitude; only the
    /// building-disable effect has a tunable duration.
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/Economy/Sabotage Effect Config")]
    public class SabotageEffectConfig : ScriptableObject
    {
        [Tooltip("Seconds a sabotaged building stops functioning for.")]
        public float buildingDisableDuration = 20f;
    }
}
