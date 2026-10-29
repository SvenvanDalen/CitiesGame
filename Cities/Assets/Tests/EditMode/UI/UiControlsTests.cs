using System.IO;
using Cities.Presentation.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Cities.Tests.EditMode.UI
{
    /// <summary>Maakt UI Toolkit-elementen en laadt assets; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class UiControlsTests
    {
        [Test]
        public void ButtonSwitchesVariantClasses()
        {
            var button = new CityButton("Bouwen", ButtonVariant.Primary);

            Assert.That(button.ClassListContains(UiClasses.Button), Is.True);
            Assert.That(button.ClassListContains(UiClasses.ButtonPrimary), Is.True);

            button.Variant = ButtonVariant.Danger;
            button.Small = true;
            button.Selected = true;

            Assert.That(button.ClassListContains(UiClasses.ButtonPrimary), Is.False);
            Assert.That(button.ClassListContains(UiClasses.ButtonDanger), Is.True);
            Assert.That(button.ClassListContains(UiClasses.ButtonSmall), Is.True);
            Assert.That(button.ClassListContains(UiClasses.ButtonSelected), Is.True);

            button.Variant = ButtonVariant.Secondary;
            Assert.That(button.ClassListContains(UiClasses.ButtonDanger), Is.False);
            Assert.That(button.text, Is.EqualTo("Bouwen"));
        }

        [Test]
        public void PanelPutsChildrenInBody()
        {
            var panel = new CityPanel("Budget");
            var content = new Label("Inkomsten");

            panel.Add(content);

            Assert.That(panel.Title, Is.EqualTo("Budget"));
            Assert.That(content.hierarchy.parent.name, Is.EqualTo("body"), "Fysiek in de inhoud");
            Assert.That(content.parent, Is.SameAs(panel), "Logisch kind van het paneel");
            Assert.That(panel.Closable, Is.True);
            panel.Closable = false;
            Assert.That(panel.Closable, Is.False);
        }

        [Test]
        public void FooterAppearsWithFirstButton()
        {
            var panel = new CityPanel("Slopen?");
            var clicked = false;

            Assert.That(panel.Footer.style.display.value, Is.EqualTo(DisplayStyle.None));
            var button = panel.AddFooterButton("Slopen", ButtonVariant.Danger, () => clicked = true);
            panel.AddFooterButton("Annuleren", ButtonVariant.Secondary, null);

            Assert.That(panel.Footer.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(panel.Footer.childCount, Is.EqualTo(2));
            Assert.That(button.ClassListContains(UiClasses.ButtonDanger), Is.True);
            Assert.That(clicked, Is.False);
        }

        [Test]
        public void MeterSetsFillWidthAndColor()
        {
            var meter = new CityMeter { Value = 0.25f, Color = MeterColor.Industrial };

            Assert.That(meter.Q("fill").style.width.value.value, Is.EqualTo(25f).Within(1e-4f));
            Assert.That(meter.ClassListContains("c-meter--industrial"), Is.True);

            meter.Color = MeterColor.Accent;
            meter.Value = 7f;
            Assert.That(meter.ClassListContains("c-meter--industrial"), Is.False);
            Assert.That(meter.Value, Is.EqualTo(1f));
        }

        [Test]
        public void ThemeAndFontsExist()
        {
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI/Styles/CityTheme.tss"), Is.Not.Null);
            Assert.That(UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/CityPanelSettings.asset").themeStyleSheet, Is.Not.Null);
            foreach (var weight in new[] { "Regular", "Medium", "SemiBold", "Bold" })
            {
                Assert.That(File.Exists($"Assets/UI/Fonts/Inter/Inter-{weight}.asset"), Is.True, weight);
            }
        }

        [Test]
        public void ShowcaseBuildsWithoutErrors()
        {
            var tree = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Templates/Showcase.uxml");

            var root = tree.Instantiate();

            Assert.That(root.Query<CityPanel>().ToList().Count, Is.EqualTo(3));
            Assert.That(root.Query<CityMeter>().ToList().Count, Is.EqualTo(4));
            Assert.That(root.Q<CityMeter>().Color, Is.EqualTo(MeterColor.Residential));
        }
    }
}
