// Alleen voor de analyse in CI: de Unity Test Framework-package is daar niet beschikbaar.
// Deze typen spiegelen de publieke API die de tests gebruiken; Unity compileert dit bestand nooit.
using System;
using NUnit.Framework;

namespace UnityEngine.TestTools
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class UnityTestAttribute : TestAttribute
    {
    }

    public static class LogAssert
    {
        public static void Expect(LogType type, string message) => throw new NotSupportedException("Alleen beschikbaar in Unity.");

        public static void Expect(LogType type, System.Text.RegularExpressions.Regex message) =>
            throw new NotSupportedException("Alleen beschikbaar in Unity.");
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class UnitySetUpAttribute : NUnitAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class UnityTearDownAttribute : NUnitAttribute
    {
    }
}

namespace UnityEngine.TestTools.Constraints
{
    using NUnit.Framework.Constraints;

    /// <summary>Spiegel van Unity's <c>Is</c> met de allocatie-constraint; in CI draaien deze tests niet.</summary>
    public abstract class Is : NUnit.Framework.Is
    {
        public static Constraint AllocatingGCMemory() => throw new NotSupportedException("Alleen beschikbaar in Unity.");
    }

    public static class ConstraintExtensions
    {
        public static Constraint AllocatingGCMemory(this ConstraintExpression chain) =>
            throw new NotSupportedException("Alleen beschikbaar in Unity.");
    }
}
