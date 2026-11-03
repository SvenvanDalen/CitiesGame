using Cities.Gameplay.Cameras;
using Cities.Gameplay.Cheats;
using Cities.Presentation.UI;
using Cities.Simulation.Scheduling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Cities.Presentation.Cheats
{
    /// <summary>
    /// De cheatconsole (E24-03), met de backtick-toets (`) aan en uit te zetten. Regels gaan naar <see cref="Console"/>;
    /// pijltje omhoog en omlaag halen eerdere regels terug en Tab vult een commandonaam aan. De klok en de camera worden
    /// aangesloten als ze in de scene staan; geldmodel, tegelontgrendeling en spawner zijn ingevuld zodra een host ze
    /// aanmaakt: zet ze op <see cref="Context"/> voordat het commando wordt gebruikt.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ConsoleBehaviour : MonoBehaviour
    {
        private const int MaxOutputLines = 200;

        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private CityCameraBehaviour _camera;
        [SerializeField] private bool _cheatsEnabled = true;

        private readonly ConsoleHistory _history = new ConsoleHistory();
        private VisualElement _panel;
        private ScrollView _output;
        private TextField _input;

        public CheatConsole Console { get; } = new CheatConsole();

        public CheatContext Context { get; } = new CheatContext();

        public bool IsOpen => _panel != null && _panel.style.display == DisplayStyle.Flex;

        public void SetOpen(bool open)
        {
            if (_panel == null)
            {
                return;
            }

            _panel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open)
            {
                _input.value = string.Empty;
                _input.schedule.Execute(() => _input.Focus());
            }
        }

        private void Awake()
        {
            Console.CheatsEnabled = _cheatsEnabled;
            StandardCheats.Register(Console, Context);
        }

        private void OnEnable()
        {
            if (_simulation != null)
            {
                Context.Clock = _simulation.Clock;
            }

            if (_camera != null && _camera.Controller != null)
            {
                Context.Teleporter = new CameraTeleporter(_camera.Controller);
            }

            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddToClassList(UiClasses.Root);
            _panel = new CityPanel("Console") { name = "cheat-console" };
            _panel.style.position = Position.Absolute;
            _panel.style.left = 16;
            _panel.style.right = 16;
            _panel.style.bottom = 16;
            _panel.style.height = 280;
            _output = new ScrollView { name = "console-output" };
            _output.style.flexGrow = 1;
            _panel.Add(_output);
            _input = new TextField { name = "console-input" };
            _input.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            _panel.Add(_input);
            root.Add(_panel);
            SetOpen(false);
        }

        private void OnDisable()
        {
            _panel?.RemoveFromHierarchy();
            _panel = null;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.backquoteKey.wasPressedThisFrame)
            {
                SetOpen(!IsOpen);
            }
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Submit();
                    break;
                case KeyCode.UpArrow:
                    _input.value = _history.Previous() ?? _input.value;
                    break;
                case KeyCode.DownArrow:
                    _input.value = _history.Next() ?? _input.value;
                    break;
                case KeyCode.Tab:
                    Complete();
                    break;
                case KeyCode.BackQuote:
                case KeyCode.Escape:
                    SetOpen(false);
                    break;
                default:
                    return;
            }

            evt.StopPropagation();
            evt.PreventDefault();
        }

        private void Submit()
        {
            var line = _input.value;
            _history.Add(line);
            _input.value = string.Empty;
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            Print("> " + line, false);
            var result = Console.Execute(line);
            if (result.Message.Length > 0)
            {
                Print(result.Message, !result.Success);
            }
        }

        private void Complete()
        {
            var matches = Console.Complete(_input.value);
            if (matches.Count == 1)
            {
                _input.value = matches[0] + " ";
                _input.SelectRange(_input.value.Length, _input.value.Length);
            }
            else if (matches.Count > 1)
            {
                Print(string.Join("  ", matches), false);
            }
        }

        private void Print(string text, bool error)
        {
            var label = new Label(text);
            label.AddToClassList(UiClasses.TextCaption);
            if (error)
            {
                label.style.color = new Color(0.95f, 0.45f, 0.4f);
            }

            _output.Add(label);
            while (_output.childCount > MaxOutputLines)
            {
                _output.RemoveAt(0);
            }

            _output.schedule.Execute(() => _output.scrollOffset = new Vector2(0f, float.MaxValue));
        }
    }
}
