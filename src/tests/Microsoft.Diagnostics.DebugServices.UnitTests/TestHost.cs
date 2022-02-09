using Microsoft.Diagnostics.TestHelpers;
using System.IO;

namespace Microsoft.Diagnostics.DebugServices.UnitTests
{
    public abstract class TestHost
    {
        private TestDataReader _testData;
        private ITarget _target;

        public TestHost(string dumpFile, string testDataFile)
        {
            DumpFile = dumpFile;
            TestDataFile = testDataFile;
        }

        public static TestHost CreateHost(TestConfiguration config)
        {
            if (config.IsTestDbgEng())
            {
                return new TestDbgEng(config);
            }
            else
            {
                return new TestDump(config.DumpFile(), config.TestDataFile());
            }
        }

        public TestDataReader TestData
        {
            get
            {
                _testData ??= new TestDataReader(TestDataFile);
                return _testData;
            }
        }

        public ITarget Target
        {
            get
            {
                _target ??= GetTarget();
                return _target;
            }
        }

        protected abstract ITarget GetTarget();

        public string DumpFile { get; }

        public string TestDataFile { get; }

        public override string ToString() => DumpFile;
    }

    public static class TestHostExtensions
    {
        public static bool IsTestDbgEng(this TestConfiguration config) => config.AllSettings.TryGetValue("TestDbgEng", out string value) && value == "true";

        public static string DumpFile(this TestConfiguration config) => TestConfiguration.MakeCanonicalPath(config.GetValue("DumpFile"));

        public static string TestDataFile(this TestConfiguration config) => TestConfiguration.MakeCanonicalPath(config.GetValue("TestDataFile"));
    }
}
