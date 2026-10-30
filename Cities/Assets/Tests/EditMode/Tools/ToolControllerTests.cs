using System.Collections.Generic;
using Cities.Gameplay.Tools;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Tests.EditMode.Tools
{
    public class ToolControllerTests
    {
        private sealed class FakeTool : ITool
        {
            public readonly List<string> Calls = new List<string>();

            public ToolPreview Next { get; set; } = new ToolPreview(float3.zero, 0f, new float2(8f, 8f));

            public bool HasStep { get; set; }

            public string Name => "Test";

            public GameObject Ghost => null;

            public bool RepeatsWhileHeld => false;

            public void Activate() => Calls.Add("activate");

            public void Deactivate() => Calls.Add("deactivate");

            public ToolPreview UpdatePreview(in ToolInput input) => Next;

            public void Apply(in ToolPreview preview) => Calls.Add("apply");

            public bool CancelStep()
            {
                var had = HasStep;
                HasStep = false;
                return had;
            }
        }

        [Test]
        public void SelectingSwitchesAndNotifies()
        {
            var controller = new ToolController();
            var first = new FakeTool();
            var second = new FakeTool();
            var changes = new List<ITool>();
            controller.ActiveChanged += changes.Add;

            controller.Select(first);
            controller.Select(first);
            controller.Select(second);

            Assert.That(controller.Active, Is.SameAs(second));
            Assert.That(first.Calls, Is.EqualTo(new[] { "activate", "deactivate" }));
            Assert.That(second.Calls, Is.EqualTo(new[] { "activate" }));
            Assert.That(changes, Is.EqualTo(new ITool[] { first, second }));
        }

        [Test]
        public void ApplyOnlyRunsForAValidPreview()
        {
            var controller = new ToolController();
            var tool = new FakeTool { Next = new ToolPreview(float3.zero, 0f, float2.zero, "Bezet") };

            Assert.That(controller.Apply(), Is.False, "zonder tool");
            controller.Select(tool);
            Assert.That(controller.Apply(), Is.False, "voor de eerste preview");
            controller.Update(default);
            Assert.That(controller.Apply(), Is.False, "ongeldige preview");

            tool.Next = new ToolPreview(float3.zero, 0f, float2.zero);
            controller.Update(default);
            Assert.That(controller.Apply(), Is.True);
            Assert.That(tool.Calls, Has.Exactly(1).EqualTo("apply"));
        }

        [Test]
        public void CancelFirstAbortsTheStepThenTheTool()
        {
            var controller = new ToolController();
            var tool = new FakeTool { HasStep = true };
            Assert.That(controller.Cancel(), Is.False, "geen tool: de toets gaat naar het menu");

            controller.Select(tool);
            Assert.That(controller.Cancel(), Is.True);
            Assert.That(controller.Active, Is.SameAs(tool));

            Assert.That(controller.Cancel(), Is.True);
            Assert.That(controller.Active, Is.Null);
            Assert.That(controller.Preview.IsVisible, Is.False);
        }

        [Test]
        public void UpdateWithoutToolHidesThePreview()
        {
            var controller = new ToolController();
            controller.Update(default);

            Assert.That(controller.Preview.IsVisible, Is.False);
            Assert.That(controller.Preview.IsValid, Is.False);
        }

        [Test]
        public void APreviewCarriesWhatTheActionCostsAndIsFreeByDefault()
        {
            var free = new ToolPreview(float3.zero, 0f, float2.zero);
            var paid = new ToolPreview(float3.zero, 0f, float2.zero, "Te steil", "40 m", 20_000);

            Assert.That(free.Cost, Is.EqualTo(0));
            Assert.That(paid.Cost, Is.EqualTo(20_000));
            Assert.That(paid.Info, Is.EqualTo("40 m"));
            Assert.That(paid.IsValid, Is.False, "de reden blijft los van de kosten");
        }
    }
}
