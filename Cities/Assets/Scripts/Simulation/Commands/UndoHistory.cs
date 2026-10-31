using System;
using System.Collections.Generic;

namespace Cities.Simulation.Commands
{
    /// <summary>
    /// Een bouwactie die zich laat terugdraaien (E01-12). <see cref="Undo"/> en <see cref="Redo"/> zijn de omgekeerde en herhaalde
    /// uitvoering, inclusief het terugboeken respectievelijk opnieuw afschrijven van <see cref="Cost"/>.
    /// </summary>
    public sealed class UndoAction
    {
        public UndoAction(string description, long cost, Action undo, Action redo)
        {
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Undo = undo ?? throw new ArgumentNullException(nameof(undo));
            Redo = redo ?? throw new ArgumentNullException(nameof(redo));
            Cost = cost;
        }

        public string Description { get; }

        /// <summary>Wat de actie kostte; negatief als ze geld opleverde (slopen met teruggave).</summary>
        public long Cost { get; }

        public Action Undo { get; }

        public Action Redo { get; }
    }

    /// <summary>
    /// De laatste <see cref="Limit"/> bouwacties met undo en redo. Een nieuwe actie na een undo gooit de redo-keten weg; de oudste actie
    /// valt af zodra er meer dan de limiet zijn.
    /// </summary>
    public sealed class UndoHistory
    {
        public const int DefaultLimit = 20;

        private readonly LinkedList<UndoAction> _done = new LinkedList<UndoAction>();
        private readonly Stack<UndoAction> _undone = new Stack<UndoAction>();

        public UndoHistory(int limit = DefaultLimit)
        {
            if (limit < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(limit), limit, "De geschiedenis bewaart minstens één actie.");
            }

            Limit = limit;
        }

        public int Limit { get; }

        public int UndoCount => _done.Count;

        public int RedoCount => _undone.Count;

        /// <summary>De actie die <see cref="Undo"/> nu terugdraait, voor de knoptekst; null als er niets is.</summary>
        public UndoAction NextUndo => _done.Last?.Value;

        public UndoAction NextRedo => _undone.Count > 0 ? _undone.Peek() : null;

        /// <summary>Legt een zojuist uitgevoerde actie vast.</summary>
        public void Record(UndoAction action)
        {
            _done.AddLast(action ?? throw new ArgumentNullException(nameof(action)));
            _undone.Clear();
            if (_done.Count > Limit)
            {
                _done.RemoveFirst();
            }
        }

        /// <summary>Draait de laatste actie terug; geeft false als er niets terug te draaien is.</summary>
        public bool Undo()
        {
            if (_done.Last == null)
            {
                return false;
            }

            var action = _done.Last.Value;
            action.Undo();
            _done.RemoveLast();
            _undone.Push(action);
            return true;
        }

        public bool Redo()
        {
            if (_undone.Count == 0)
            {
                return false;
            }

            var action = _undone.Pop();
            action.Redo();
            _done.AddLast(action);
            return true;
        }

        public void Clear()
        {
            _done.Clear();
            _undone.Clear();
        }
    }
}
