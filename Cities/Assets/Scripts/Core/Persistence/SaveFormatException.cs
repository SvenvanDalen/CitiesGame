using System;

namespace Cities.Core.Persistence
{
    /// <summary>De save is geen savebestand, is beschadigd of afgebroken, of komt uit een nieuwere versie van het spel.</summary>
    public sealed class SaveFormatException : Exception
    {
        public SaveFormatException()
        {
        }

        public SaveFormatException(string message)
            : base(message)
        {
        }

        public SaveFormatException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
