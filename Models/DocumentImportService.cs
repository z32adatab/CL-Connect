using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using CampusLogicEvents.Implementation;
using CampusLogicEvents.Implementation.Models;
using CsvHelper;
using Hangfire;
using log4net;
using Newtonsoft.Json.Linq;
using CampusLogicEvents.Web.Results;

namespace CampusLogicEvents.Web.Models
{
	public static class DocumentImportService
	{
		public const string Name = "Document Import"; // Used for logging purposes and also to identify the Hangfire recurring job.
		private static readonly ILog logger = LogManager.GetLogger("AdoNetAppender");
		private static readonly NotificationManager notificationManager = new NotificationManager();


		/// <summary>
		/// Checks a specified folder to see if index files are present.  Each index file
		/// contains records that indicate a document to be uploaded for a student.  Once
		/// each record is processed, the index file is archived so that it is not run again.
		/// Associated documents for each record in the index file are only archived if they 
		/// were successfully processed.
		/// </summary>
		[AutomaticRetry(Attempts = 0)]
		public static void ProcessImports(DocumentImportSettingsDto importSettings)
		{
			DocumentManager manager = new DocumentManager();

			// Check read/write permissions.
			if (!manager.ValidateDirectory(importSettings.FileDirectory) ||
				!manager.ValidateDirectory(importSettings.ArchiveDirectory))
			{
				NotificationService.ErrorNotification(Name, $"{Name} does not authorize read and write updates");
				logger.Error($"{Name} does not authorize read and write updates");
				throw new Exception($"{Name} does not authorize read and write updates");
			}

			// Get all files to process.
			string[] filesToProcess =
				Directory.GetFiles(importSettings.FileDirectory)
					.Where(fileName => fileName.EndsWith(importSettings.FileExtension))
					.Select(fileName => Path.Combine(importSettings.FileDirectory, fileName))
					.ToArray();

			foreach (string filePath in filesToProcess)
			{
				var records = new List<DocumentImportRecord>();
				var failedRecords = new List<DocumentImportRecord>();

				try
				{
					using (StreamReader sr = new StreamReader(filePath))
					{
						// Parse the file using CsvHelper library.
						using (CsvReader csvReader = new CsvReader(sr))
						{
							csvReader.Configuration.RegisterClassMap<DocumentImportRecordCsvMap>();
							csvReader.Configuration.HasHeaderRecord = importSettings.HasHeaderRow;

							records = csvReader.GetRecords<DocumentImportRecord>().Select(record =>
							{
								// Manually set file path for use later.
								record.FilePath = Path.Combine(importSettings.FileDirectory, record.FileName);
								return record;
							}).ToList();
						}
						sr.Close();
						sr.Dispose();
					}
				}
				catch (Exception ex)
				{
					string message = "File was not in the correct format.\n\nDetails:\n\n" + ex;
					CreateFailuresFile(importSettings.ArchiveDirectory, filePath, failureMessage: message);
				}

				// Send each document to StudentVerification for processing.
				foreach (DocumentImportRecord record in records)
				{
					try
					{
						HttpResponseMessage result = manager.ImportDocument(record, importSettings).Result;
						if (result.IsSuccessStatusCode)
						{
							// Archive the document once we are done with it.
							ArchiveFile(record.FilePath, importSettings.ArchiveDirectory);
						}
						else
						{
							if (result.Content == null)
							{
								// Never hit the API...failed validation.
								throw new Exception(result.ReasonPhrase);
							}
							else
							{
								// Get the message returned from the API.
								string jsonStringResponse = result.Content.ReadAsStringAsync().Result;

								// Try to parse the JSON.
								string message = null;
								try
								{
									JObject json = JObject.Parse(jsonStringResponse);
									message = json["message"].Value<string>();
								}
								catch (Exception) { }

								throw new Exception(message ?? jsonStringResponse);
							}
						}
					}
					catch (Exception ex)
					{
						record.FailureReason = ex.Message;
						failedRecords.Add(record);
					}
				}

				// Done.  Now archive the file and create a failures file, if necessary.
				ArchiveFile(filePath, importSettings.ArchiveDirectory);
				CreateFailuresFile(importSettings.ArchiveDirectory, filePath, failedRecords: failedRecords);
			}
		}

		/// <summary>
		/// Takes whatever files are in the bulk action upload directory
		/// and posts them to SV to begin the Bulk Action process
		/// </summary>
		[AutomaticRetry(Attempts = 0)]
		public static void ProcessBulkAction(BulkActionUploadDto bulkActionUpload)
		{

			DocumentManager manager = new DocumentManager();

			// Check read/write permissions.
			if (!manager.ValidateDirectory(bulkActionUpload.FileUploadDirectory) ||
				!manager.ValidateDirectory(bulkActionUpload.FileArchiveDirectory))
			{
				NotificationService.ErrorNotification("Bulk Action", $"{bulkActionUpload.FileUploadDirectory} does not authorize read and write updates");
				logger.Error($"{bulkActionUpload.FileUploadDirectory} does not authorize read and write updates");
				throw new Exception($"{bulkActionUpload.FileUploadDirectory} does not authorize read and write updates");
			}

			// Get all files to process.
			string[] filesToProcess =
				Directory.GetFiles(bulkActionUpload.FileUploadDirectory)
					.Select(fileName => Path.Combine(bulkActionUpload.FileUploadDirectory, fileName))
					.ToArray();

			foreach (string filePath in filesToProcess)
			{
				try
				{
					HttpResponseMessage result = manager.UploadBulkAction(bulkActionUpload, filePath).Result;
					if (result.IsSuccessStatusCode)
					{
						// Archive the document once we are done with it.
						ArchiveFile(filePath, bulkActionUpload.FileArchiveDirectory);
					}
					else
					{
						if (result.Content == null)
						{
							// Never hit the API...failed validation.
							throw new Exception(result.ReasonPhrase);
						}
						else
						{
							// Build error message to send to notitification service 
							var importResult = result.Content.ReadAsAsync<ImportResult>().Result;
							// archive after failure
							ArchiveFile(filePath, bulkActionUpload.FileArchiveDirectory);
							throw new Exception(FormatImportResultMessage(importResult, filePath));
						}
					}
				}
				catch (Exception ex)
				{
					// what to do if a file fails...
					var response = notificationManager.SendErrorNotification("ProcessBulkAction", ex.Message, bulkActionUpload.NotificationEmail).Result;
				}
			}
		}

		private static string FormatImportResultMessage(ImportResult result, string filePath)
		{
			string message = $"Error in file {Path.GetFileName(filePath)}" + Environment.NewLine;
			foreach(var error in result.TopErrors)
			{
				message += $"Error found in row: {error.RowNum}, error message: {error.ErrorMessage}" + Environment.NewLine;
			}
			return message;
		}


		/// <summary>
		/// Archives a file by moving it to the specified archive directory.
		/// </summary>
		/// <param name="filePath">Path of the file to archive.</param>
		/// <param name="archiveDirectory">Directory to move the file to.</param>
		private static void ArchiveFile(string filePath, string archiveDirectory)
		{
			// Validate args
			if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(archiveDirectory))
			{
				return;
			}

			if (File.Exists(filePath))
			{
				string fileName = Path.GetFileName(filePath);
				string archiveFilePath = Path.Combine(archiveDirectory, fileName);

				// Need to delete and replace the file in the archive path if one already exists.
				// Otherwise exception is thrown.
				if (File.Exists(archiveFilePath))
				{
					DocumentManager.WaitReady(archiveFilePath);
					File.Delete(archiveFilePath);
				}

				DocumentManager.WaitReady(filePath);
				File.Move(filePath, archiveFilePath);
			}
		}

		/// <summary>
		/// Creates a file that contains any failure message(s).  Only 1 parameter should be provided.
		/// </summary>
		/// <param name="fileNameOrPath">The name of or path to the original file.  The failures file will use this name and append a .failures extension.</param>
		/// <param name="failedRecords">If provided, creates a CSV file with a list of failed records.</param>
		/// <param name="failureMessage">If provided, creates a text file with a failure message.</param>
		/// <param name="directory">The directory to put the failure file in.</param>
		private static void CreateFailuresFile(string directory, string fileNameOrPath, List<DocumentImportRecord> failedRecords = null, string failureMessage = null)
		{
			// Validate args
			if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileNameOrPath) ||
				((failedRecords as object ?? failureMessage as object) == null))
			{
				return;
			}

			string fileName = Path.GetFileName(fileNameOrPath);
			string failureFilePath = Path.Combine(directory, fileName + ".failures");

			using (StreamWriter sw = new StreamWriter(failureFilePath))
			{
				// Create a new CSV file containing any failed records.
				if (failedRecords != null && failedRecords.Any())
				{
					// Append header row.
					sw.WriteLine("Student ID, Document Name, File Name,Award Year,Failure Reason");

					// Append row per failed record.
					foreach (DocumentImportRecord record in failedRecords)
					{
						sw.WriteLine(
							$"{record.Identifier},\"{record.DocumentName}\",\"{record.FileName}\",\"{record.AwardYearRaw}\",{record.FailureReason}");
					}
				}
				// Otherwise a file with just a message in it.
				else if (!string.IsNullOrWhiteSpace(failureMessage))
				{
					sw.WriteLine(failureMessage);
				}

				sw.Close();
				sw.Dispose();

			}
		}

	}
}