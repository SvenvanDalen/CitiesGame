using Cities.Presentation.UI;
using NUnit.Framework;

namespace Cities.Tests.EditMode.UI
{
    public class MenuModelTests
    {
        [Test]
        public void StartsAtTheMainMenuWithoutACity()
        {
            var menu = new MenuModel();

            Assert.That(menu.Current, Is.EqualTo(MenuScreen.Main));
            Assert.That(menu.CityRunning, Is.False);
            menu.Back();
            Assert.That(menu.Current, Is.EqualTo(MenuScreen.Main), "het hoofdmenu gaat niet weg");
        }

        [Test]
        public void NewCityAndLoadOpenFromTheMainMenuAndGoBack()
        {
            var menu = new MenuModel();

            Assert.That(menu.Open(MenuScreen.NewCity), Is.True);
            Assert.That(menu.Current, Is.EqualTo(MenuScreen.NewCity));
            menu.Back();
            Assert.That(menu.Open(MenuScreen.Load), Is.True);
            Assert.That(menu.Current, Is.EqualTo(MenuScreen.Load));
            Assert.That(menu.Open(MenuScreen.Load), Is.False, "al open");
            Assert.That(menu.Open(MenuScreen.None), Is.False);
        }

        [Test]
        public void SavingNeedsARunningCity()
        {
            Assert.That(new MenuModel().Open(MenuScreen.Save), Is.False);
            var running = new MenuModel(true);
            running.Escape();
            Assert.That(running.Open(MenuScreen.Save), Is.True);
        }

        [Test]
        public void EscapeTogglesThePauseMenuInARunningCity()
        {
            var menu = new MenuModel(true);
            var changes = 0;
            menu.Changed += () => changes++;

            Assert.That(menu.IsOpen, Is.False);
            menu.Escape();
            Assert.That(menu.Current, Is.EqualTo(MenuScreen.Pause));
            menu.Open(MenuScreen.Load);
            menu.Escape();
            Assert.That(menu.Current, Is.EqualTo(MenuScreen.Pause), "Esc gaat een scherm terug");
            menu.Escape();
            Assert.That(menu.IsOpen, Is.False, "de stad hervat");
            Assert.That(changes, Is.EqualTo(4));
        }

        [Test]
        public void StartingACityClosesTheMenusAndQuittingReturnsToMain()
        {
            var menu = new MenuModel();
            menu.Open(MenuScreen.NewCity);

            menu.StartCity();
            Assert.That(menu.Current, Is.EqualTo(MenuScreen.None));
            Assert.That(menu.CityRunning, Is.True);
            menu.Escape();
            menu.QuitToMain();
            Assert.That(menu.Current, Is.EqualTo(MenuScreen.Main));
            Assert.That(menu.CityRunning, Is.False);
            menu.Escape();
            Assert.That(menu.Current, Is.EqualTo(MenuScreen.Main), "zonder stad opent Esc geen pauzemenu");
        }
    }
}
