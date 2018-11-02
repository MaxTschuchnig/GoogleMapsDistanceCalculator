using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GoogleMapsDistances
{
	class Program
	{
		static void Main(string[] args)
		{
			/*
			 * ENTER YOUR GOOGLE DISTANCE API CODE
			*/
			string yourApiCode = "";
			/*
			 * ENTER YOUR GOOGLE DISTANCE API CODE
			*/
			if (yourApiCode.Equals(""))
			{
				Console.WriteLine("Enter your Api Code, Aborting");
				System.Environment.Exit(1);
			}

			bool debug = false;

			List<string> sources = Reader.GetSources();
			List<string> sourcesInfo = Reader.GetSourcesInfo();
			List<string> targets = Reader.GetTargets();
			List<string> targetsInfo = Reader.GetTargetsInfo();

			ErrorWriter err = new ErrorWriter();

			// Default is only shortest on transit
			bool OnlyShortestToTarget = true;
			string travelMode = "transit";
			if (!debug)
			{
				if (sources.Count != sourcesInfo.Count)
				{
					Console.WriteLine("There is a different Amount of Lines in Sources and Sources Information! Aborting ...");
				}
				if (targets.Count != targetsInfo.Count)
				{
					Console.WriteLine("There is a different Amount of Lines in Targets and Targets Information! Aborting ...");
				}

				if (args.Count() < 1 || args.Count() > 2)
				{
					Console.WriteLine("Wrong usage of Programm, Parameters = 2\nParameter one (driving, bicycling, transit, walking)\nParameter two (all, shortest)");
					System.Environment.Exit(1);
				}
				travelMode = args[0];

				Console.WriteLine("Starting, using " + args[0] + " as a distance metric. The time, as of starting, is: " + DateTime.Now.ToString("h:mm:ss tt"));
				
				if (args[1].Equals("all"))
					OnlyShortestToTarget = false;
				else
					OnlyShortestToTarget = true;
			}

			List<int> resultsDistance = new List<int>();
			List<int> resultsDuration = new List<int>();
			List<string> resultsTragets = new List<string>();
			List<string> resultsTragetsInfo = new List<string>();
			List<string> resultsSources = new List<string>();
			List<string> resultsSourcesInfo = new List<string>();

			foreach (string source in sources)
			{
				int shortestTime = int.MaxValue;
				int shortestDistance = int.MaxValue;
				string shortestTarget = "";
				foreach(string target in targets)
				{
					string url = @"https://maps.googleapis.com/maps/api/distancematrix/json?&origins=" + source + "&destinations=" + target + "&mode=" + travelMode + "&key=" + yourApiCode;
					WebRequest request = WebRequest.Create(url);

					WebResponse response = request.GetResponse();

					Stream data = response.GetResponseStream();

					StreamReader reader = new StreamReader(data);

					// json-formatted string from maps api
					string responseFromServer = reader.ReadToEnd();

					response.Close();

					GoogleMapsDistanceJson ResponseUsefull = JsonConvert.DeserializeObject<GoogleMapsDistanceJson>(responseFromServer);
					GoogleMapsDistanceElementDistance tempDistance = ResponseUsefull.rows[0].elements[0].distance;
					GoogleMapsDistanceElementDuration tempDuration = ResponseUsefull.rows[0].elements[0].duration;

					if (ResponseUsefull.rows[0].elements[0].status.Equals("NOT_FOUND") || ResponseUsefull.rows[0].elements[0].status.Equals("ZERO_RESULTS"))
					{
						Console.WriteLine("Couldn't get a result for: " + source + " - " + target + "(" + ResponseUsefull.rows[0].elements[0].status + ") Skipping (Improve Search Query) ...");
						err.AddError(ResponseUsefull.rows[0].elements[0].status, source, target);
						continue;
					}

					if (tempDuration.value < shortestTime)
					{
						shortestTime = tempDuration.value;
						shortestDistance = tempDistance.value;
						shortestTarget = target;
					}

					if (!OnlyShortestToTarget)
					{
						Console.WriteLine(source + " to " + target + " = Distance[m]: " + tempDistance.value + ", Duration[s]: " + tempDuration.value);
						resultsDistance.Add(tempDistance.value);
						resultsDuration.Add(tempDuration.value);
						resultsTragets.Add(target);
						resultsSources.Add(source);

						List<string> tempTargets = JsonConvert.DeserializeObject<List<string>>(JsonConvert.SerializeObject(targets));
						List<string> tempSources = JsonConvert.DeserializeObject<List<string>>(JsonConvert.SerializeObject(sources));
						int targetIndex = tempTargets.FindIndex(a => a == target);
						resultsTragetsInfo.Add(targetsInfo[targetIndex]);
						int sourceIndex = tempSources.FindIndex(a => a == source);
						resultsSourcesInfo.Add(sourcesInfo[sourceIndex]);
					}
						
				}
				// Only keep it if the distance is shorter than the diameter of earth
				if (OnlyShortestToTarget && shortestDistance < 12742000)
				{
					Console.WriteLine(source + " to " + shortestTarget + " = Distance[m]: " + shortestDistance + ", Duration[s]: " + shortestTime);
					resultsDistance.Add(shortestDistance);
					resultsDuration.Add(shortestTime);
					resultsTragets.Add(shortestTarget);
					resultsSources.Add(source);

					List<string> tempTargets = JsonConvert.DeserializeObject<List<string>>(JsonConvert.SerializeObject(targets));
					List<string> tempSources = JsonConvert.DeserializeObject<List<string>>(JsonConvert.SerializeObject(sources));
					int targetIndex = tempTargets.FindIndex(a => a == shortestTarget);
					resultsTragetsInfo.Add(targetsInfo[targetIndex]);
					int sourceIndex = tempSources.FindIndex(a => a == source);
					resultsSourcesInfo.Add(sourcesInfo[sourceIndex]);
				}
			}

			Writer.WriteResultsToFile(resultsTragets, resultsSources, resultsDistance, resultsDuration, resultsSourcesInfo, resultsTragetsInfo);
			err.WriteResultsToFile();

			Console.WriteLine("Finished ...");
			Console.ReadKey();
		}
	}

	public class Writer
	{
		static string filePath = "results.csv";

		public static void WriteResultsToFile(List<string> resultsTragets, List<string> resultsSources, List<int> resultsDistance, List<int> resultsDuration, List<string> sourcesInfo, List<string> targetsInfo)
		{
			//before your loop
			var csv = new StringBuilder();
			csv.AppendLine("Meassured Time" + ";" + "From" + ";" + "From Info" + ";" + "To" + ";" + "To Info" + ";" + "Distance[m]" + ";" + "Distance[Km]" + ";" + "Duration[s]" + ";" + "Duration[m]");

			for (int i = 0; i < resultsDistance.Count; i ++)
			{
				csv.AppendLine(DateTime.Now.ToString("h:mm:ss tt") + ";" + resultsSources[i] + ";" + sourcesInfo[i] + ";" + resultsTragets[i] + ";" + targetsInfo[i] + ";" + resultsDistance[i] + ";" + (resultsDistance[i] / 1000) + ";" + resultsDuration[i] + ";" + (resultsDuration[i] / 60));
			}

			//after your loop
			File.WriteAllText(filePath, csv.ToString(), Encoding.Default);
		}
	}

	public class ErrorWriter
	{
		string filePath = "errors.csv";

		List<string> errors = new List<string>();
		List<string> froms = new List<string>();
		List<string> tos = new List<string>();

		public void AddError(string error, string from, string to)
		{
			this.errors.Add(error);
			this.froms.Add(from);
			this.tos.Add(to);
		}

		public void WriteResultsToFile()
		{
			//before your loop
			var csv = new StringBuilder();
			csv.AppendLine("Status" + ";" + "From" + ";" + "To");

			for (int i = 0; i < errors.Count; i ++)
				csv.AppendLine(errors[i] + ";" + froms[i] + ";" + tos[i]);

			//after your loop
			File.WriteAllText(filePath, csv.ToString(), Encoding.Default);
		}
	}

	public class Reader
	{
		static string sourcesPath = "sources.txt";
		static string targetsPath = "targets.txt";
		static string sourcesInfoPath = "sourcesInfo.txt";
		static string targetsInfoPath = "targetsInfo.txt";

		public static List<string> GetSources()
		{
			List<string> retList = new List<string>();
			var filestream = new System.IO.FileStream(sourcesPath,
										  System.IO.FileMode.Open,
										  System.IO.FileAccess.Read,
										  System.IO.FileShare.ReadWrite);
			var file = new System.IO.StreamReader(filestream, System.Text.Encoding.Default, true, 128);

			string line;
			while ((line = file.ReadLine()) != null)
			{
				retList.Add(line);
			}

			file.Close();
			return retList;
		}

		public static List<string> GetTargets()
		{
			List<string> retList = new List<string>();
			var filestream = new System.IO.FileStream(targetsPath,
										  System.IO.FileMode.Open,
										  System.IO.FileAccess.Read,
										  System.IO.FileShare.ReadWrite);
			var file = new System.IO.StreamReader(filestream, System.Text.Encoding.Default, true, 128);

			string line;
			while ((line = file.ReadLine()) != null)
			{
				retList.Add(line);
			}

			file.Close();
			return retList;
		}

		internal static List<string> GetSourcesInfo()
		{
			List<string> retList = new List<string>();
			var filestream = new System.IO.FileStream(sourcesInfoPath,
										  System.IO.FileMode.Open,
										  System.IO.FileAccess.Read,
										  System.IO.FileShare.ReadWrite);
			var file = new System.IO.StreamReader(filestream, System.Text.Encoding.Default, true, 128);

			string line;
			while ((line = file.ReadLine()) != null)
			{
				retList.Add(line);
			}

			file.Close();
			return retList;
		}

		internal static List<string> GetTargetsInfo()
		{
			List<string> retList = new List<string>();
			var filestream = new System.IO.FileStream(targetsInfoPath,
										  System.IO.FileMode.Open,
										  System.IO.FileAccess.Read,
										  System.IO.FileShare.ReadWrite);
			var file = new System.IO.StreamReader(filestream, System.Text.Encoding.Default, true, 128);

			string line;
			while ((line = file.ReadLine()) != null)
			{
				retList.Add(line);
			}

			file.Close();
			return retList;
		}
	}

	public class GoogleMapsDistanceJson
	{
		public List<string> destination_addresses { get; set; }
		public List<string> origin_addresses { get; set; }
		public List<GoogleMapsDistanceRow> rows { get; set; }
		public string status { get; set; }
	}

	public class GoogleMapsDistanceRow
	{
		public List<GoogleMapsDistanceElement> elements { get; set; }
	}

	public class GoogleMapsDistanceElement
	{
		public GoogleMapsDistanceElementDistance distance { get; set; }
		public GoogleMapsDistanceElementDuration duration { get; set; }
		public string status { get; set; }
	}

	public class GoogleMapsDistanceElementDistance
	{
		public string text { get; set; }
		public int value { get; set; }
	}

	public class GoogleMapsDistanceElementDuration
	{
		public string text { get; set; }
		public int value { get; set; }
	}
}
