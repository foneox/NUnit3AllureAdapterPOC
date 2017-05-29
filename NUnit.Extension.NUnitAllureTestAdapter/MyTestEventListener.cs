using System;
using System.IO;
using System.Reflection;
using System.Xml;

using AllureCSharpCommons;
using log4net;
using NUnit.Engine;
using NUnit.Engine.Extensibility;
using AllureCSharpCommons.Events;
using System.Collections.Specialized;
using AllureCSharpCommons.Utils;
using System.Linq;

namespace NUnit.Extension.NUnitAllureTestAdapter
{
    [Extension(Description = "Test Reporter Extension", EngineVersion = "3.4")]
    public class MyTestEventListener : ITestEventListener
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(MyTestEventListener));

        private readonly Allure _lifecycle = Allure.Lifecycle;

        private static readonly bool WriteOutputToAttachmentFlag;
        private static readonly bool TakeScreenShotOnFailedTestsFlag;

        private static readonly OrderedDictionary SuiteStorage =
            new OrderedDictionary();

        public void OnTestEvent(string report)
        {

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(report);
            var eventType = xmlDoc.DocumentElement;
            
            using (StreamWriter file =
            new StreamWriter("WriteLines2.txt", true))
            {
                file.WriteLine(report);
            }
            switch (eventType.Name)
            {
                case "start-suite":
                    

                    //Console.WriteLine($"***test-run started: {eventType.InnerText}");
                    //var suiteName = eventType.GetAttribute("name");
                    var suiteFullName = eventType.GetAttribute("fullname");
                    //var suiteID = eventType.GetAttribute("id");
                    string suiteUid = Guid.NewGuid().ToString();

                    string assembly = suiteFullName.Split('.')[0];
                    string clazz = suiteFullName.Split('.')[suiteFullName.Split('.').Length - 1];

                    var testSuiteStartedEvt = new TestSuiteStartedEvent(suiteUid, suiteFullName);

                    foreach (
                    Assembly asm in AppDomain.CurrentDomain.GetAssemblies().Where(x => x.FullName.Contains(assembly)))
                    {
                        foreach (Type type in asm.GetTypes().Where(x => x.Name.Contains(clazz)))
                        {
                            var manager = new AttributeManager(type.GetCustomAttributes(false).OfType<Attribute>().ToList());
                            manager.Update(testSuiteStartedEvt);
                        }
                    }
                    SuiteStorage.Add(suiteFullName, suiteUid);
                    _lifecycle.Fire(testSuiteStartedEvt);
                    break;
                case "start-test":
                    //Console.WriteLine($"***test started: {eventType.GetAttribute("name")}, id: {eventType.GetAttribute("id")}, parentId: {eventType.GetAttribute("parentId")}");
                    var testFullName = eventType.GetAttribute("fullname");
                    var testName = eventType.GetAttribute("name");

                    string assembly2 = testFullName.Split('.')[0];
                    string clazz2 = testFullName.Split('(')[0].Split('.')[testFullName.Split('(')[0].Split('.').Count() - 2];

                    var testStartedEvt = new TestCaseStartedEvent((string)SuiteStorage[SuiteStorage.Count - 1], testFullName);

                    foreach (
                        Assembly asm in AppDomain.CurrentDomain.GetAssemblies().Where(x => x.FullName.Contains(assembly2)))
                    {
                        foreach (Type type in asm.GetTypes().Where(x => x.Name.Contains(clazz2)))
                        {
                            string name = !testName.Contains("(")
                                ? testName
                                : testName.Substring(0, testName.IndexOf('('));

                            MethodInfo methodInfo = type.GetMethod(name);
                            if (methodInfo == null) continue;
                            var manager =
                                new AttributeManager(methodInfo.GetCustomAttributes(false).OfType<Attribute>().ToList());
                            manager.Update(testStartedEvt);
                        }
                    }
                    
                    _lifecycle.Fire(testStartedEvt);



                    break;
                case "test-case":
                    switch (eventType.GetAttribute("result"))
                    {
                        case "Passed":
                            var testFinishedEvt = new TestCaseFinishedEvent();
                            _lifecycle.Fire(testFinishedEvt);
                            break;
                        case "Failed":
                            {
                                Console.WriteLine($"***{eventType.Name}: {report}");
                                _lifecycle.Fire(new TestCaseFailureEvent
                                {
                                    Throwable = new Exception("test"),
                                    StackTrace = "test"
                                });
                            }
                            break;   
                        default:
                            break;
                    }
                    break;
                case "test-suite":
                    var suiteFullName2 = eventType.GetAttribute("fullname");
                    var testSuiteFinishedEvt = new TestSuiteFinishedEvent((string)SuiteStorage[suiteFullName2]);
                    _lifecycle.Fire(testSuiteFinishedEvt);
                    SuiteStorage.Remove(suiteFullName2);
                    
                    
                    break;

                default:
                    
                    break;

            }
            xmlDoc.Save("output.xml");

        }

        static MyTestEventListener()
        {
            try
            {
                string codeBase = Assembly.GetEntryAssembly().CodeBase;
                var uri = new UriBuilder(codeBase);
                string path = Path.GetDirectoryName(Uri.UnescapeDataString(uri.Path));
                AllureConfig.ResultsPath = $"{path}\\..\\..\\..\\allure-results\\";
                Console.Out.WriteLine($"report path {AllureConfig.ResultsPath}");

                //    XDocument.Load(path + "/config.xml")
                //        .Descendants()
                //        .First(x => x.Name.LocalName.Equals("results-path"))
                //        .Value + "/";

                //TakeScreenShotOnFailedTestsFlag =
                //    Convert.ToBoolean(XDocument.Load(path + "/config.xml")
                //        .Descendants()
                //        .First(x => x.Name.LocalName.Equals("take-screenshots-after-failed-tests"))
                //        .Value);

                //WriteOutputToAttachmentFlag =
                //    Convert.ToBoolean(XDocument.Load(path + "/config.xml")
                //        .Descendants()
                //        .First(x => x.Name.LocalName.Equals("write-output-to-attachment"))
                //        .Value);

                //Logger.Error("Initialization completed successfully.\n");
                //Logger.Error(
                //    String.Format(
                //        "Results Path: {0};\n WriteOutputToAttachmentFlag: {1};\n TakeScreenShotOnFailedTestsFlag: {2}",
                //        AllureConfig.ResultsPath, WriteOutputToAttachmentFlag, TakeScreenShotOnFailedTestsFlag));

                if (Directory.Exists(AllureConfig.ResultsPath))
                {
                    Directory.Delete(AllureConfig.ResultsPath, true);
                }
                Directory.CreateDirectory(AllureConfig.ResultsPath);
            }
            catch (Exception e)
            {
                Console.Out.WriteLine($"Exception in initialization {e}");
            }
        }
    }
}
