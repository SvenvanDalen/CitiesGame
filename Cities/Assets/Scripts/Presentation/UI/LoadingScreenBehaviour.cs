using Cities.Core.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cities.Presentation.UI
{
    /// <summary>
    /// Laadscherm met een voortgangsbalk (E26-01): dekt het scherm af tot de stappen van <see cref="StartupProgress"/> klaar zijn. De generatoren
    /// melden hun stap; de laatste twee stappen worden afgerond zodra de eerste frames gerenderd zijn.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(-2000)]
    public sealed class LoadingScreenBehaviour : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _root;
        private CityMeter _bar;
        private Label _label;
        private StartupProgress _progress;
        private int _frames;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            _document.sortingOrder = 1000;
            _progress = StartupProgress.ForMainScene();
            StartupProgress.Shared = _progress;
        }

        private void OnEnable()
        {
            var root = _document.rootVisualElement;
            _root = new VisualElement { name = "loading" };
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.right = 0;
            _root.style.top = 0;
            _root.style.bottom = 0;
            _root.style.backgroundColor = new Color(0.07f, 0.09f, 0.12f, 1f);
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;

            _label = new Label("Stad voorbereiden") { name = "loading-label" };
            _label.style.color = Color.white;
            _label.style.fontSize = 20;
            _label.style.marginBottom = 12;

            _bar = new CityMeter { name = "loading-bar" };
            _bar.style.width = 360;
            _bar.style.height = 10;

            _root.Add(_label);
            _root.Add(_bar);
            root.Add(_root);
        }

        private void Update()
        {
            if (_progress == null || _root == null)
            {
                return;
            }

            _frames++;
            if (_frames == 2)
            {
                _progress.Complete("Wegen en water");
            }
            else if (_frames == 4 && _progress.Current == "Stad")
            {
                _progress.Complete("Stad");
            }

            _bar.Value = _progress.Fraction;
            _label.text = _progress.IsComplete ? "Klaar" : _progress.Current + " voorbereiden";
            if (_progress.IsComplete)
            {
                _root.style.display = DisplayStyle.None;
                if (StartupProgress.Shared == _progress)
                {
                    StartupProgress.Shared = null;
                }

                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (StartupProgress.Shared == _progress)
            {
                StartupProgress.Shared = null;
            }
        }
    }
}
