using UnityEngine;

namespace MechTS.AI
{
    /// <summary>
    /// Designer-tunable settings for the Enemy faction's economic AI (issue #79) — how many
    /// Harvesters it aims to keep standing, and how often it re-evaluates production and node
    /// assignment. Injected via <see cref="Core.Bootstrapper"/> like every other per-mission
    /// config.
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/AI/Enemy AI Economy Config")]
    public class EnemyAIEconomyConfig : ScriptableObject
    {
        [Tooltip("How many Harvesters the Enemy faction tries to keep alive + queued at once.")]
        public int targetHarvesterCount = 3;

        [Tooltip("Seconds between each production/assignment re-evaluation.")]
        public float decisionIntervalSeconds = 2f;
    }
}
