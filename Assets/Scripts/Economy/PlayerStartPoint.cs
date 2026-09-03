using MechTS.Units;
using MechTS.Utilities;
using UnityEngine;

namespace MechTS.Economy
{
    /// <summary>
    /// A Map Editor-painted marker (see <c>Assets/Editor/MapEditorWindow.cs</c>) identifying
    /// where a faction's Main Building spawns at Economy Round start (see
    /// <see cref="EconomyManager"/>). Purely an authoring-time position marker — no runtime
    /// renderer or collider, visible only as a Scene view gizmo.
    /// </summary>
    public class PlayerStartPoint : MonoBehaviour
    {
        private const float GizmoRadius = 1.5f;

        /// <summary>The faction this start point places, pre-set per brush prefab.</summary>
        public Faction faction;

        /// <summary>
        /// Draws a faction-colored wire sphere in the Scene view so a painted start point is
        /// visible while authoring, even though it has no runtime visual.
        /// </summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = FactionColor.GetColor(faction);
            Gizmos.DrawWireSphere(transform.position, GizmoRadius);
        }
    }
}
