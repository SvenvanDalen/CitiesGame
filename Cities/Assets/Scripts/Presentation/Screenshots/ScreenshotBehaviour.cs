using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cities.Core.Diagnostics;
using Cities.Gameplay.Cameras;
using Cities.Gameplay.Tools;
using Cities.Presentation.Tools;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Cities.Presentation.Screenshots
{
    /// <summary>
    /// Screenshot-modus (F9): verbergt de interface en de bouwghost, zet de tools uit en geeft de camera ruimere grenzen
    /// om vrij te roteren. F12 maakt een opname op minstens <c>_targetWidth</c> pixels breed in
    /// <c>persistentDataPath/Screenshots</c>; buiten de modus verbergt dat de interface alleen voor het moment van de opname.
    /// </summary>
    public sealed class ScreenshotBehaviour : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _controls;
        [SerializeField] private CityCameraBehaviour _camera;

        [Tooltip("Minimale breedte van een opname in pixels; de resolutie wordt een veelvoud van het scherm.")]
        [SerializeField] private int _targetWidth = 7680;

        private readonly SystemLog _log = new SystemLog("Screenshot");
        private readonly List<(UIDocument Document, DisplayStyle Style)> _hiddenDocuments = new List<(UIDocument, DisplayStyle)>();
        private readonly List<Behaviour> _disabled = new List<Behaviour>();
        private InputAction _toggle;
        private InputAction _capture;
        private Action<InputAction.CallbackContext> _onToggle;
        private Action<InputAction.CallbackContext> _onCapture;
        private bool _hidden;
        private bool _capturing;

        public bool IsActive { get; private set; }

        public void SetActive(bool active)
        {
            if (active == IsActive)
            {
                return;
            }

            IsActive = active;
            if (_camera != null)
            {
                _camera.FreeLook = active;
            }

            SetInterfaceHidden(active);
        }

        private void Awake()
        {
            var map = _controls.FindActionMap("Game", throwIfNotFound: true);
            _toggle = map.FindAction("ScreenshotMode", true);
            _capture = map.FindAction("Screenshot", true);
            _onToggle = _ => SetActive(!IsActive);
            _onCapture = _ => StartCoroutine(Capture());
        }

        private void OnEnable()
        {
            _toggle.performed += _onToggle;
            _capture.performed += _onCapture;
            _controls.FindActionMap("Game", true).Enable();
        }

        private void OnDisable()
        {
            _toggle.performed -= _onToggle;
            _capture.performed -= _onCapture;
            SetActive(false);
        }

        private IEnumerator Capture()
        {
            if (_capturing)
            {
                yield break;
            }

            _capturing = true;
            var wasHidden = _hidden;
            SetInterfaceHidden(true);

            // Een frame wachten: de interface moet al verdwenen zijn voordat de opname gemaakt wordt.
            yield return new WaitForEndOfFrame();
            var folder = Path.Combine(Application.persistentDataPath, "Screenshots");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, ScreenshotMode.FileName(DateTime.Now));
            var superSize = ScreenshotMode.SuperSize(Screen.width, Screen.height, _targetWidth);
            ScreenCapture.CaptureScreenshot(path, superSize);
            _log.Info("Screenshot {0} ({1})", path, $"{Screen.width * superSize}x{Screen.height * superSize}");

            // De opname wordt aan het eind van dit frame weggeschreven; pas daarna mag de interface terug.
            yield return new WaitForEndOfFrame();
            yield return null;
            SetInterfaceHidden(wasHidden);
            _capturing = false;
        }

        private void SetInterfaceHidden(bool hide)
        {
            if (hide == _hidden)
            {
                return;
            }

            _hidden = hide;
            if (hide)
            {
                foreach (var document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                {
                    var root = document.rootVisualElement;
                    if (root != null)
                    {
                        _hiddenDocuments.Add((document, root.style.display.value));
                        root.style.display = DisplayStyle.None;
                    }
                }

                DisableAll<ToolGhostBehaviour>();
                DisableAll<ToolsBehaviour>();
                return;
            }

            foreach (var (document, style) in _hiddenDocuments)
            {
                if (document != null && document.rootVisualElement != null)
                {
                    document.rootVisualElement.style.display = style;
                }
            }

            _hiddenDocuments.Clear();
            foreach (var behaviour in _disabled)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = true;
                }
            }

            _disabled.Clear();
        }

        private void DisableAll<T>() where T : Behaviour
        {
            foreach (var behaviour in FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (behaviour.enabled)
                {
                    behaviour.enabled = false;
                    _disabled.Add(behaviour);
                }
            }
        }
    }
}
