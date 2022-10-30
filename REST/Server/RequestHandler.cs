using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using REST.Utils;

namespace REST
{
	enum ReadMode
	{
		Root,
		Headers,
		Body
	}


	internal class RequestHandler
	{
		readonly EndPointsManager EndPoints;
		readonly CancellationToken Cancellation;
		readonly Logger Logger;
		internal Task? Task;

		readonly object AccessLocker = new object();
		bool isBusy;
		public bool IsBusy
		{
			get
			{
				lock(AccessLocker)
				{
					return isBusy;
				}
			}
		}

		// Pre-defined variables to avoid a bit of GC
		Request request;
		Response? response;
		EndPointsMethodGroup? endPointsGroup;
		NameValueCollection? filters;
		string url;
		Func<Request, Task<Response>>? handler;
		Dictionary<string, string> headers = new Dictionary<string, string>();


		public RequestHandler(EndPointsManager endPoints, CancellationToken cancellationToken, Logger logger)
		{
			EndPoints = endPoints;
			Cancellation = cancellationToken;
			Logger = logger;
		}

		public bool GetLock()
		{
			lock (AccessLocker)
			{
				if (!isBusy)
				{
					isBusy = true;
					return true;
				}
				return false;
			}
		}

		public void Handle(TcpClient client) => Task = Task.Run(async () => await HandleAsync(client));

		async Task HandleAsync(TcpClient client)
		{
			try
			{
				using (client)
				using (var networkStream = client.GetStream())
				using (var networkReader = new StreamReader(networkStream, Encoding.UTF8))
				{
					// networkWriter.AutoFlush = true;
					long contentLength;
					var killConnection = false;

					// Message loop
					while (!networkReader.EndOfStream && !Cancellation.IsCancellationRequested && !killConnection)
					{
						// Reset shared variables between messages
						response = null;
						handler = null;
						contentLength = -1;
						headers.Clear();

						// Method (GET/POST etc) and URL
						var line = await networkReader.ReadLineAsync();
						killConnection = !ParseRoot(line, client);

						// Headers
						while (!networkReader.EndOfStream && !Cancellation.IsCancellationRequested && !killConnection)
						{
							line = await networkReader.ReadLineAsync();
							if (string.IsNullOrWhiteSpace(line))
								break;

							killConnection = !ParseHeader(line);
						}

						// Figure out if we're done or if we have a body to parse
						if (headers.TryGetValue("Content-Length", out var contentLengthString))
						{
							if (long.TryParse(contentLengthString, out contentLength))
							{
								var bodyBytes = new char[contentLength];
								var i = await networkReader.ReadBlockAsync(bodyBytes, Cancellation);
								var body = new string(bodyBytes);
								request = new Request(client.Client.RemoteEndPoint, filters, headers, body);
							}
							else
							{
								Logger.Error(12637, $"{client.Client.RemoteEndPoint} [{url}] Could not parse content length header value '{contentLengthString}'");
								contentLength = 0;
								killConnection = true;
							}
						}
						// No body, create a request without one
						else
						{
							request = new Request(client.Client.RemoteEndPoint, filters, headers, null);
						}


						// If we have a handler we're all good, send the request to the handler
						if (handler != null)
						{
							if (killConnection)
								request.Headers["Connection"] = "close";

							response = await handler.Invoke(request);
						}

						// Send the response
						if (response != null)
						{
							var responseHttp = response.ToHttpResponse();
							// var chars = responseHttp.ToCharArray();
							// await networkWriter.WriteAsync(chars, Cancellation);
							var bytes = Encoding.UTF8.GetBytes(responseHttp);
							await client.Client.SendAsync(bytes, SocketFlags.None, Cancellation);

							if (killConnection)
								Logger.Info(43523, "Killing connection to " + client.Client.RemoteEndPoint);
						}
						// If we got this far without a request something is seriously wrong, terminate the connection for safety
						else
						{
							Logger.Error(36273, "INTERNAL ERROR - Reached end of message loop without a response. This should not happen, terminating connection...");
							killConnection = true;
						}
					}

					client.Close();
				}

			}
			catch(Exception e)
			{
				Logger.Error(500, e);
			}
			finally
			{
					lock (AccessLocker)
						isBusy = false;
			}
		}



		// The first parse of a message, to find the method (GET/POST/etc) and URL
		bool ParseRoot(string line, TcpClient client)
		{
			var index = line.IndexOf(' ');
			var requestMethod = line.Substring(0, index);
			switch (requestMethod)
			{
				case "GET": endPointsGroup = EndPoints.GET; break;
				case "POST": endPointsGroup = EndPoints.POST; break;
				default:
					response = Response.BadRequest;
					return false;
			}

			var endIndex = line.LastIndexOf("HTTP/");
			url = index > -1
						? line.Substring(index + 1, endIndex - index - 1)
						: line.Substring(index + 1)
						;

			url = HttpUtility.UrlDecode(url);
			var filterIndex = url.IndexOf('?');
			filters = null;
			if (filterIndex > -1)
			{
				var filter = url.Length > filterIndex + 1
						? url.Substring(filterIndex + 1).Trim()
						: null
						;
				if (filter?.Length > 0)
					filters = HttpUtility.ParseQueryString(filter);

				url = url.Substring(0, filterIndex);
			}

			// Does this endpoint exist?
			if (!endPointsGroup.TryGet(url, out handler))
			{
				response = Response.NotFound;
				Logger.Error(9487, $"{client.Client.RemoteEndPoint}: Could not find url: {url}");
				return false;
			}
			
			// Just for the sake of logging
			Logger.Info(21487, $"{client.Client.RemoteEndPoint} requested {url}");
			return true;
		}


		bool ParseHeader(string line)
		{
			var index = line.IndexOf(':');
			if (index > -1)
			{
				headers?.Add(line.Substring(0, index).Trim(), line.Substring(index + 1).Trim());
				return true;
			}
			else
			{
				Logger.Error(0847, $"Incorrect header: {line}");
				response = new Response
				{
					Code = HttpStatusCode.BadRequest,
					Body = $"Could not parse as a header: {line}"
				};
				return false;
			}
		}
	}
}
