namespace TemaranGHAS3Uploader
{
	using System;
	using Amazon;
	using Amazon.S3;
	using Amazon.S3.Transfer;
	using CommandLine;
	using System.IO;
	using System.IO.Compression;

	public class Options
	{
		[Option('k', "key", Required = true, HelpText = "Your S3 key.")]
		public string AWSKey { get; set; }

		[Option('s', "secretkey", Required = true, HelpText = "Your S3 secret key.")]
		public string AWSSecretKey { get; set; }

		[Option('r', "region", Required = false, HelpText = "Your S3 region.")]
		public string AWSRegion { get; set; }

		[Option('p', "path", Required = true, HelpText = "Directory to zip and upload, or file to upload.")]
		public string Path { get; set; }

		[Option('b', "bucket", Required = true, HelpText = "The bucket to upload to.")]
		public string Bucket { get; set; }

		[Option('n', "name", Required = false, HelpText = "Optional new name to use in S3.")]
		public string S3Name { get; set; }

		[Option('d', "subdir", Required = false, HelpText = "Optional subdir to upload to in S3.")]
		public string S3Subdir { get; set; }
	}

	public static class Logging
	{
		public static void LogInfo(string logString)
		{
			var prevColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine("TemaranGHAS3Uploader: " + logString);
			Console.ForegroundColor = prevColor;
		}

		public static void LogError(string logString)
		{
			var prevColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("TemaranGHAS3Uploader ERROR: " + logString);
			Console.ForegroundColor = prevColor;
		}
	}

	class Program
	{
		static int Main(string[] args)
		{
			int returnVal = 3;
			Parser.Default.ParseArguments<Options>(args).WithParsed(options =>
			{
				if (options == null)
				{
					Logging.LogError("Could not parse arguments.");
					return;
				}

				if (options.AWSKey.Length <= 0 || options.AWSSecretKey.Length <= 0)
				{
					Logging.LogError("AWSKey or AWSSecretKey are not valid. You must provide these. See here how to generate them: https://docs.aws.amazon.com/IAM/latest/UserGuide/id_credentials_access-keys.html");
					return;
				}

				if (!Directory.Exists(options.Path) && !File.Exists(options.Path))
				{
					Logging.LogError(String.Format("Input path must be a valid file or directory. This is neither: {0}", options.Path));
					return;
				}

				if (string.IsNullOrEmpty(options.Bucket))
				{
					Logging.LogError("You must specify a bucket to use.");
					return;
				}
				options.Bucket = options.Bucket.ToLower();

				var endPoint = string.IsNullOrEmpty(options.AWSRegion) ? RegionEndpoint.EUNorth1 : RegionEndpoint.GetBySystemName(options.AWSRegion);
				var client = new AmazonS3Client(options.AWSKey.ToString(), options.AWSSecretKey.ToString(), endPoint);
				var utility = new TransferUtility(client);
				var uploader = new S3Uploader(utility);

				FileAttributes pathAttributes = File.GetAttributes(options.Path);
				if (pathAttributes.HasFlag(FileAttributes.Directory))
				{
					returnVal = uploader.UploadDirectoryToS3(options);
				}
				else
				{
					returnVal = uploader.UploadFileToS3(options);
				}
			});

			return returnVal;
		}
	}

	class S3Uploader
	{
		private TransferUtility _transferUtility;
		private int _currentPercentDone = 0;

		public S3Uploader(TransferUtility transferUtility)
		{
			_transferUtility = transferUtility;
		}

		public int UploadDirectoryToS3(Options options)
		{
			Logging.LogInfo("Attempting to upload directory " + options.Path);
			try
			{
				var path = new DirectoryInfo(options.Path);
				if (path == null || path.Parent == null)
				{
					return 2;
				}

				var request = new TransferUtilityUploadRequest
				{
					BucketName = string.IsNullOrEmpty(options.S3Subdir) ? options.Bucket : (options.Bucket + @"/" + options.S3Subdir),
					Key = string.IsNullOrEmpty(options.S3Name) ? path.Name : options.S3Name
				};

				var archivePath = Path.Combine(path.Parent.FullName, "TempS3Archive.zip");
				Logging.LogInfo("Creating temporary archive to upload in the parent dir: " + archivePath);
				File.Delete(archivePath);
				ZipFile.CreateFromDirectory(options.Path, archivePath);

				Logging.LogInfo("Uploading archive...");
				request.FilePath = archivePath;
				request.UploadProgressEvent += Request_UploadProgressEvent;
				_transferUtility.Upload(request);

				Logging.LogInfo("Deleting temporary archive.");
				File.Delete(archivePath);

				var summaryText = string.Format("{0} uploaded to: https://{1}.s3.amazonaws.com/{2}/{0}", request.Key, options.Bucket, options.S3Subdir);
				Logging.LogInfo("Writing " + summaryText + " to summary...");
				using (var summaryAppender = File.AppendText(Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY")))
				{
					summaryAppender.WriteLine(summaryText);
					summaryAppender.Flush();
				}

				return 0;
			}
			catch (Exception e)
			{
				Logging.LogError("Could not upload directory: " + e.ToString());
				return 1;
			}
		}

		public int UploadFileToS3(Options options)
		{
			Logging.LogInfo("Attempting to upload file " + options.Path);

			try
			{
				var request = new TransferUtilityUploadRequest
				{
					BucketName = string.IsNullOrEmpty(options.S3Subdir) ? options.Bucket : (options.Bucket + @"/" + options.S3Subdir),
					Key = string.IsNullOrEmpty(options.S3Name) ? Path.GetFileNameWithoutExtension(options.Path) : options.S3Name,
					FilePath = options.Path
				};
				request.UploadProgressEvent += Request_UploadProgressEvent;

				Logging.LogInfo("Uploading file...");
				_transferUtility.Upload(request);

				var summaryText = string.Format("{0} uploaded to: https://{1}.s3.amazonaws.com/{2}/{0}", request.Key, options.Bucket, options.S3Subdir);
				Logging.LogInfo("Writing " + summaryText + " to summary...");
				using (var summaryAppender = File.AppendText(Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY")))
				{
					summaryAppender.WriteLine(summaryText);
					summaryAppender.Flush();
				}
				return 0;
			}
			catch (Exception e)
			{
				Logging.LogError("Could not upload file: " + e.ToString());
				return 1;
			}
		}

		private void Request_UploadProgressEvent(object sender, UploadProgressArgs e)
		{
			if (e.PercentDone != _currentPercentDone)
			{
				Logging.LogInfo("Upload progress: " + e.PercentDone + "%");
			}

			_currentPercentDone = e.PercentDone;
		}
	}
}
