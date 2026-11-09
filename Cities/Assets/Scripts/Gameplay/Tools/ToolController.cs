using System;
using Cities.Simulation.Commands;

namespace Cities.Gameplay.Tools
{
    /// <summary>Houdt de actieve tool bij, met zijn preview, en beslist wat klikken en annuleren doen.</summary>
    public sealed class ToolController
    {
        public ITool Active { get; private set; }

        /// <summary>Wat de speler ongedaan kan maken (E27-04); tools die <see cref="IUndoSource"/> zijn vullen dit na het uitvoeren.</summary>
        public UndoHistory History { get; } = new UndoHistory();

        public ToolPreview Preview { get; private set; }

        public event Action<ITool> ActiveChanged;

        /// <summary>Optioneel (E26-06): of het saldo de kosten toelaat; zonder blijft alles gratis.</summary>
        public Func<long, bool> CanAfford { get; set; }

        /// <summary>Optioneel (E26-06): boekt de kosten; <c>false</c> als het niet kan.</summary>
        public Func<long, bool> TryPay { get; set; }

        /// <summary>Optioneel: boekt kosten terug als een betaalde actie ongedaan wordt gemaakt.</summary>
        public Action<long> Refund { get; set; }

        public const string NotEnoughMoney = "Te weinig geld";

        /// <summary>Maakt <paramref name="tool"/> actief; <c>null</c> zet de tools uit.</summary>
        public void Select(ITool tool)
        {
            if (ReferenceEquals(tool, Active))
            {
                return;
            }

            Active?.Deactivate();
            Active = tool;
            Preview = ToolPreview.Hidden;
            Active?.Activate();
            ActiveChanged?.Invoke(Active);
        }

        public void Deselect() => Select(null);

        public void Update(in ToolInput input)
        {
            Preview = Active?.UpdatePreview(input) ?? ToolPreview.Hidden;
            if (Preview.IsValid && Preview.Cost > 0 && CanAfford != null && !CanAfford(Preview.Cost))
            {
                Preview = Preview.WithReason(NotEnoughMoney);
            }
        }

        /// <summary>Voert de preview uit als die geldig is.</summary>
        public bool Apply()
        {
            if (Active == null || !Preview.IsValid)
            {
                return false;
            }

            if (Preview.Cost > 0 && TryPay != null && !TryPay(Preview.Cost))
            {
                return false;
            }

            var cost = Preview.Cost;
            Active.Apply(Preview);
            if (Active is IUndoSource source)
            {
                var action = source.TakeUndo();
                if (action != null)
                {
                    History.Record(cost > 0 ? WithCost(action, cost) : action);
                }
            }

            return true;
        }

        /// <summary>Undo geeft de kosten terug; redo rekent ze opnieuw af en doet niets als het geld er niet is.</summary>
        private UndoAction WithCost(UndoAction action, long cost) => new UndoAction(
            action.Description,
            cost,
            () =>
            {
                action.Undo();
                Refund?.Invoke(cost);
            },
            () =>
            {
                if (TryPay == null || TryPay(cost))
                {
                    action.Redo();
                }
            });

        /// <summary>
        /// Escape: eerst een lopende stap afbreken, anders de tool uitzetten. <c>false</c> als er geen tool actief
        /// was, zodat de toets dan het menu opent.
        /// </summary>
        public bool Cancel()
        {
            if (Active == null)
            {
                return false;
            }

            if (!Active.CancelStep())
            {
                Deselect();
            }

            return true;
        }
    }
}
