//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using NDesk.Options;
using Raven.Abstractions;
using Raven.Abstractions.Data;
using Raven.Abstractions.Extensions;
using Raven.Abstractions.Smuggler;

namespace Raven.Smuggler
{
	using System.Net.Sockets;

	public class Program
	{
		private readonly RavenConnectionStringOptions connectionStringOptions;
		private readonly SmugglerOptions options;
		private readonly OptionSet optionSet;
		bool incremental, waitForIndexing;

		private Program()
		{
			connectionStringOptions = new RavenConnectionStringOptions();
			options = new SmugglerOptions();

			optionSet = new OptionSet
			            	{
			            		{
			            			"operate-on-types:", "Specify the types to operate on. Specify the types to operate on. You can specify more than one type by combining items with a comma." + Environment.NewLine +
			            			                     "Default is all items." + Environment.NewLine +
			            			                     "Usage example: Indexes,Documents,Attachments", value =>
			            			                                                                     	{
			            			                                                                     		try
			            			                                                                     		{
			            			                                                                     			options.OperateOnTypes = options.ItemTypeParser(value);
			            			                                                                     		}
			            			                                                                     		catch (Exception e)
			            			                                                                     		{
			            			                                                                     			PrintUsageAndExit(e);
			            			                                                                     		}
			            			                                                                     	}
			            			},
			            		{
			            			"metadata-filter:{=}", "Filter documents by a metadata property." + Environment.NewLine +
			            			                       "Usage example: Raven-Entity-Name=Posts", (key, val) => options.Filters.Add(new FilterSetting
			            			                       {
				            			                       Path = "@metadata." + key,
															   ShouldMatch = true,
															   Values = new List<string>{val}
			            			                       })
			            			},
								{
			            			"negative-metadata-filter:{=}", "Filter documents NOT matching a metadata property." + Environment.NewLine +
			            			                       "Usage example: Raven-Entity-Name=Posts", (key, val) => options.Filters.Add(new FilterSetting
			            			                       {
				            			                       Path = "@metadata." + key,
															   ShouldMatch = false,
															   Values = new List<string>{val}
			            			                       })
			            			},
			            		{
			            			"filter:{=}", "Filter documents by a document property" + Environment.NewLine +
			            			              "Usage example: Property-Name=Value", (key, val) => options.Filters.Add(new FilterSetting
			            			              {
													  Path = key,
													  ShouldMatch = true,
													  Values = new List<string>{val}
			            			              })
			            			},
								{
			            			"negative-filter:{=}", "Filter documents NOT matching a document property" + Environment.NewLine +
			            			              "Usage example: Property-Name=Value", (key, val) => options.Filters.Add(new FilterSetting
			            			              {
													  Path = key,
													  ShouldMatch = false,
													  Values = new List<string>{val}
			            			              })
			            			},
			            		{
			            			"transform:", "Transform documents using a given script (import only)", script => options.TransformScript = script
			            		},
								{
			            			"transform-file:", "Transform documents using a given script file (import only)", script => options.TransformScript = File.ReadAllText(script)
			            		},
								{"timeout:", "The timeout to use for requests", s => options.Timeout = int.Parse(s) },
								{"batch-size:", "The batch size for requests", s => options.BatchSize = int.Parse(s) },
			            		{"d|database:", "The database to operate on. If no specified, the operations will be on the default database.", value => connectionStringOptions.DefaultDatabase = value},
			            		{"u|user|username:", "The username to use when the database requires the client to authenticate.", value => Credentials.UserName = value},
			            		{"p|pass|password:", "The password to use when the database requires the client to authenticate.", value => Credentials.Password = value},
			            		{"domain:", "The domain to use when the database requires the client to authenticate.", value => Credentials.Domain = value},
			            		{"key|api-key|apikey:", "The API-key to use, when using OAuth.", value => connectionStringOptions.ApiKey = value},
								{"incremental", "States usage of incremental operations", _ => incremental = true },
								{"wait-for-indexing", "Wait until all indexing activity has been completed (import only)", _=> waitForIndexing=true},
                                {"excludeexpired", "Excludes expired documents created by the expiration bundle", _ => options.ShouldExcludeExpired = true },
			            		{"h|?|help", v => PrintUsageAndExit(0)},
			            	};
		}

		private NetworkCredential Credentials
		{
			get { return (NetworkCredential)(connectionStringOptions.Credentials ?? (connectionStringOptions.Credentials = new NetworkCredential())); }
		}

		static void Main(string[] args)
		{
			var program = new Program();
			program.Parse(args);
		}

		private void Parse(string[] args)
		{
			// Do these arguments the traditional way to maintain compatibility
			if (args.Length < 3)
				PrintUsageAndExit(-1);

			SmugglerAction action = SmugglerAction.Export;
			if (string.Equals(args[0], "in", StringComparison.OrdinalIgnoreCase))
				action = SmugglerAction.Import;
			else if (string.Equals(args[0], "out", StringComparison.OrdinalIgnoreCase))
				action = SmugglerAction.Export;
			else
				PrintUsageAndExit(-1);

			var url = args[1];
			if (url == null)
			{
				PrintUsageAndExit(-1);
				return;
			}
			connectionStringOptions.Url = url;

			options.BackupPath = args[2];
			if (options.BackupPath == null)
				PrintUsageAndExit(-1);

			try
			{
				optionSet.Parse(args);
			}
			catch (Exception e)
			{
				PrintUsageAndExit(e);
			}

			if (options.BackupPath != null && Directory.Exists(options.BackupPath))
			{
				incremental = true;
			}

			var smugglerApi = new SmugglerApi(options, connectionStringOptions);

			try
			{
				switch (action)
				{
					case SmugglerAction.Import:
						smugglerApi.ImportData(options, incremental).Wait();
						if (waitForIndexing)
							smugglerApi.WaitForIndexing(options).Wait();
						break;
					case SmugglerAction.Export:
						smugglerApi.ExportData(null, options, incremental).Wait();
						break;
				}
			}
			catch (AggregateException ex)
			{
				var exception = ex.ExtractSingleInnerException();
				var e = exception as WebException;
				if (e != null)
				{

					if (e.Status == WebExceptionStatus.ConnectFailure)
					{
						Console.WriteLine("Error: {0} {1}", e.Message, connectionStringOptions.Url);
						var socketException = e.InnerException as SocketException;
						if (socketException != null)
						{
							Console.WriteLine("Details: {0}", socketException.Message);
							Console.WriteLine("Socket Error Code: {0}", socketException.SocketErrorCode);
						}

						Environment.Exit((int)e.Status);
					}

					var httpWebResponse = e.Response as HttpWebResponse;
					if (httpWebResponse == null)
						throw;
					Console.WriteLine("Error: " + e.Message);
					Console.WriteLine("Http Status Code: " + httpWebResponse.StatusCode + " " + httpWebResponse.StatusDescription);

					using (var reader = new StreamReader(httpWebResponse.GetResponseStream()))
					{
						string line;
						while ((line = reader.ReadLine()) != null)
						{
							Console.WriteLine(line);
						}
					}

					Environment.Exit((int)httpWebResponse.StatusCode);
				}
				else
				{
					Console.WriteLine(ex);
					Environment.Exit(-1);
				}
			}
		}

		private void PrintUsageAndExit(Exception e)
		{
			Console.WriteLine(e.Message);
			PrintUsageAndExit(-1);
		}

		private void PrintUsageAndExit(int exitCode)
		{
			Console.WriteLine(@"
Smuggler Import/Export utility for RavenDB
----------------------------------------
Copyright (C) 2008 - {0} - Hibernating Rhinos
----------------------------------------
Usage:
	- Import the dump.raven file to a local instance:
		Raven.Smuggler in http://localhost:8080/ dump.raven
	- Export a local instance to dump.raven:
		Raven.Smuggler out http://localhost:8080/ dump.raven

Command line options:", SystemTime.UtcNow.Year);

			optionSet.WriteOptionDescriptions(Console.Out);
			Console.WriteLine();

			Environment.Exit(exitCode);
		}
	}
}
