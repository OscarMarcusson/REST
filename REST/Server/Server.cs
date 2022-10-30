using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using REST.Utils;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using System.IO;

namespace REST
{
	public class Server
	{
		public readonly ushort Port;
		public bool LocalOnly { get; private set; }
		public Encoding Encoding { get; set; } = Encoding.GetEncoding("iso-8859-1");

		TcpListener listener;
		CancellationTokenSource cancellation = new CancellationTokenSource();
		readonly AutoResetEvent WaitListener = new AutoResetEvent(true);

		readonly Logger Logger = new Logger();
		readonly EndPointsManager EndPoints = new EndPointsManager();
		readonly List<RequestHandler> RequestHandlers = new List<RequestHandler>();



		public Server(ushort port)
		{
			Port = port;
		}

		public Server SetLocalOnly(bool value)
		{
			LocalOnly = value;
			return this;
		}



		public Server SetInfoLogger(Action<ushort, object> logger)
		{
			Logger.SetInfoLogger(logger);
			return this;
		}

		public Server SetErrorLogger(Action<ushort, object> logger)
		{
			Logger.SetErrorLogger(logger);
			return this;
		}




		public Server Get(string url, Func<Request, Task<Response>> handler)
		{
			EndPoints.GET.Add(url, handler);
			return this;
		}

		public Server POST(string url, Func<Request, Task<Response>> handler)
		{
			EndPoints.POST.Add(url, handler);
			return this;
		}


		public Server MapDirectory(string path, bool minimizeFileContent = true)
		{
			throw new NotImplementedException();
		}

		public Server MapEmbeddedDirectory(string? source, string? output = null, bool minify = true, Func<Assembly, bool> includeAssembly = null)
		{
			// Format the source to the embedded path format
			source = source?.Replace('/', '.')?.Replace('\\', '.')?.Trim(' ', '\t', '"', '.');
			if (string.IsNullOrWhiteSpace(source))
				source = null;

			// Format the root url
			output = output?.Trim(' ', '\t', '/');
			if (string.IsNullOrWhiteSpace(output))
				output = null;

			// Load the assemblies and start getting resources from them
			var libraryAssembly = Assembly.GetExecutingAssembly();
			var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(x =>
			{
				if (x == libraryAssembly)
					return false;

				if (x.FullName.StartsWith("System.") || x.FullName.StartsWith("netstandard,"))
					return false;

				if (includeAssembly != null)
					return includeAssembly(x);

				return true;
			}).ToArray();

			foreach(var assembly in assemblies)
			{
				var allResources = assembly.GetManifestResourceNames();
				if(allResources.Length > 0)
				{
					var index = allResources[0].IndexOf('.');
					var root = allResources[0].Substring(0, index);
					var directory = source != null
										? $"{root}.{source}."
										: $"{root}."
										;

					var resourcesToLoad = allResources
						.Where(x => x.StartsWith(directory))
						.ToArray()
						;

					foreach(var resourceToLoad in resourcesToLoad)
					{
						using (var stream = assembly.GetManifestResourceStream(resourceToLoad))
						using (var reader = new StreamReader(stream))
						{
							var content = reader.ReadToEnd();

							var extension = Path.GetExtension(resourceToLoad);
							if (minify)
							{
								switch (extension)
								{
									case ".html":
										content = Minifier.HTML(content);
										break;

									case ".js":
										content = Minifier.JavaScript(content);
										break;

									case ".css":
										content = Minifier.CSS(content);
										break;
								}
							}

							var fileUrl = resourceToLoad.Substring(directory.Length).Trim('.');
							fileUrl = fileUrl.Substring(0, fileUrl.Length - extension.Length).Replace('.', '/');
							fileUrl += extension;

							var url = output != null
										? $"{output}/{fileUrl}"
										: fileUrl
										;

							var response = new Response
							{
								Code = HttpStatusCode.OK,
								Type = MimeTypeParser.Resolve(extension),
								Body = content
							};

							Get(url, async _ => response);
						}
					}
				}
			}

			return this;
		}



		public Server Start()
		{
			Stop();

			try
			{
				WaitListener.Reset();
				cancellation = new CancellationTokenSource();

				listener = new TcpListener(LocalOnly ? IPAddress.Loopback : IPAddress.Any, Port);
				listener.Start(100); // TODO:: Config for this value

				new Thread(() => MainLoopAsync().Wait()) { IsBackground = true }.Start();
			}
			catch(Exception e)
			{
				WaitListener.Set();
				Logger.Error(500, e.ToString());
			}

			return this;
		}


		public void Stop()
		{
			cancellation?.Cancel();
			listener?.Stop();
			Task.WhenAll(RequestHandlers.Where(x => x.Task != null).Select(x => x.Task))
				.ConfigureAwait(false)
				.GetAwaiter()
				.GetResult()
				;
			RequestHandlers.Clear();
		}





		public void WaitForExit() => WaitListener.WaitOne();






		async Task MainLoopAsync()
		{
			Logger.Info(90, "ON START EVENT HERE");

			TcpClient client;
			while (!cancellation.IsCancellationRequested)
			{

				client = await listener.AcceptTcpClientAsync();

				var handler = RequestHandlers.FirstOrDefault(x => x.GetLock());
				if(handler == null)
				{
					handler = new RequestHandler(EndPoints, cancellation.Token, Logger, Encoding);
					RequestHandlers.Add(handler);
					Logger.Info(91, $"Created new handler, total: {RequestHandlers.Count}");
				}

				handler.Handle(client);
			}

			Logger.Info(99, "ON STOP EVENT HERE");
			WaitListener.Set();
		}
	}
}