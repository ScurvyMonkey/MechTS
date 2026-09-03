using MechTS.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MechTS.Core
{
    /// <summary>
    /// Gives the player pan/zoom control over the angled, fixed-pitch <c>Main Camera</c>
    /// (issue #80 — an SC2-style offset Perspective view, replacing the original straight-down
    /// Orthographic camera) during an active round — edge-of-screen and arrow-key pan, scroll-
    /// wheel zoom, both clamped to a configured world-space bounds — plus SC2-style saved
    /// camera locations (Ctrl+F1-F4 save, F1-F4 jump). Purely input-driven; never moves the
    /// camera on its own. Created by <see cref="Bootstrapper"/>.
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        private const int SavedLocationCount = 4;

        private CameraConfig _config;
        private GameManager _gameManager;
        private SelectionController _selectionController;
        private Camera _camera;
        private Vector3 _focusPoint;
        private float _distance;
        private readonly CameraSnapshot?[] _savedLocations = new CameraSnapshot?[SavedLocationCount];

        /// <summary>This camera's tunable pan/zoom/bounds config, for systems that need the
        /// same map bounds (e.g. <see cref="UI.MinimapPanel"/>'s world-to-minimap mapping).</summary>
        public CameraConfig Config => _config;

        /// <summary>The ground-plane point the camera is currently offset from and looking at
        /// — read by <see cref="UI.MinimapPanel"/> to approximate the camera's visible area.</summary>
        public Vector3 FocusPoint => _focusPoint;

        /// <summary>The camera's current distance from <see cref="FocusPoint"/> — what
        /// scroll-wheel zoom changes. Read by <see cref="UI.MinimapPanel"/> alongside
        /// <see cref="FocusPoint"/> for the same approximation.</summary>
        public float Distance => _distance;

        /// <summary>
        /// Assigns this manager's tunable config. Called by <see cref="Bootstrapper"/>
        /// immediately after creation, since it's instantiated at runtime rather than from
        /// a prefab with an Inspector-assigned reference.
        /// </summary>
        /// <param name="config">The camera's tunable pan/zoom/bounds config.</param>
        public void Initialize(CameraConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Caches the scene's <see cref="GameManager"/>, <see cref="SelectionController"/>,
        /// and the main camera; switches it from the scene-authored Orthographic top-down
        /// setup to the angled Perspective view this manager now drives (issue #80) — done
        /// here in code, not left to a one-time manual Editor edit, so the feature doesn't
        /// silently depend on the scene's <c>Main Camera</c> component already having the
        /// right projection/rotation baked in. <c>Main Camera</c> is a per-scene object, not
        /// <c>DontDestroyOnLoad</c> like this manager, so a future mission-to-mission scene
        /// reload would need this re-acquired — not an issue yet since no such reload exists.
        /// </summary>
        private void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            _selectionController = FindFirstObjectByType<SelectionController>();
            _camera = Camera.main;

            _camera.orthographic = false;
            _camera.fieldOfView = _config.fieldOfView;
            _camera.farClipPlane = _config.farClipPlane;

            _focusPoint = ClampToBounds(new Vector3(_camera.transform.position.x, 0f, _camera.transform.position.z));
            _distance = Mathf.Clamp(_config.defaultZoomDistance, _config.zoomDistanceMin, _config.zoomDistanceMax);

            UpdateCameraTransform();
        }

        /// <summary>
        /// Polls edge-of-screen pan, arrow-key pan, scroll-wheel zoom, and F1-F4 saved-location
        /// input each frame. Ignored outside an active round (e.g. while the Mission End screen
        /// is shown), matching every other input system's gating convention.
        /// </summary>
        private void Update()
        {
            if (_gameManager == null || !_gameManager.IsRoundActive || _camera == null) return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            Vector3 pan = Vector3.zero;
            if (keyboard != null) pan += ArrowKeyPan(keyboard);
            if (mouse != null) pan += EdgePan(mouse);

            if (pan.sqrMagnitude > 0f)
            {
                _focusPoint = ClampToBounds(_focusPoint + pan.normalized * (_config.panSpeed * Time.deltaTime));
                UpdateCameraTransform();
            }

            if (mouse != null) ApplyZoom(mouse);
            if (keyboard != null) HandleSavedLocations(keyboard);
        }

        /// <summary>
        /// Returns the world-space pan direction from currently-held arrow keys, or zero if none are held.
        /// </summary>
        /// <param name="keyboard">The current keyboard device.</param>
        private Vector3 ArrowKeyPan(Keyboard keyboard)
        {
            Vector3 direction = Vector3.zero;
            if (keyboard.upArrowKey.isPressed) direction += Vector3.forward;
            if (keyboard.downArrowKey.isPressed) direction += Vector3.back;
            if (keyboard.leftArrowKey.isPressed) direction += Vector3.left;
            if (keyboard.rightArrowKey.isPressed) direction += Vector3.right;
            return direction;
        }

        /// <summary>
        /// Returns the world-space pan direction implied by the cursor sitting within the
        /// configured margin of a screen edge, or zero if the cursor isn't near an edge, is
        /// over a UI element, or a drag-select box is in progress (see
        /// <see cref="SelectionController.IsDragging"/> — otherwise edge-panning would move
        /// the camera out from under an in-progress drag box).
        /// </summary>
        /// <param name="mouse">The current mouse device.</param>
        private Vector3 EdgePan(Mouse mouse)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return Vector3.zero;
            if (_selectionController != null && _selectionController.IsDragging) return Vector3.zero;

            Vector2 pos = mouse.position.ReadValue();
            float margin = _config.edgePanMarginPixels;

            Vector3 direction = Vector3.zero;
            if (pos.x <= margin) direction += Vector3.left;
            else if (pos.x >= Screen.width - margin) direction += Vector3.right;

            if (pos.y <= margin) direction += Vector3.back;
            else if (pos.y >= Screen.height - margin) direction += Vector3.forward;

            return direction;
        }

        /// <summary>
        /// Adjusts the camera's distance from <see cref="_focusPoint"/> from scroll-wheel
        /// input, clamped to the configured zoom range (issue #80 — replaces the old
        /// orthographic-size zoom with a real dolly toward/away from the focus point).
        /// </summary>
        /// <param name="mouse">The current mouse device.</param>
        private void ApplyZoom(Mouse mouse)
        {
            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) < 0.01f) return;

            float delta = Mathf.Sign(scrollY) * _config.zoomSpeed;
            _distance = Mathf.Clamp(_distance - delta, _config.zoomDistanceMin, _config.zoomDistanceMax);
            UpdateCameraTransform();
        }

        /// <summary>
        /// Sets the camera's actual world rotation and position from <see cref="_focusPoint"/>,
        /// the fixed configured pitch/yaw, and the current zoom <see cref="_distance"/> — the
        /// single place camera transform math happens (issue #80), called after every pan, zoom,
        /// jump, or saved-location change instead of any of those setting the transform directly.
        /// </summary>
        private void UpdateCameraTransform()
        {
            Quaternion rotation = Quaternion.Euler(_config.cameraPitchDegrees, _config.cameraYawDegrees, 0f);
            _camera.transform.rotation = rotation;
            _camera.transform.position = _focusPoint - rotation * Vector3.forward * _distance;
        }

        /// <summary>
        /// Saves (Ctrl+F1-F4) or jumps to (F1-F4) one of 4 bookmarked camera views. A jump
        /// is a no-op if that slot has never been saved to.
        /// </summary>
        /// <param name="keyboard">The current keyboard device.</param>
        private void HandleSavedLocations(Keyboard keyboard)
        {
            bool ctrlHeld = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;

            for (int i = 0; i < SavedLocationCount; i++)
            {
                var key = keyboard[(Key)((int)Key.F1 + i)];
                if (!key.wasPressedThisFrame) continue;

                if (ctrlHeld)
                {
                    _savedLocations[i] = new CameraSnapshot(_focusPoint, _distance);
                }
                else if (_savedLocations[i].HasValue)
                {
                    var snapshot = _savedLocations[i].Value;
                    _focusPoint = snapshot.FocusPoint;
                    _distance = snapshot.Distance;
                    UpdateCameraTransform();
                }
            }
        }

        /// <summary>
        /// Snaps the camera's focus point to an arbitrary world position immediately, keeping
        /// its current zoom distance. Distinct from the F1-F4 saved-location jump, which also
        /// restores zoom. Used by the minimap's click-to-jump behavior (issue #12).
        /// </summary>
        /// <param name="worldPosition">The world position to center the camera on.</param>
        public void JumpToPosition(Vector3 worldPosition)
        {
            if (_camera == null) return;

            _focusPoint = ClampToBounds(new Vector3(worldPosition.x, 0f, worldPosition.z));
            UpdateCameraTransform();
        }

        /// <summary>
        /// Clamps a position's X/Z to the configured world-space bounds, leaving Y untouched.
        /// </summary>
        /// <param name="position">The position to clamp.</param>
        private Vector3 ClampToBounds(Vector3 position)
        {
            float x = Mathf.Clamp(position.x, _config.boundsMin.x, _config.boundsMax.x);
            float z = Mathf.Clamp(position.z, _config.boundsMin.y, _config.boundsMax.y);
            return new Vector3(x, position.y, z);
        }

        /// <summary>
        /// A saved camera bookmark's focus point and zoom distance.
        /// </summary>
        private readonly struct CameraSnapshot
        {
            public readonly Vector3 FocusPoint;
            public readonly float Distance;

            public CameraSnapshot(Vector3 focusPoint, float distance)
            {
                FocusPoint = focusPoint;
                Distance = distance;
            }
        }
    }
}
