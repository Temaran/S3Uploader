namespace S3Uploader
{
	using System;
	using Amazon;
	using Amazon.S3;
	using Amazon.S3.Transfer;
	using CommandLine;
	using System.IO;
	using System.IO.Compression;

	class Program
	{
		public class Options
		{
			[Option('k', "key", Required = true, HelpText = "Your S3 key.")]
			public string AWSKey { get; set; }

			[Option('s', "secretkey", Required = true, HelpText = "Your S3 secret key.")]
			public string AWSSecretKey { get; set; }

			[Option('p', "path", Required = true, HelpText = "Directory to zip and upload, or file to upload.")]
			public string Path { get; set; }

			[Option('b', "bucket", Required = true, HelpText = "The bucket to upload to.")]
			public string Bucket { get; set; }

			[Option('n', "name", Required = false, HelpText = "Optional new name to use in S3.")]
			public string S3Name { get; set; }

			[Option('d', "subdir", Required = false, HelpText = "Optional subdir to upload to in S3.")]
			public string S3Subdir { get; set; }
		}

		static void Main(string[] args)
		{
			Parser.Default.ParseArguments<Options>(args).WithParsed(options =>
			{
				if (options == null)
				{
					Console.WriteLine("Error: Could not parse arguments.");
					return;
				}

				if (options.AWSKey.Length <= 0 || options.AWSSecretKey.Length <= 0)
				{
					Console.WriteLine("Error: AWSKey or AWSSecretKey are not valid. You must provide these. See here how to generate them: https://docs.aws.amazon.com/IAM/latest/UserGuide/id_credentials_access-keys.html");
					return;
				}

				if (!Directory.Exists(options.Path) && !File.Exists(options.Path))
				{
					Console.WriteLine("Error: Input path must be a valid file or directory. This is neither: {0}", options.Path);
					return;
				}

				if (string.IsNullOrEmpty(options.Bucket))
				{
					Console.WriteLine("Error: You must specify a bucket to use.");
					return;
				}
				options.Bucket = options.Bucket.ToLower();

				var client = new AmazonS3Client(options.AWSKey.ToString(), options.AWSSecretKey.ToString(), RegionEndpoint.EUNorth1);
				var utility = new TransferUtility(client);
				FileAttributes pathAttributes = File.GetAttributes(options.Path);
				if (pathAttributes.HasFlag(FileAttributes.Directory))
				{
					UploadDirectoryToS3(options, utility);
				}
				else
				{
					UploadFileToS3(options, utility);
				}
			});
		}

		private static void UploadDirectoryToS3(Options options, TransferUtility utility)
		{
			try
			{
				var request = new TransferUtilityUploadRequest
				{
					BucketName = string.IsNullOrEmpty(options.S3Subdir) ? options.Bucket : (options.Bucket + @"/" + options.S3Subdir),
					Key = string.IsNullOrEmpty(options.S3Name) ? Path.GetDirectoryName(options.Path) : options.S3Name
				};

				// Create a temporary zip file in the parent directory.
				var parentDI = Directory.GetParent(options.Path);
				if (parentDI == null)
				{
					return;
				}

				var archivePath = Path.Combine(parentDI.FullName, "TempS3Archive.zip");
				File.Delete(archivePath);
				ZipFile.CreateFromDirectory(options.Path, archivePath);

				// Upload the temporary zip file.
				request.FilePath = archivePath;
				utility.Upload(request);

				// Delete the temp zip after we are done.
				File.Delete(archivePath);
			}
			catch (Exception e)
			{
				Console.WriteLine("Error: Could not upload directory: " + e.ToString());
			}
		}

		private static void UploadFileToS3(Options options, TransferUtility utility)
		{
			try
			{
				var request = new TransferUtilityUploadRequest
				{
					BucketName = string.IsNullOrEmpty(options.S3Subdir) ? options.Bucket : (options.Bucket + @"/" + options.S3Subdir),
					Key = string.IsNullOrEmpty(options.S3Name) ? Path.GetFileNameWithoutExtension(options.Path) : options.S3Name,
					FilePath = options.Path
				};

				utility.Upload(request);
			}
			catch (Exception e)
			{
				Console.WriteLine("Error: Could not upload file: " + e.ToString());
			}
		}
	}
}
