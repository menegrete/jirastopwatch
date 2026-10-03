namespace StopWatchTest
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Moq;
    using NUnit.Framework;
    using StopWatch;
    using StopWatch.Plugin;
    using StopWatch.Plugins;


    [TestFixture]
    public class PluginLoaderTest
    {
        private string root;
        private List<string> logged;

        [SetUp]
        public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "StopWatchPluginTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            logged = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }


        private PluginLoader NewLoader(string pluginsRoot = null)
        {
            return new PluginLoader(
                new[] { pluginsRoot ?? root },
                new Version(1, 0),
                id => new Mock<IPluginHost>().Object,
                (message, ex) => logged.Add(message));
        }

        private string MakePluginFolder(string id, string manifest)
        {
            string folder = Path.Combine(root, id);
            Directory.CreateDirectory(folder);
            if (manifest != null)
                File.WriteAllText(Path.Combine(folder, "plugin.json"), manifest);
            return folder;
        }

        private static string Manifest(string id, string contract = "1.0")
        {
            return "{ \"id\": \"" + id + "\", \"name\": \"" + id + "\", \"version\": \"1.0.0\", \"contractVersion\": \"" + contract + "\" }";
        }


        #region manifest
        [Test]
        public void Manifest_ParsesRequiredFields()
        {
            PluginManifest m = PluginManifest.Parse(Manifest("Foo", "1.2"));

            Assert.That(m.Id, Is.EqualTo("Foo"));
            Assert.That(m.Name, Is.EqualTo("Foo"));
            Assert.That(m.Version, Is.EqualTo("1.0.0"));
            Assert.That(m.ContractVersion, Is.EqualTo(new Version(1, 2)));
        }


        [Test]
        public void Manifest_AcceptsMajorOnlyContractVersion()
        {
            Assert.That(PluginManifest.Parse(Manifest("Foo", "1")).ContractVersion, Is.EqualTo(new Version(1, 0)));
        }


        [TestCase("not json")]
        [TestCase("[]")]
        [TestCase("{ \"id\": \"a\", \"name\": \"a\", \"version\": \"1\" }")]
        [TestCase("{ \"id\": \"a\", \"name\": \"a\", \"version\": \"1\", \"contractVersion\": \"x.y\" }")]
        [TestCase("{ \"id\": \"\", \"name\": \"a\", \"version\": \"1\", \"contractVersion\": \"1.0\" }")]
        public void Manifest_RejectsMalformedOrIncomplete(string json)
        {
            Assert.Throws<PluginLoadException>(() => PluginManifest.Parse(json));
        }
        #endregion


        #region contract version
        [TestCase("1.0", "1.0", true)]
        [TestCase("1.2", "1.1", true)]
        [TestCase("1.0", "1.1", false)]
        [TestCase("1.0", "2.0", false)]
        [TestCase("2.0", "1.0", false)]
        public void IsCompatible_SameMajorAndMinorNotNewer(string host, string plugin, bool expected)
        {
            Assert.That(PluginContract.IsCompatible(Version.Parse(host), Version.Parse(plugin)), Is.EqualTo(expected));
        }
        #endregion


        #region loading
        [Test]
        public void LoadAll_WithoutPluginsFolder_ReturnsNothingAndLogsNothing()
        {
            var result = NewLoader(Path.Combine(root, "does-not-exist")).LoadAll();

            Assert.That(result, Is.Empty);
            Assert.That(logged, Is.Empty);
        }


        [Test]
        public void LoadAll_MissingManifest_IsNotLoadedWithReason()
        {
            MakePluginFolder("NoManifest", null);

            PluginInfo info = NewLoader().LoadAll().Single();

            Assert.That(info.IsLoaded, Is.False);
            Assert.That(info.Reason, Does.Contain("plugin.json"));
        }


        [Test]
        public void LoadAll_IncompatibleContract_IsRejectedWithBothVersions()
        {
            MakePluginFolder("Future", Manifest("Future", "2.0"));

            PluginInfo info = NewLoader().LoadAll().Single();

            Assert.That(info.IsLoaded, Is.False);
            Assert.That(info.Reason, Does.Contain("2.0").And.Contain("1.0"));
        }


        [Test]
        public void LoadAll_ManifestIdMustMatchFolder()
        {
            MakePluginFolder("Folder", Manifest("Other"));

            PluginInfo info = NewLoader().LoadAll().Single();

            Assert.That(info.IsLoaded, Is.False);
            Assert.That(info.Reason, Does.Contain("Other").And.Contain("Folder"));
        }


        [Test]
        public void LoadAll_OrdersByIdOrdinalIgnoringCase()
        {
            MakePluginFolder("beta", null);
            MakePluginFolder("Alpha", null);
            MakePluginFolder("charlie", null);

            var ids = NewLoader().LoadAll().Select(p => p.Id).ToList();

            Assert.That(ids, Is.EqualTo(new[] { "Alpha", "beta", "charlie" }));
        }


        [Test]
        public void LoadAll_SameIdInTwoRoots_FirstRootWinsAndSecondIsReportedAsDuplicate()
        {
            string second = Path.Combine(root, "second");
            string first = Path.Combine(root, "first");
            Directory.CreateDirectory(Path.Combine(first, "Dup"));
            Directory.CreateDirectory(Path.Combine(second, "Dup"));
            Directory.CreateDirectory(Path.Combine(second, "Only"));

            var loader = new PluginLoader(new[] { first, second, Path.Combine(root, "missing") }, new Version(1, 0), id => new Mock<IPluginHost>().Object, (m, e) => logged.Add(m));
            IReadOnlyList<PluginInfo> result = loader.LoadAll();

            Assert.That(result.Select(p => p.Id), Is.EqualTo(new[] { "Dup", "Dup", "Only" }));
            Assert.That(result[0].Reason, Does.Contain("plugin.json"));
            Assert.That(result[1].Reason, Does.Contain("Duplicate").And.Contain("second"));
        }


        [Test]
        public void LoadAll_CorruptAssembly_IsContainedAndOthersStillProcessed()
        {
            string broken = MakePluginFolder("Broken", Manifest("Broken"));
            File.WriteAllText(Path.Combine(broken, "Broken.dll"), "this is not an assembly");
            MakePluginFolder("Later", null);

            IReadOnlyList<PluginInfo> result = NewLoader().LoadAll();

            Assert.That(result.Select(p => p.Id), Is.EqualTo(new[] { "Broken", "Later" }));
            Assert.That(result[0].IsLoaded, Is.False);
            Assert.That(result[0].Commands, Is.Empty);
            Assert.That(result[0].Reason, Is.Not.Empty);
            Assert.That(logged, Has.Some.Contain("Broken"));
        }
        #endregion


        #region commands
        [Test]
        public void CommandItem_RunContainsExceptionAndReportsIt()
        {
            var plugin = new PluginInfo("P") { Name = "P" };
            var command = new PluginCommand("boom", "Boom", () => throw new InvalidOperationException("kaput"));
            var item = new PluginCommandItem(plugin, command, (m, e) => logged.Add(m));

            string error = item.Run();

            Assert.That(error, Does.Contain("kaput"));
            Assert.That(logged, Has.Some.Contain("boom"));
        }


        [Test]
        public void CommandItem_RunReturnsNullOnSuccess()
        {
            bool ran = false;
            var item = new PluginCommandItem(new PluginInfo("P"), new PluginCommand("ok", "Ok", () => ran = true), (m, e) => { });

            Assert.That(item.Run(), Is.Null);
            Assert.That(ran, Is.True);
        }
        #endregion


        #region issue list facade
        [Test]
        public void IssueList_RaisesAddedAndRemoved_AndIsolatesThrowingSubscribers()
        {
            var settings = new Settings();
            settings.MaxIssues = 10;
            var issues = new IssueListViewModel(settings);
            var activeTimer = new ActiveTimerViewModel(() => issues.Issues.Cast<ITimerSource>());
            var facade = new PluginIssueList(issues, activeTimer, (m, e) => logged.Add(m));

            var added = new List<PluginIssue>();
            var removed = new List<PluginIssue>();
            facade.IssueAdded += (s, e) => throw new InvalidOperationException("bad subscriber");
            facade.IssueAdded += (s, e) => added.Add(e.Issue);
            facade.IssueRemoved += (s, e) => removed.Add(e.Issue);

            IssueViewModel first = issues.Add();
            first.IssueKey = "AB-1";
            IssueViewModel second = issues.Add();
            second.IssueKey = "AB-2";
            issues.Remove(second);

            Assert.That(added, Has.Count.EqualTo(2));
            Assert.That(removed.Select(i => i.Key), Is.EqualTo(new[] { "AB-2" }));
            Assert.That(logged, Has.Some.Contain("IssueAdded"));
            Assert.That(facade.GetIssues().Select(i => i.Key), Is.EqualTo(new[] { "AB-1" }));
        }
        #endregion
    }
}
