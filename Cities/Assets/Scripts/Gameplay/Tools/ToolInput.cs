using Cities.World.Picking;

namespace Cities.Gameplay.Tools
{
    /// <summary>Invoer van één frame voor de actieve tool, los van het invoersysteem.</summary>
    public readonly struct ToolInput
    {
        public ToolInput(PickResult pick, int rotateSteps = 0, float deltaTime = 0f)
        {
            Pick = pick;
            RotateSteps = rotateSteps;
            DeltaTime = deltaTime;
        }

        /// <summary>Wat er onder de muis ligt; zonder grond (muis boven de UI of de lucht) is er geen preview.</summary>
        public PickResult Pick { get; }

        /// <summary>Kwartslagen draaien dit frame, positief met de klok mee.</summary>
        public int RotateSteps { get; }

        /// <summary>Seconden sinds het vorige frame, voor tools die doorwerken zolang de knop ingedrukt is.</summary>
        public float DeltaTime { get; }
    }
}
