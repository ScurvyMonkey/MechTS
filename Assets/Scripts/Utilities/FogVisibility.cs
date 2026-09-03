using System.Collections.Generic;
using MechTS.Units;
using MechTS.Vision;
using UnityEngine;

namespace MechTS.Utilities
{
    /// <summary>
    /// Shared 3-state fog-visibility toggle for static structures (<see cref="Economy.BuildingInstance"/>,
    /// <see cref="Economy.MainBuilding"/>) — Player-owned structures are always fully visible;
    /// an enemy-owned structure shows full detail while <see cref="FogState.Visible"/>, a
    /// dimmed <see cref="FogGhostMarker"/> at its last-known position while
    /// <see cref="FogState.Explored"/>, and nothing while <see cref="FogState.Unexplored"/>.
    /// Units use their own simpler always-visible/hidden toggle instead (see
    /// <see cref="Units.UnitBase"/>) since a remembered "ghost" of something that moves would
    /// be misleading, not useful information.
    /// </summary>
    public static class FogVisibility
    {
        /// <summary>
        /// Applies the current fog state to a structure's real renderers and ghost marker.
        /// </summary>
        /// <param name="ownerFaction">The structure's owning faction.</param>
        /// <param name="worldPosition">The structure's world position.</param>
        /// <param name="visionManager">The scene's <see cref="VisionManager"/>, or null to treat everything as fully visible.</param>
        /// <param name="realRenderers">The structure's own renderers (its real, live appearance).</param>
        /// <param name="ghostMarker">The structure's lazily-created ghost marker, created on first use and reused afterward.</param>
        public static void Apply(Faction ownerFaction, Vector3 worldPosition, VisionManager visionManager,
            List<Renderer> realRenderers, ref GameObject ghostMarker)
        {
            if (ownerFaction == Faction.Player || visionManager == null)
            {
                SetRenderersEnabled(realRenderers, true);
                if (ghostMarker != null) ghostMarker.SetActive(false);
                return;
            }

            switch (visionManager.GetFogState(Faction.Player, worldPosition))
            {
                case FogState.Visible:
                    SetRenderersEnabled(realRenderers, true);
                    if (ghostMarker != null) ghostMarker.SetActive(false);
                    break;

                case FogState.Explored:
                    SetRenderersEnabled(realRenderers, false);
                    if (ghostMarker == null)
                    {
                        ghostMarker = FogGhostMarker.Create(worldPosition, ownerFaction);
                    }
                    else
                    {
                        ghostMarker.SetActive(true);
                        FogGhostMarker.SetPosition(ghostMarker, worldPosition);
                    }
                    break;

                default: // Unexplored
                    SetRenderersEnabled(realRenderers, false);
                    if (ghostMarker != null) ghostMarker.SetActive(false);
                    break;
            }
        }

        /// <summary>
        /// Enables or disables every renderer in the list, skipping any that have been destroyed.
        /// </summary>
        private static void SetRenderersEnabled(List<Renderer> renderers, bool enabled)
        {
            foreach (var renderer in renderers)
            {
                if (renderer != null) renderer.enabled = enabled;
            }
        }
    }
}
