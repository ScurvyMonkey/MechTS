using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MechTS.UI
{
    /// <summary>
    /// Builds the Canvas, EventSystem, and top-level HUD hierarchy at runtime and owns
    /// them for the life of the game, matching the project's convention of code-created
    /// scene objects rather than hand-authored prefabs. Created by <see cref="MechTS.Core.Bootstrapper"/>.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        /// <summary>The HUD's root Canvas, parented under this manager.</summary>
        public Canvas HUDCanvas { get; private set; }

        /// <summary>
        /// Builds the EventSystem, Canvas, and HUD hierarchy.
        /// </summary>
        private void Awake()
        {
            BuildEventSystem();
            BuildCanvas();
            BuildHud();
            BuildMissionEndScreen();
            BuildMainMenu();
        }

        /// <summary>
        /// Creates the scene's EventSystem using <see cref="InputSystemUIInputModule"/> —
        /// required because this project's Input System settings (activeInputHandler) disable
        /// the legacy Input Manager that the default StandaloneInputModule depends on, so UGUI
        /// would otherwise never receive clicks. <see cref="InputSystemUIInputModule.AssignDefaultActions"/>
        /// must be called explicitly here: it normally runs from the component's Editor-only
        /// Reset() callback when added via the Inspector, which never fires for a component
        /// added at runtime via AddComponent — without it, the module's Point/Click/etc. actions
        /// stay unbound and every UGUI button in the game is permanently unclickable.
        /// </summary>
        private void BuildEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.transform.SetParent(transform, false);

            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        /// <summary>
        /// Creates the Screen Space Overlay canvas that hosts all HUD/menu content.
        /// </summary>
        private void BuildCanvas()
        {
            var go = new GameObject("HUDCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            HUDCanvas = go.AddComponent<Canvas>();
            HUDCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            go.AddComponent<GraphicRaycaster>();
        }

        /// <summary>
        /// Creates the HUD root panel and attaches <see cref="HUDController"/>, which builds
        /// the resource, selection, build, and production panels beneath it.
        /// </summary>
        private void BuildHud()
        {
            var go = new GameObject("HUD", typeof(RectTransform));
            go.transform.SetParent(HUDCanvas.transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            go.AddComponent<HUDController>();
        }

        /// <summary>
        /// Creates the Mission End screen, stretched to fill the canvas so its own
        /// content can center itself.
        /// </summary>
        private void BuildMissionEndScreen()
        {
            var go = new GameObject("MissionEndScreen", typeof(RectTransform));
            go.transform.SetParent(HUDCanvas.transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            go.AddComponent<MissionEndScreen>();
        }

        /// <summary>
        /// Creates the Main Menu, stretched to fill the canvas so its own content can center itself.
        /// </summary>
        private void BuildMainMenu()
        {
            var go = new GameObject("MainMenu", typeof(RectTransform));
            go.transform.SetParent(HUDCanvas.transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            go.AddComponent<MainMenu>();
        }
    }
}
