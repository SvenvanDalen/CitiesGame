using Cities.Gameplay.Events;
using Cities.Gameplay.Statistics;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cities.Presentation.UI
{
    /// <summary>
    /// Meldingen (E26-12): de laatste meldingen rechtsboven, rampen in het rood. Ververst een paar keer per seconde, alleen bij verandering.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class NotificationsBehaviour : MonoBehaviour
    {
        private const int Shown = 4;

        [SerializeField] private EventsHostBehaviour _host;

        private VisualElement _list;
        private int _lastCount = -1;
        private float _nextCheck;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddToClassList(UiClasses.Root);
            _list = new VisualElement { name = "notifications", pickingMode = PickingMode.Ignore };
            _list.style.position = Position.Absolute;
            _list.style.right = 8;
            _list.style.top = 56;
            _list.style.width = 320;
            root.Add(_list);
            _lastCount = -1;
        }

        private void OnDisable() => _list?.RemoveFromHierarchy();

        private void Update()
        {
            if (Time.unscaledTime < _nextCheck || _host == null || _host.Events == null)
            {
                return;
            }

            _nextCheck = Time.unscaledTime + 0.5f;
            var count = _host.Events.Notifications.Items.Count;
            if (count == _lastCount)
            {
                return;
            }

            _lastCount = count;
            _list.Clear();
            foreach (var item in _host.Events.Latest(Shown))
            {
                var label = new Label(item.Text);
                label.style.color = item.Severity == Severity.Critical ? new Color(1f, 0.45f, 0.4f) : Color.white;
                label.style.marginBottom = 2;
                if (item.Location.HasValue)
                {
                    var location = item.Location.Value;
                    label.pickingMode = PickingMode.Position;
                    label.tooltip = "Klik om naar de plek te gaan";
                    label.RegisterCallback<ClickEvent>(_ => FindAnyObjectByType<Cities.Gameplay.Cameras.CityCameraBehaviour>()?.FocusOn(location));
                }

                _list.Add(label);
            }
        }
    }
}
