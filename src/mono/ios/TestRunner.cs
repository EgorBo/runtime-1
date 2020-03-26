using System;
using System.IO;
using System.Linq;
using Xunit;
using System.Xml.Linq;

public class TestRunner
{
    public static int Main(string[] args)
    {
        string assemblyFileName = Directory.GetFiles(".", "*.Tests.dll")[0];
        var configuration = new TestAssemblyConfiguration()
            {
                ShadowCopy = false,
                MaxParallelThreads = 1,
                ParallelizeAssembly = false,
                ParallelizeTestCollections = false,
                StopOnFail = false
            };
        var discoveryOptions = TestFrameworkOptions.ForDiscovery(configuration);
        var discoverySink = new TestDiscoverySink();
        var testOptions = TestFrameworkOptions.ForExecution(configuration);
        var testSink = new TestMessageSink();
        var controller = new XunitFrontController(AppDomainSupport.Denied, assemblyFileName, configFileName: null,
            shadowCopy: false);

        Log($"Discovering tests for {assemblyFileName}");
        controller.Find(includeSourceInformation: false, discoverySink, discoveryOptions);
        discoverySink.Finished.WaitOne();
        var testCasesToRun = discoverySink.TestCases.ToList();
        Log($"Discovery finished.");

        var summarySink = new DelegatingExecutionSummarySink(testSink, () => false,
            (completed, summary) =>
            {
                Log($"Tests run: {summary.Total}, Errors: 0, Failures: {summary.Failed}, Skipped: {summary.Skipped}{Environment.NewLine}Time: {TimeSpan.FromSeconds((double)summary.Time).TotalSeconds}s");
            });
        var resultsXmlAssembly = new XElement("assembly");
        var resultsSink = new DelegatingXmlCreationSink(summarySink, resultsXmlAssembly);
        testSink.Execution.TestPassedEvent += args =>
            {
                Log($"[PASS] {args.Message.Test.DisplayName}");
                mono_sdks_ui_increment_testcase_result(0);
            };
        testSink.Execution.TestSkippedEvent += args =>
            {
                Log($"[SKIP] {args.Message.Test.DisplayName}");
                mono_sdks_ui_increment_testcase_result(1);
            };
        testSink.Execution.TestFailedEvent += args =>
            {
                Log($"[FAIL] {args.Message.Test.DisplayName}{Environment.NewLine}{Xunit.Sdk.ExceptionUtility.CombineMessages(args.Message)}{Environment.NewLine}{Xunit.Sdk.ExceptionUtility.CombineStackTraces(args.Message)}");
                mono_sdks_ui_increment_testcase_result(2);
            };
        controller.RunTests(testCasesToRun, resultsSink, testOptions);
        resultsSink.Finished.WaitOne();

        bool failed = resultsSink.ExecutionSummary.Failed > 0 || resultsSink.ExecutionSummary.Errors > 0;
        return failed ? 1 : 0;
    }

    [System.Runtime.InteropServices.DllImport ("__Internal")]
    private extern static void mono_sdks_ui_increment_testcase_result (int resultType);

    private static void Log(string str = "") => Console.WriteLine(str);
}
