using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// Designer-tunable stats for the Harvester unit.
    /// </summary>
    [CreateAssetMenu(menuName = "MechTS/Economy/Harvester Config")]
    public class HarvesterConfig : ScriptableObject
    {
        public float gatherRate = 5f;
        public int carryCapacity = 20;
        public float moveSpeed = 3.5f;
        public float maxHealth = 50f;

        [Header("Engine Audio")]
        public AudioClip engineSound;

        /// <summary>
        /// Scales this Harvester's track-roll flipbook advance rate (issue #47) —
        /// <see cref="Units.VehicleAnimationDriver"/> feeds it raw <c>NavMeshAgent</c>
        /// velocity magnitude (world units/sec, tops out at <see cref="moveSpeed"/>), but
        /// <c>WheelAnimation.Speed</c> actually means "frame-index advance per second," not a
        /// real rolling speed — at this unit's 5-frame track flipbook, the raw velocity value
        /// alone (~3.5 at max) reads as a slow, visible step rather than smooth rolling. This
        /// multiplier rescales it into a real frame-advance rate.
        /// </summary>
        public float trackAnimationSpeedMultiplier = 5f;

        /// <summary>
        /// This Harvester's ongoing upkeep cost per minute while active during the Economy
        /// Round (issue #48) — kept low since a Harvester is the economy engine itself, not a
        /// standing army cost. Pushed into <see cref="Units.UnitBase.SetUpkeepCost"/> from
        /// <see cref="HarvesterGatherBehavior.Start"/>, alongside the existing maxHealth/
        /// moveSpeed calls.
        /// </summary>
        [Header("Upkeep (per minute)")]
        public int oreUpkeepPerMinute = 1;
        public int biomassUpkeepPerMinute;
        public int goldUpkeepPerMinute;

        /// <summary>
        /// Tuning for <see cref="Units.HarvesterPartAnimator"/>'s track tread-texture scroll
        /// (issue #65/#67/#68 follow-up) — distinct from <see cref="trackAnimationSpeedMultiplier"/>
        /// above, which only ever meant something for the old 2D sprite's flipbook. Not read by
        /// the Enemy Harvester, which still uses the 2D sprite pipeline. The tracks are a flat
        /// plate whose UVs tile the tread pattern along U (confirmed via direct mesh inspection)
        /// — physically rotating that plate reads as tumbling, not rolling, so this scrolls the
        /// texture instead, in texture-widths/second at the unit's full configured move speed.
        /// The claw animation itself needs no tuning here — it plays the 3D model's own bundled
        /// clip at its authored speed, only gated on/off by <see cref="HarvesterGatherBehavior.IsGathering"/>.
        /// </summary>
        [Header("3D Model Part Animation")]
        public float trackScrollSpeed = 2f;
    }
}
