using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Prediction.v1_6;
using Google.Apis.Prediction.v1_6.Data;
using Google.Apis.Services;

namespace TestEcompanion
{
	class Program
	{
		private const string ApplicationName = "e-companion-application";
		private const string ProjectName = "e-companion";
		private const string PredictiveModelId = "e-companion-predict-send-invoice";
		private const string ServiceAccountEmail = "843055860918-8h4lfmh30b7im6ov80bj8hjhe5nnsn3f@developer.gserviceaccount.com";
		private const string KeyFileName = "key.p12";
		private const string Password = "notasecret";
		private const string StrYes = "yes";
		private const string StrNo = "no";
		private const int ProgressWaitingTime = 60000;
		

		// ReSharper disable once UnusedParameter.Local
		public static void Main(string[] args)
		{
			try
			{
				Run();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error: {0}", ex.Message);
			}

			Console.WriteLine("Press any key to continue");
			Console.ReadKey();
		}

		private static void Run()
		{
			var certificate = new X509Certificate2(KeyFileName, Password, X509KeyStorageFlags.Exportable);
			var credential = new ServiceAccountCredential(
				new ServiceAccountCredential.Initializer(ServiceAccountEmail)
					{ Scopes = new[] { PredictionService.Scope.Prediction } }
						.FromCertificate(certificate));

			var service = new PredictionService(new BaseClientService.Initializer
			{
				ApplicationName = ApplicationName,
				HttpClientInitializer = credential
			});

			Console.WriteLine("Deleting any previous model...");

			string deleteResponse = string.Empty;

			try
			{
				// Delete everything from there.
				var deleteRequest = service.Trainedmodels.Delete(ProjectName, PredictiveModelId);
				deleteResponse = deleteRequest.Execute();
			}
			catch (GoogleApiException ex)
			{
				if (ex.HttpStatusCode.Equals(HttpStatusCode.NotFound))
				{
					// Skip any exception about this.
					Console.WriteLine("The model was not initialized, so it could not be deleted. All OK.");
				}
				else
				{
					throw;
				}
			}

			Console.WriteLine("Deleted the previous model with the status: {0}", deleteResponse);

			var trainingInstances = new List<Insert.TrainingInstancesData>();
			int index;
			
			// Insert the data here
			for (index = 0; index < 100; ++index)
			{
				trainingInstances.Add(new Insert.TrainingInstancesData
				{
					CsvInstance = new List<object> {"jmette", "tivoli", index},
					Output = index % 2 == 0 ? StrYes : StrNo
				});
			}

			var trainInsert = new Insert
			{
				Id = PredictiveModelId,
				TrainingInstances = trainingInstances
			};

			Console.WriteLine("Inserting the training data for the model...");

			// Train the model.
			var insertRequest = service.Trainedmodels.Insert(trainInsert, ProjectName);
			insertRequest.Execute();

			Console.WriteLine("Inserted the training data for the model.");

			// Wait until the training is complete
			while (true)
			{
				Console.WriteLine("Getting a new training progress status...");

				var getRequest = service.Trainedmodels.Get(ProjectName, PredictiveModelId);
				var getResponse = getRequest.Execute();

				Console.WriteLine("Got a new training progress status: {0}", getResponse.TrainingStatus);

				if (getResponse.TrainingStatus.Equals("RUNNING"))
				{
					Console.WriteLine("The model training is still in progress, let us wait for {0} ms.", ProgressWaitingTime);
					Thread.Sleep(ProgressWaitingTime);
				}
				else if (getResponse.TrainingStatus.Equals("DONE"))
				{
					Console.WriteLine("The model has been trained successfully.");
					break;
				}
				else if (getResponse.TrainingStatus.Equals("ERROR: TRAINING JOB NOT FOUND"))
				{
					throw new Exception("the training job was not found.");
				}
				else if (getResponse.TrainingStatus.Equals("ERROR: TOO FEW INSTANCES IN DATASET"))
				{
					throw new Exception("there are too few instances in the dataset.");
				}
				else
				{
					throw new Exception("an unknown error.");
				}
			}

			Console.WriteLine("Predicted the outcome for new data...");

			// Predict model
			var predictInput = new Input
			{
				// Use the next index
				InputValue = new Input.InputData {CsvInstance = new List<object> {"jmette", "tivoli", index}}
			};

			var predictRequest = service.Trainedmodels.Predict(predictInput, ProjectName, PredictiveModelId);
			var predictResponse = predictRequest.Execute();

			// Get the response.
			var responseLabel = predictResponse.OutputLabel;
			Console.WriteLine("Predicted the outcome for new data: {0}", responseLabel);

			// Get any other response.
			foreach (var otherResponse in predictResponse.OutputMulti)
			{
				Console.WriteLine("Other class: {0} with probability: {1}", otherResponse.Label, otherResponse.Score);
			}
		}
	}
}
