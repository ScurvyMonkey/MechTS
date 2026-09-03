using System;
using System.Collections.Generic;
using MechTS.Core;
using MechTS.Economy;
using MechTS.Units;
using MechTS.Utilities;
using MechTS.Vision;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MechTS.UI
{
    /// <summary>
    /// Top-right icon-based overview of the whole map: colored markers for units,
    /// buildings, main buildings, and resource nodes, plus a rectangle showing the main
    /// camera's current visible area. Clicking anywhere on it jumps the camera there via
    /// <see cref="CameraManager.JumpToPosition"/>. World-to-minimap coordinate mapping uses
    /// <see cref="CameraManager"/>'s own <see cref="CameraConfig"/> bounds, so there's only
    /// one definition of "the map's extent" in the project.
    /// </summary>
    public class MinimapPanel : MonoBehaviour
    {
        private const float MinimapSize = 200f;
        private const float MarkerSize = 6f;
        private const float MainBuildingMarkerSize = 10f;

        private static readonly Color ViewportColor = new Color(1f, 1f, 1f, 0.3f);
        private static readonly Color OreColor = new Color(0.7f, 0.55f, 0.35f);
        private static readonly Color BiomassColor = new Color(0.3f, 0.75f, 0.3f);
        private static readonly Color GoldColor = new Color(0.9f, 0.8f, 0.2f);

        private EconomyManager _economyManager;
        private UnitManager _unitManager;
        private CameraManager _cameraManager;
        private VisionManager _visionManager;
        private Camera _camera;

        private RectTransform _background;
        private RectTransform _markersGroup;
        private RectTransform _viewportRect;

        /// <summary>
        /// Builds the minimap's background, marker group, and viewport-rect overlay.
        /// </summary>
        private void Awake()
        {
            _background = UIFactory.CreatePanel(transform, "Background", UIFactory.TopRight, new Vector2(-10f, -10f), new Vector2(MinimapSize, MinimapSize));
            _background.gameObject.AddComponent<ClickTarget>().OnClicked = HandleMinimapClick;

            _markersGroup = UIFactory.CreateRect(_background, Vector2.zero, Vector2.zero, Vector2.zero);

            _viewportRect = UIFactory.CreateRect(_background, Vector2.zero, Vector2.zero, Vector2.zero);
            var viewportImage = _viewportRect.gameObject.AddComponent<Image>();
            viewportImage.color = ViewportColor;
            viewportImage.raycastTarget = false;
        }

        /// <summary>
        /// Caches the scene's managers and the main camera.
        /// </summary>
        private void Start()
        {
            _economyManager = FindFirstObjectByType<EconomyManager>();
            _unitManager = FindFirstObjectByType<UnitManager>();
            _cameraManager = FindFirstObjectByType<CameraManager>();
            _visionManager = FindFirstObjectByType<VisionManager>();
            _camera = Camera.main;
        }

        /// <summary>
        /// Rebuilds markers every tick and refreshes the viewport rect. Reads only
        /// already-maintained registries (<see cref="UnitManager.ActiveUnits"/>,
        /// <see cref="EconomyManager"/>'s building/main-building/node lists) — never a
        /// per-frame scene-wide scan. Always rebuilds (rather than only on a set-membership
        /// change, as before issue #36) since an enemy marker's fog-gated visibility can
        /// change every tick without the underlying tracked set changing at all — at this
        /// project's current unit-count scale, destroying/recreating a handful of small UI
        /// markers each frame is cheap enough not to need the extra diff-tracking complexity
        /// that would otherwise be required to detect a fog-only change correctly.
        /// </summary>
        private void Update()
        {
            if (_economyManager == null || _unitManager == null || _cameraManager == null || _cameraManager.Config == null) return;

            RebuildMarkers();
            UpdateViewportRect();
        }

        /// <summary>
        /// Destroys and recreates one marker per currently-visible-to-the-Player unit/building/
        /// main building, plus every resource node. Player-owned units/buildings and resource
        /// nodes are always shown; enemy units only while <see cref="FogState.Visible"/>;
        /// enemy buildings/main buildings while <see cref="FogState.Visible"/> or
        /// <see cref="FogState.Explored"/> (matching their own remembered-marker behavior).
        /// </summary>
        private void RebuildMarkers()
        {
            foreach (Transform child in _markersGroup) Destroy(child.gameObject);

            foreach (var unit in _unitManager.ActiveUnits)
            {
                if (!ShouldShowOnMinimap(unit.Faction, unit.transform.position, requireFullyVisible: true)) continue;
                CreateMarker(unit.transform.position, FactionColor.GetColor(unit.Faction), MarkerSize);
            }

            var playerState = _economyManager.GetState(Faction.Player);
            var enemyState = _economyManager.GetState(Faction.Enemy);
            if (playerState != null) CreateBuildingMarkers(playerState.Buildings);
            if (enemyState != null) CreateBuildingMarkers(enemyState.Buildings);

            foreach (var mainBuilding in _economyManager.MainBuildings.Values)
            {
                if (!ShouldShowOnMinimap(mainBuilding.Faction, mainBuilding.transform.position, requireFullyVisible: false)) continue;
                CreateMarker(mainBuilding.transform.position, FactionColor.GetColor(mainBuilding.Faction), MainBuildingMarkerSize);
            }

            foreach (var node in _economyManager.ResourceNodes)
            {
                CreateMarker(node.transform.position, GetResourceColor(node.ResourceType), MarkerSize);
            }
        }

        /// <summary>
        /// Creates one marker per building in the given list that should currently show on the minimap.
        /// </summary>
        /// <param name="buildings">The buildings to consider.</param>
        private void CreateBuildingMarkers(IEnumerable<BuildingInstance> buildings)
        {
            foreach (var building in buildings)
            {
                if (!ShouldShowOnMinimap(building.Faction, building.transform.position, requireFullyVisible: false)) continue;
                CreateMarker(building.transform.position, FactionColor.GetColor(building.Faction), MarkerSize);
            }
        }

        /// <summary>
        /// Returns whether a faction-owned thing at a position should currently show a
        /// minimap marker: always true for the Player's own; for the Enemy, gated on the
        /// Player faction's fog state — <paramref name="requireFullyVisible"/> true means
        /// only while <see cref="FogState.Visible"/> (units, which are never remembered),
        /// false means while <see cref="FogState.Visible"/> or <see cref="FogState.Explored"/>
        /// (buildings, which show a remembered marker like their own in-world ghost marker).
        /// </summary>
        /// <param name="ownerFaction">The thing's owning faction.</param>
        /// <param name="worldPosition">The thing's world position.</param>
        /// <param name="requireFullyVisible">True for units, false for buildings/main buildings.</param>
        private bool ShouldShowOnMinimap(Faction ownerFaction, Vector3 worldPosition, bool requireFullyVisible)
        {
            if (ownerFaction == Faction.Player || _visionManager == null) return true;

            var fogState = _visionManager.GetFogState(Faction.Player, worldPosition);
            return requireFullyVisible ? fogState == FogState.Visible : fogState != FogState.Unexplored;
        }

        /// <summary>
        /// Creates one colored square marker at the minimap position corresponding to a world position.
        /// </summary>
        /// <param name="worldPosition">The world position to mark.</param>
        /// <param name="color">The marker's color.</param>
        /// <param name="size">The marker's width/height in pixels.</param>
        private void CreateMarker(Vector3 worldPosition, Color color, float size)
        {
            var rect = UIFactory.CreateRect(_markersGroup, Vector2.zero, WorldToMinimapLocal(worldPosition), new Vector2(size, size));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        /// <summary>
        /// Returns the marker color for a resource type — Ore, Biomass, and Gold are each
        /// visually distinguished, per the acceptance criteria.
        /// </summary>
        /// <param name="resourceType">The resource type to look up.</param>
        private static Color GetResourceColor(ResourceType resourceType)
        {
            switch (resourceType)
            {
                case ResourceType.Ore: return OreColor;
                case ResourceType.Biomass: return BiomassColor;
                default: return GoldColor;
            }
        }

        /// <summary>
        /// Repositions and resizes the viewport rectangle to approximate the main camera's
        /// current visible world area, centered on <see cref="CameraManager.FocusPoint"/> and
        /// sized from <see cref="CameraManager.Distance"/> (issue #80) — the old
        /// <c>orthographicSize</c>-based calculation no longer applies now that the camera is
        /// Perspective. A true angled-frustum footprint on the ground is a trapezoid, not a
        /// rectangle; this is a deliberate approximation, not a precise reprojection.
        /// </summary>
        private void UpdateViewportRect()
        {
            if (_camera == null || _cameraManager == null) return;

            float halfHeight = _cameraManager.Distance;
            float halfWidth = halfHeight * _camera.aspect;
            Vector3 focus = _cameraManager.FocusPoint;

            Vector2 min = WorldToMinimapLocal(new Vector3(focus.x - halfWidth, 0f, focus.z - halfHeight));
            Vector2 max = WorldToMinimapLocal(new Vector3(focus.x + halfWidth, 0f, focus.z + halfHeight));

            _viewportRect.anchoredPosition = min;
            _viewportRect.sizeDelta = max - min;
        }

        /// <summary>
        /// Converts a world position (X/Z) to a local pixel offset from the minimap
        /// background's bottom-left corner, per <see cref="CameraManager.Config"/>'s bounds.
        /// </summary>
        /// <param name="worldPosition">The world position to convert.</param>
        private Vector2 WorldToMinimapLocal(Vector3 worldPosition)
        {
            var bounds = _cameraManager.Config;
            float fractionX = Mathf.InverseLerp(bounds.boundsMin.x, bounds.boundsMax.x, worldPosition.x);
            float fractionZ = Mathf.InverseLerp(bounds.boundsMin.y, bounds.boundsMax.y, worldPosition.z);
            return new Vector2(fractionX * MinimapSize, fractionZ * MinimapSize);
        }

        /// <summary>
        /// Converts a local click point (relative to the background rect's bottom-center
        /// pivot) into a world position, the inverse of <see cref="WorldToMinimapLocal"/>.
        /// </summary>
        /// <param name="localPoint">The click point in the background rect's local space.</param>
        private Vector3 MinimapLocalToWorld(Vector2 localPoint)
        {
            var bounds = _cameraManager.Config;
            float fractionX = (localPoint.x + MinimapSize * 0.5f) / MinimapSize;
            float fractionZ = localPoint.y / MinimapSize;
            float worldX = Mathf.Lerp(bounds.boundsMin.x, bounds.boundsMax.x, fractionX);
            float worldZ = Mathf.Lerp(bounds.boundsMin.y, bounds.boundsMax.y, fractionZ);
            return new Vector3(worldX, 0f, worldZ);
        }

        /// <summary>
        /// Resolves a click on the minimap into a world position and jumps the main camera there.
        /// </summary>
        /// <param name="eventData">The click event, used for its screen position.</param>
        private void HandleMinimapClick(PointerEventData eventData)
        {
            if (_cameraManager == null || _cameraManager.Config == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_background, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);
            _cameraManager.JumpToPosition(MinimapLocalToWorld(localPoint));
        }

        /// <summary>
        /// Forwards UGUI pointer-click events to a delegate — attached to the minimap
        /// background so <see cref="MinimapPanel"/> itself doesn't need to be the raycast
        /// target's own component.
        /// </summary>
        private class ClickTarget : MonoBehaviour, IPointerClickHandler
        {
            public Action<PointerEventData> OnClicked;

            /// <summary>Invokes <see cref="OnClicked"/> with the click event.</summary>
            /// <param name="eventData">The click event.</param>
            public void OnPointerClick(PointerEventData eventData) => OnClicked?.Invoke(eventData);
        }
    }
}
