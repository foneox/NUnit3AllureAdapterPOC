using System;
using System.IO;
using System.Reflection;
using System.Xml;

using AllureCSharpCommons;
using NUnit.Engine;
using NUnit.Engine.Extensibility;
using AllureCSharpCommons.Events;
using System.Collections.Specialized;
using AllureCSharpCommons.Utils;
using System.Linq;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;

namespace NUnit.Extension.NUnitAllureTestAdapter
{
    [Extension(Description = "Test Reporter Extension", EngineVersion = "3.4")]
    public class MyTestEventListener : ITestEventListener
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(MyTestEventListener));    

        private static void SetupLogger()
        {
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();

            PatternLayout patternLayout = new PatternLayout
            {
                ConversionPattern = "%date [%thread] %-5level %logger - %message%newline"
            };
            patternLayout.ActivateOptions();
            var roller = new FileAppender
            {
                AppendToFile = false,
                File = @"NUnit3AllureAdapter.log",
                Layout = patternLayout
            };
            roller.ActivateOptions();
            hierarchy.Root.AddAppender(roller);

            var memory = new MemoryAppender();
            memory.ActivateOptions();
            hierarchy.Root.AddAppender(memory);

            hierarchy.Root.Level = Level.Info;
            hierarchy.Configured = true;
        }

        

        private readonly Allure _lifecycle = Allure.Lifecycle;

        private static readonly OrderedDictionary SuiteStorage =
            new OrderedDictionary();

        public void OnTestEvent(string report)
        {
            Logger.Info($"{report}");

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
                    var suiteFullName = eventType.GetAttribute("name");
                    //var suiteFullName = eventType.GetAttribute("fullname");
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
                    Logger.Info($"Adding suite '{suiteFullName}' with '{suiteUid}' to SuiteStorage");
                    SuiteStorage.Add(suiteFullName, suiteUid);
                    _lifecycle.Fire(testSuiteStartedEvt);
                    break;
                case "start-test":

                    try
                    {
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
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"{ex.Message} - {ex.StackTrace}");
                    }




                    break;
                case "test-case":
                    switch (eventType.GetAttribute("result"))
                    {
                        case "Passed":
                            Logger.Info($"Test Passed: {eventType.GetAttribute("name")}");
                            var testFinishedEvt = new TestCaseFinishedEvent();
                            _lifecycle.Fire(testFinishedEvt);
                            break;
                        case "Failed":
                            {
                                Logger.Info($"Test Failed: {eventType.GetAttribute("name")}");
                                try
                                {
                                    _lifecycle.Fire(new TestCaseFailureEvent
                                    {
                                        Throwable = new Exception(eventType["failure"]?["message"]?.InnerText),
                                        StackTrace = eventType["failure"]?["stack-trace"]?.InnerText
                                    });
                                    _lifecycle.Fire(new TestCaseFinishedEvent());
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error($"{ex.Message} - {ex.StackTrace}");
                                }

                            }
                            break;
                    }
                    break;
                case "test-suite":
                    var suiteFullName2 = eventType.GetAttribute("name");
                    var suiteUidFromStorage = (string) SuiteStorage[suiteFullName2];
                    if (suiteUidFromStorage != null)
                    {
                        var testSuiteFinishedEvt = new TestSuiteFinishedEvent(suiteUidFromStorage);
                        try
                        {
                            _lifecycle.Fire(testSuiteFinishedEvt);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"{ex.Message} - {ex.StackTrace}");
                        }

                        SuiteStorage.Remove(suiteFullName2);
                    }
                    else
                    {
                        Logger.Warn($"{suiteFullName2} - suite not present in storage");
                    }
                    break;

                default:
                    
                    break;

            }
        }

        static MyTestEventListener()
        {
            SetupLogger();
            try
            {
                AllureConfig.ResultsPath = "allure-results/";
                Logger.Info("Initialization completed successfully.\n");
                Logger.Info($"Results Path: {AllureConfig.ResultsPath}\n");

                if (Directory.Exists(AllureConfig.ResultsPath))
                {
                    Directory.Delete(AllureConfig.ResultsPath, true);
                }
                Directory.CreateDirectory(AllureConfig.ResultsPath);
            }
            catch (Exception e)
            {
                Logger.Error($"Exception in initialization {e}");
            }
        }
    }
}
