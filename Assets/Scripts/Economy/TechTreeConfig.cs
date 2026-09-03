using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Designer-tunable per-mission tech tree setup: each faction's own set of
    /// researchable upgrades. Humans and Aliens draw from separate lists — see
    /// <see cref="FactionTechTree"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/Tech/Tech Tree Config")]
    public class TechTreeConfig : ScriptableObject
    {
        public FactionTechTree[] factionTrees;
    }
}
