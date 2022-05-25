using System;
using System.IO;

namespace Experiments
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("$GITHUB_STEP_SUMMARY: " + Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY"));
			using (var appender = File.AppendText(Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY")))
			{
				appender.WriteLine("LOOOOOL");
				appender.Flush();
			}

			File.Copy(Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY"), "C:\\lol.txt");
		}
	}
}
