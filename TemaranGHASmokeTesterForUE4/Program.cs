using CommandLine;
using System;
using System.Diagnostics;
using System.IO;

namespace TemaranGHASmokeTesterForUE4
{
	public class Options
	{
		[Option('i', "inputstagebinary", Required = true, HelpText = "The path to the staged build binary to use")]
		public string InputStageBinaryPath { get; set; }

		[Option('f', "smokeflag", Required = true, HelpText = "This is the flag that will be passed to the binary indicating this is a smoke test.")]
		public string SmokeFlag { get; set; }

		[Option('s', "successartifact", Required = true, HelpText = "This is the symbol we will look for in the log. If it is present, then the smoke test succeeds.")]
		public string SuccessArtifact { get; set; }
	}

	public static class Logging
	{
		public static void LogInfo(string logString)
		{
			var prevColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine("TemaranGHASmokeTesterForUE4: " + logString);
			Console.ForegroundColor = prevColor;
		}

		public static void LogError(string logString)
		{
			var prevColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("TemaranGHASmokeTesterForUE4 ERROR: " + logString);
			Console.ForegroundColor = prevColor;
		}
	}

	class Program
	{
		private static string LogPathName = "SmokeTestingLog.txt";

		static int Main(string[] args)
		{
			int returnCode = 1;
			Parser.Default.ParseArguments<Options>(args).WithParsed(options =>
			{
				if (!File.Exists(options.InputStageBinaryPath))
				{
					Logging.LogError("Invalid log path! " + ((options.InputStageBinaryPath == null) ? "" : options.InputStageBinaryPath));
					return;
				}

				var startInfo = new ProcessStartInfo
				{
					FileName = options.InputStageBinaryPath,
					Arguments = string.Format("-NoSound -Log=\"{0}\" -{1}", LogPathName, options.SmokeFlag),
					UseShellExecute = true
				};

				Process.Start(startInfo).WaitForExit();
				
				var logs = Directory.GetFiles(Path.GetDirectoryName(options.InputStageBinaryPath), LogPathName, SearchOption.AllDirectories);
				if (logs.Length <= 0)
				{
					Logging.LogError("No smoke test log found.");
					return;
				}
				if (logs.Length > 1)
				{
					Logging.LogError("Found more than one smoke test log.. something is wrong.");
					return;
				}

				returnCode = File.ReadAllText(logs[0]).Contains(options.SuccessArtifact) ? 0 : 1;
			});

			if (returnCode == 0)
			{
				Logging.LogInfo("Success artifact found. Smoke test was successful.");
			}
			else
			{
				Logging.LogError("Smoke test failed.");
			}

			return returnCode;
		}
	}
}
