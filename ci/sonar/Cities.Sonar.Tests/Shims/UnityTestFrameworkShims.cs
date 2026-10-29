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

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class UnitySetUpAttribute : NUnitAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class UnityTearDownAttribute : NUnitAttribute
    {
    }
}
