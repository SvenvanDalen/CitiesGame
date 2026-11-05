using System.Globalization;
using Cities.Core.Configuration;
using Cities.Core.Randomness;
using Cities.Gameplay.Persistence;
using Cities.Gameplay.Tools;
using Cities.Presentation.Settings;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Cities.Presentation.UI
{
    /// <summary>
    /// Hoofdmenu en pauzemenu (E26-02) op basis van <see cref="MenuModel"/>. Esc opent het pauzemenu (pauzeert de klok) tenzij een tool actief is;
    /// Nieuwe stad en Nieuwe wereld laden de scène opnieuw met een <see cref="StartRequest"/>, Laden en Opslaan gebruiken de save-slots.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MenuBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private SaveGameBehaviour _save;
        [SerializeField] private ToolsBehaviour _tools;
        [SerializeField] private InputActionAsset _controls;

        private UIDocument _document;
        private VisualElement _root;
        private InputAction _menu;
        private TextField _seedField;
        private TextField _nameField;
        private int _difficulty = 1;
        private SimulationSpeed _speedBefore = SimulationSpeed.Normal;

        public MenuModel Model { get; private set; }

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            _document.sortingOrder = 500;
            var request = StartRequest.Pending;
            Model = new MenuModel(cityRunning: request != null && request.SkipMenu);
            Model.Changed += OnChanged;
            if (_controls != null)
            {
                _menu = _controls.FindActionMap("Game", throwIfNotFound: true).FindAction("Menu", true);
            }
        }

        private void OnEnable()
        {
            _root = new VisualElement { name = "menu" };
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.right = 0;
            _root.style.top = 0;
            _root.style.bottom = 0;
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            _document.rootVisualElement.Add(_root);
            if (_menu != null)
            {
                _menu.actionMap.Enable();
                _menu.performed += OnMenuKey;
            }
        }

        private void OnDisable()
        {
            if (_menu != null)
            {
                _menu.performed -= OnMenuKey;
            }
        }

        private void Start()
        {
            if (Model.IsOpen)
            {
                _simulation.Clock.SetSpeed(SimulationSpeed.Paused);
            }

            OnChanged();
        }

        private void OnMenuKey(InputAction.CallbackContext context)
        {
            if (_tools != null && _tools.Controller.Active != null)
            {
                return;
            }

            var wasOpen = Model.IsOpen;
            if (!wasOpen)
            {
                _speedBefore = _simulation.Clock.IsPaused ? SimulationSpeed.Normal : _simulation.Clock.Speed;
            }

            Model.Escape();
            if (!wasOpen && Model.IsOpen)
            {
                _simulation.Clock.SetSpeed(SimulationSpeed.Paused);
            }
        }

        private void OnChanged()
        {
            if (_root == null)
            {
                return;
            }

            _root.Clear();
            if (!Model.IsOpen)
            {
                _root.style.display = DisplayStyle.None;
                if (_simulation.Clock.IsPaused)
                {
                    _simulation.Clock.SetSpeed(_speedBefore);
                }

                return;
            }

            _root.style.display = DisplayStyle.Flex;
            _root.style.backgroundColor = Model.Current == MenuScreen.Main ? new Color(0.07f, 0.09f, 0.12f, 0.92f) : new Color(0f, 0f, 0f, 0.55f);
            switch (Model.Current)
            {
                case MenuScreen.Main: BuildMain(); break;
                case MenuScreen.Pause: BuildPause(); break;
                case MenuScreen.NewCity: BuildNewCity(); break;
                case MenuScreen.Load: BuildLoad(); break;
                case MenuScreen.Save: BuildSave(); break;
            }
        }

        private void BuildMain()
        {
            var panel = NewPanel("Cities");
            panel.Add(Button("Nieuwe stad", () => Model.Open(MenuScreen.NewCity), ButtonVariant.Primary));
            panel.Add(Button("Laden", () => Model.Open(MenuScreen.Load)));
            panel.Add(Button("Instellingen", OpenSettings));
            panel.Add(Button("Afsluiten", Quit));
        }

        private void BuildPause()
        {
            var panel = NewPanel("Pauze");
            panel.Add(Button("Hervatten", Model.Back, ButtonVariant.Primary));
            panel.Add(Button("Opslaan", () => Model.Open(MenuScreen.Save)));
            panel.Add(Button("Laden", () => Model.Open(MenuScreen.Load)));
            panel.Add(Button("Nieuwe wereld", () => Restart(new StartRequest { Seed = SeedHash.NewSeed(), Difficulty = _difficulty })));
            panel.Add(Button("Instellingen", OpenSettings));
            panel.Add(Button("Naar hoofdmenu", () => Restart(new StartRequest { SkipMenu = false })));
        }

        private void BuildNewCity()
        {
            var panel = NewPanel("Nieuwe stad");
            _seedField = new TextField("Seed (leeg = willekeurig)");
            panel.Add(_seedField);
            var difficulty = new RadioButtonGroup("Moeilijkheid", new System.Collections.Generic.List<string> { "Makkelijk", "Normaal", "Moeilijk" }) { value = _difficulty };
            difficulty.RegisterValueChangedCallback(change => _difficulty = Mathf.Clamp(change.newValue, 0, 2));
            panel.Add(difficulty);
            panel.Add(Button("Start", StartNewCity, ButtonVariant.Primary));
            panel.Add(Button("Terug", Model.Back));
        }

        private void BuildLoad()
        {
            var panel = NewPanel("Laden");
            var any = false;
            if (_save != null && _save.Slots != null)
            {
                foreach (var info in _save.Slots.List())
                {
                    if (!info.IsReadable)
                    {
                        continue;
                    }

                    any = true;
                    var name = info.Name;
                    var m = info.Metadata;
                    var label = string.Format(CultureInfo.InvariantCulture, "{0} ({1:0000}-{2:00}-{3:00})", name, m.Year, m.Month, m.Day);
                    panel.Add(Button(label, () => LoadSlot(name)));
                }
            }

            if (!any)
            {
                panel.Add(new Label("Geen opgeslagen steden."));
            }

            panel.Add(Button("Terug", Model.Back));
        }

        private void BuildSave()
        {
            var panel = NewPanel("Opslaan");
            _nameField = new TextField("Naam") { value = "Stad" };
            panel.Add(_nameField);
            panel.Add(Button("Opslaan", () =>
            {
                var name = string.IsNullOrWhiteSpace(_nameField.value) || SaveSlots.IsReservedName(_nameField.value.Trim()) ? "Stad" : _nameField.value.Trim();
                _save.RequestSave(SaveSlotKind.Manual, name);
                Model.Back();
            }, ButtonVariant.Primary));
            panel.Add(Button("Terug", Model.Back));
        }

        private void StartNewCity()
        {
            ulong? seed = null;
            if (_seedField != null && SimulationRandom.TryParse(_seedField.value, out var parsed))
            {
                seed = parsed;
            }

            Restart(new StartRequest { Seed = seed ?? SeedHash.NewSeed(), Difficulty = _difficulty });
        }

        private void LoadSlot(string name)
        {
            if (_save != null)
            {
                _save.LoadNamed(name);
            }

            Model.StartCity();
        }

        private static void Restart(StartRequest request)
        {
            StartRequest.RememberDifficulty(request.Difficulty);
            StartRequest.Submit(request);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private static void OpenSettings()
        {
            var settings = FindAnyObjectByType<ControlsSettingsBehaviour>();
            if (settings != null)
            {
                settings.Open();
            }
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private VisualElement NewPanel(string title)
        {
            var panel = new CityPanel(title);
            panel.style.width = 360;
            panel.CloseRequested += Model.Back;
            _root.Add(panel);
            return panel;
        }

        private static CityButton Button(string text, System.Action action, ButtonVariant variant = ButtonVariant.Secondary)
        {
            var button = new CityButton(text, variant);
            button.style.marginBottom = 6;
            button.clicked += action;
            return button;
        }
    }
}
