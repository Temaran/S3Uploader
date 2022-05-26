using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;

namespace TemaranGHATestRunnerForUE4
{
	public class Options
	{
		[Option('i', "inputlog", Required = true, HelpText = "Path to the log file we are going to generate the report for.")]
		public string InputLogPath { get; set; }
	}

	public static class Logging
	{
		public static void LogInfo(string logString)
		{
			var prevColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine("TemaranGHATestRunnerForUE4: " + logString);
			Console.ForegroundColor = prevColor;
		}

		public static void LogError(string logString)
		{
			var prevColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("TemaranGHATestRunnerForUE4 ERROR: " + logString);
			Console.ForegroundColor = prevColor;
		}
	}

	class FunctionalTest
	{
		public string Name { get; set; }
		public bool TestSucceeded { get; set; }
		public List<string> Errors { get; set; }

		public FunctionalTest()
		{
			Name = "";
			TestSucceeded = false;
			Errors = new List<string>();
		}

		public override string ToString()
		{
			var outputString = string.Format("{0} {1}", TestSucceeded ? ":heavy_check_mark:" : ":x:", Name);
			foreach (var error in Errors)
			{
				outputString += string.Format("\n\t{0}", error);
			}

			return outputString;
		}
	}

	class Program
	{
		static List<string> InterestingLogCategories = new List<string>() { "LogAutomationController", "LogAutomationCommandLine", "LogFunctionalTest" };
		static string FunctionalTestIdentityString = "LogFunctionalTest: Running tests indicated by Repro String";

		static int Main(string[] args)
		{
			int returnVal = 2;
			Parser.Default.ParseArguments<Options>(args).WithParsed(options =>
			{
				if (!File.Exists(options.InputLogPath))
				{
					Logging.LogError("Invalid log path! " + ((options.InputLogPath == null) ? "" : options.InputLogPath));
					return;
				}

				FunctionalTest currentTest = null;
				HashSet<FunctionalTest> allTests = new();
				HashSet<FunctionalTest> failedTests = new();
				var logLines = File.ReadAllLines(options.InputLogPath);
				foreach(var line in logLines)
				{
					var isInterestingLine = false;
					foreach (var interestingCategory in InterestingLogCategories)
					{
						if (line.Contains(interestingCategory))
						{
							isInterestingLine = true;
							break;
						}
					}

					if (!isInterestingLine)
					{
						continue;
					}

					Logging.LogInfo(line);
					var functionalTestIdentityIndex = line.IndexOf(FunctionalTestIdentityString);
					if (functionalTestIdentityIndex != -1)
					{
						currentTest = new FunctionalTest
						{
							Name = line.Substring(functionalTestIdentityIndex + FunctionalTestIdentityString.Length),
							TestSucceeded = true
						};
						allTests.Add(currentTest);
					}
					if (line.Contains("LogAutomationController: Error:"))
					{
						currentTest.TestSucceeded = false;
						currentTest.Errors.Add(line);
						failedTests.Add(currentTest);
					}
				}

				using (var summaryAppender = File.AppendText(Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY")))
				{
					var statusColor = (failedTests.Count == 0) ? "green" : "red";

					summaryAppender.WriteLine("Test run report! :rocket:");
					summaryAppender.WriteLine("----------------");
					summaryAppender.WriteLine(string.Format("<span style=\"color: {0}\">{1}/{2} tests succeeded!</span>", statusColor, allTests.Count - failedTests.Count, allTests.Count));
					summaryAppender.WriteLine("Tests ran:");

					foreach(var test in allTests)
					{
						summaryAppender.WriteLine(string.Format("<span style=\"color: {0}\">{1}</span>\n", statusColor, test));
					}

					summaryAppender.Flush();
				}

				returnVal = (failedTests.Count == 0) ? 0 : 1;
			});

			return returnVal;
		}
	}
}
