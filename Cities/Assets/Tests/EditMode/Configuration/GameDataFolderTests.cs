using System.IO;
using Cities.Core.Configuration;
using NUnit.Framework;
using UnityEngine;

namespace Cities.Tests.EditMode.Configuration
{
    /// <summary>Gebruikt ScriptableObjects en JsonUtility; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class GameDataFolderTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "cities-data-" + System.Guid.NewGuid().ToString("N"));
            GameDataFolder.Reload();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }

            GameDataFolder.Reload();
        }

        [Test]
        public void FolderIsUnderThePersistentPath()
        {
            Assert.That(GameDataFolder.FolderUnder("C:/x"), Does.EndWith("Data"));
        }

        [Test]
        public void ExportWritesReadmeAndOneFilePerDefinition()
        {
            var catalog = ScriptableObject.CreateInstance<DefinitionCatalog>();
            try
            {
                var written = GameDataFolder.ExportDefaults(catalog, _folder);

                Assert.That(written, Is.EqualTo(0));
                Assert.That(File.Exists(Path.Combine(_folder, GameDataFolder.ReadmeFileName)), Is.True);
                Assert.That(Directory.Exists(Path.Combine(_folder, GameDataFolder.DefaultsFolderName)), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void ProjectCatalogLoadsThroughTheDataFolder()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<DefinitionCatalog>("Assets/Data/DefinitionCatalog.asset");

            var database = GameDataFolder.Load(catalog);

            Assert.That(database.Count, Is.GreaterThan(0));
            Assert.That(GameDataFolder.Load(catalog), Is.SameAs(database), "Eén keer opgebouwd per catalogus");
            Assert.That(GameDataFolder.Readme(), Does.Contain("Defaults"));
        }
    }
}
