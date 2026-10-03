namespace StopWatchTest
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Moq;
    using NUnit.Framework;
    using StopWatch.Plugin;
    using StopWatch.Plugins;


    /// <summary>
    /// Loads the real sample plugins with the real loader. They are loaded
    /// once for the whole fixture: the default load context cannot hold two
    /// assemblies of the same name, so each sample can only be loaded once
    /// per process.
    /// </summary>
    [TestFixture]
    public class PluginSamplesIntegrationTest
    {
        private string root;
        private IReadOnlyList<PluginInfo> loaded;
        private List<string> logged;

        [OneTimeSetUp]
        public void LoadSamples()
        {
            root = Path.Combine(Path.GetTempPath(), "StopWatchSamples-" + Guid.NewGuid().ToString("N"));
            logged = new List<string>();

            CopyDirectory(SampleOutput("HelloWorld"), Path.Combine(root, "HelloWorld"));
            CopyDirectory(SampleOutput("HelloSqlite"), Path.Combine(root, "HelloSqlite"));

            // A plugin that is broken on purpose, sorted between the two.
            string broken = Path.Combine(root, "Broken");
            Directory.CreateDirectory(broken);
            File.WriteAllText(Path.Combine(broken, "plugin.json"), "{ \"id\": \"Broken\", \"name\": \"Broken\", \"version\": \"1.0.0\", \"contractVersion\": \"1.0\" }");
            File.WriteAllText(Path.Combine(broken, "Broken.dll"), "not an assembly");

            var logger = new Mock<IPluginLogger>();
            var host = new Mock<IPluginHost>();
            host.SetupGet(h => h.Logger).Returns(logger.Object);

            var loader = new PluginLoader(new[] { root }, PluginContract.ContractVersion, id => host.Object, (m, e) => logged.Add(m));
            loaded = loader.LoadAll();
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
            catch (IOException)
            {
                // Native libraries stay mapped until the process exits.
            }
        }


        private PluginInfo Plugin(string id)
        {
            return loaded.Single(p => p.Id == id);
        }


        [Test]
        public void LoadsInIdOrder()
        {
            Assert.That(loaded.Select(p => p.Id), Is.EqualTo(new[] { "Broken", "HelloSqlite", "HelloWorld" }));
        }


        [Test]
        public void HelloWorld_LoadsAndOffersItsCommand()
        {
            PluginInfo info = Plugin("HelloWorld");

            Assert.That(info.IsLoaded, Is.True, info.Reason);
            Assert.That(info.Commands.Select(c => c.Command.Id), Is.EqualTo(new[] { "say-hello" }));
        }


        [Test]
        public void HelloSqlite_LoadsManagedAndNativeDependencies()
        {
            // Initialize runs a query, which forces the native e_sqlite3.dll to load.
            PluginInfo info = Plugin("HelloSqlite");

            Assert.That(info.IsLoaded, Is.True, info.Reason);
            Assert.That(info.Commands.Select(c => c.Command.Id), Is.EqualTo(new[] { "run-query" }));
        }


        [Test]
        public void BrokenPlugin_IsNotLoaded_AndDoesNotStopTheOthers()
        {
            PluginInfo broken = Plugin("Broken");

            Assert.That(broken.IsLoaded, Is.False);
            Assert.That(broken.Commands, Is.Empty);
            Assert.That(broken.Reason, Is.Not.Empty);
            Assert.That(Plugin("HelloWorld").IsLoaded, Is.True);
            Assert.That(Plugin("HelloSqlite").IsLoaded, Is.True);
        }


        /// <summary>
        /// The sample's build output, found from the test's own
        /// (…\StopWatchTest\bin\&lt;Config&gt;\&lt;tfm&gt;\) location.
        /// </summary>
        private static string SampleOutput(string sample)
        {
            var tfmDir = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            string tfm = tfmDir.Name;
            string config = tfmDir.Parent.Name;
            string sourceDir = tfmDir.Parent.Parent.Parent.Parent.FullName;

            string path = Path.Combine(sourceDir, "Samples", sample, "bin", config, tfm);
            Assert.That(Directory.Exists(path), Is.True, "Sample output not built: " + path);
            return path;
        }


        private static void CopyDirectory(string from, string to)
        {
            Directory.CreateDirectory(to);

            foreach (string file in Directory.GetFiles(from))
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);

            foreach (string dir in Directory.GetDirectories(from))
                CopyDirectory(dir, Path.Combine(to, Path.GetFileName(dir)));
        }
    }
}
