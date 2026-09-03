using MechTS.Units;
using UnityEngine;

namespace MechTS.Utilities
{
    /// <summary>
    /// Placeholder visual differentiation for faction-owned GameObjects, until real
    /// per-faction art exists. Tints a Renderer's material — safe for primitive
    /// placeholder geometry only; per CLAUDE.md's art convention, real painted sprite
    /// art must never be tinted this way once it's in use.
    /// </summary>
    public static class FactionColor
    {
        private static readonly Color PlayerColor = new Color(0.2f, 0.45f, 0.95f);
        private static readonly Color EnemyColor = new Color(0.9f, 0.25f, 0.2f);

        /// <summary>
        /// Returns the placeholder color associated with a faction.
        /// </summary>
        /// <param name="faction">The faction to look up.</param>
        public static Color GetColor(Faction faction)
        {
            return faction == Faction.Player ? PlayerColor : EnemyColor;
        }

        /// <summary>
        /// Tints every placeholder MeshRenderer under the given GameObject with its
        /// faction's color. Deliberately skips SpriteRenderers — per CLAUDE.md's art
        /// convention, real painted sprite art must stay white and is never tinted. Units
        /// using real art show faction via their own merged faction/selection ring instead
        /// (see <see cref="Units.UnitBase"/>), not by tinting.
        /// </summary>
        /// <param name="go">The GameObject to tint.</param>
        /// <param name="faction">The faction that owns it.</param>
        public static void Apply(GameObject go, Faction faction)
        {
            var color = GetColor(faction);
            foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.material.color = color;
            }
        }
    }
}
