using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.IO;
using REST.Utils;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;

namespace REST
{
	public class Client : IDisposable
	{
		public const ushort Connecting = 100;
		public const ushort CouldNotConnect = 100;



		public readonly IPEndPoint EndPoint;
		readonly Queue<Task<Response>> Queue = new Queue<Task<Response>>();
		readonly SemaphoreSlim QueueWaiter = new SemaphoreSlim(1, 1);

		CancellationTokenSource cancellationTokenSource;


		public decimal SendTimeout { get; set; } = 1m;
		public decimal ReceiveTimeout { get; set; } = 1m;
		public decimal DelayBetweenConnectionTimeouts { get; set; } = 5m;
		public Encoding Encoding { get; set; } = Encoding.GetEncoding("iso-8859-1");

		readonly Logger Logger = new Logger();
		readonly object ClientLocker = new object();
		readonly object RequestLocker = new object();

		bool connected;
		public bool Connected { get { lock (ClientLocker) return connected; } }

		TcpClient? client;
		NetworkStream? networkStream;
		StreamWriter? writer;
		StreamReader? reader;


		/// <summary> Creates a client that only listens on localhost </summary>
		public Client(ushort port) : this(IPAddress.Loopback, port) { }

		/// <summary> Creates a client that listens on a specified IP</summary>
		public Client(IPAddress iPAddress, ushort port)
		{
			EndPoint = new IPEndPoint(iPAddress, port);
			cancellationTokenSource = new CancellationTokenSource();
		}



		public Client SetInfoLogger(Action<ushort, object> logger)
		{
			Logger.SetInfoLogger(logger);
			return this;
		}

		public Client SetErrorLogger(Action<ushort, object> logger)
		{
			Logger.SetErrorLogger(logger);
			return this;
		}



		public async Task<bool> TryConnect(double timeout = 10.0)
		{
			lock (ClientLocker)
			{
				connected = client != null && client.Connected;
				if (connected)
					return true;

				client?.Close();
				client?.Dispose();
				networkStream?.Dispose();
				writer?.Dispose();
				reader?.Dispose();
				client = new TcpClient();
			}

			try
			{
				lock (ClientLocker)
					connected = false;

				var localTaskCancellationTokenSource = new CancellationTokenSource();
				var connectTask = Task.Run(async () =>
				{
					var stopwatch = new Stopwatch();
					while (!localTaskCancellationTokenSource.IsCancellationRequested && !cancellationTokenSource.IsCancellationRequested)
					{
						stopwatch.Restart();
						try
						{
							await client.ConnectAsync(EndPoint.Address, EndPoint.Port);
							break;
						}
						catch (Exception)
						{
							stopwatch.Stop();
							var delay = 6000 - (int)stopwatch.ElapsedTicks; // TODO:: Some setting for this seems reasonable
							try
							{
								if (delay > 1) await Task.Delay(delay, cancellationTokenSource.Token);
								else await Task.Yield();
							}
							// Ignore exceptions here, should just be cancellations
							catch (Exception) { }
						}
					}
				});

				// A timeout of 0 or less means an infinite connection attempt, so just keep trying
				if (timeout <= 0.0)
				{
					await connectTask;
				}
				else
				{
					var timeoutInMs = Math.Max(0, (int)Math.Round(timeout * 1000));
					var timeoutTask = Task.Delay(timeoutInMs);

					await await Task.WhenAny(connectTask, timeoutTask);
					if (timeoutTask.IsCompleted)
					{
						localTaskCancellationTokenSource.Cancel();
						lock (ClientLocker)
						{
							client?.Dispose();
							client = null;
						}
						Logger.Error(5, "Could not connect, timed out");
						return false;
					}
				}

				if (cancellationTokenSource.IsCancellationRequested)
				{
					lock (ClientLocker)
					{
						client?.Dispose();
						client = null;
					}
					return false;
				}


				// await client.ConnectAsync(EndPoint.Address, EndPoint.Port);
				networkStream = client.GetStream();
				reader = new StreamReader(networkStream, Encoding);
				writer = new StreamWriter(networkStream, Encoding);
				lock(ClientLocker)
					connected = true;
				return true;
			}
			catch(Exception e)
			{
				Logger.Error(23, e);
				lock (ClientLocker)
				{
					client?.Dispose();
					client = null;
				}
				return false;
			}
		}



		public Client Disconnect()
		{
			Dispose();
			return this;
		}



		public async Task WaitForExit()
		{
			await QueueWaiter.WaitAsync();
		}


		public async Task<Response> Get(string url)
			=> await Fetch("GET", url, null, null);


		public async Task<Response> Post(string url)
			=> await Fetch("POST", url, null, null);

		public async Task<Response> Post(string url, object body, MimeType? mimeType)
			=> await Fetch("POST", url, body, mimeType);




		async Task<Response> Fetch(string method, string url, object? body, MimeType? mimeType)
		{
			if (!Connected)
				return Response.NotConnected;

			await QueueWaiter.WaitAsync();
			
			try
			{
				// Send request to server
				await writer.WriteLineAsync($"{method} {url} HTTP/1.1");
				await writer.WriteLineAsync("User-Agent: Mozilla/4.0 (compatible; MSIE5.01; Windows NT)");
				await writer.WriteLineAsync("Accept-Language: en-us");
				await writer.WriteLineAsync("Accept-Encoding: gzip, deflate");
				await writer.WriteLineAsync("Connection: Keep-Alive");

				if(body == null)
				{
					await writer.WriteLineAsync();
				}
				else
				{
					var bodyString = "";
					if(mimeType == null)
					{
						// TODO:: Resolve a bit better from the object type
						mimeType = MimeType.Text;
						bodyString = body.ToString(); // TODO:: auto json stuff
					}
					else
					{
						bodyString = body.ToString();
					}

					await writer.WriteLineAsync($"Content-Type: {MimeTypeParser.Parser[mimeType.Value]}; charset={Encoding.WebName}");
					await writer.WriteLineAsync("Content-Length: " + bodyString.Length);
					await writer.WriteLineAsync();
					await writer.WriteLineAsync(bodyString);
				}
				await writer.FlushAsync();




				// Read the response
				// "HTTP/1.1 200 OK" -> "200 OK"
				var line = await reader.ReadLineAsync();
				if (line == null || line.Length == 0)
					line = await reader.ReadLineAsync();

				var index = line.IndexOf(' ') + 1;
				if (index > 0)
					line = index < line.Length
									? line.Substring(index)
									: ""
									;
				if (line.Length == 0)
				{
					return new Response(HttpStatusCode.InternalServerError)
					{
						Body = "Empty response"
					};
				}


				// "200 OK" -> "200" -> parsed enum
				index = line.IndexOf(' ');
				var rawStatusCode = index > -1
							? line.Substring(0, index)
							: line
							;
				if (!Enum.TryParse<HttpStatusCode>(rawStatusCode, out var statusCode) || !Enum.IsDefined(typeof(HttpStatusCode), statusCode))
				{
					return new Response(HttpStatusCode.InternalServerError)
						{
							Body = $"Invalid response status code: {rawStatusCode}"
						};
				}

				var statusDescription = index > -1 ? line.Substring(index + 1) : null;
			
				// Header done, create the response and start populating the headers
				var response = new Response(statusCode, statusDescription);


				while (!reader.EndOfStream && !cancellationTokenSource.IsCancellationRequested)
				{
					line = await reader.ReadLineAsync();
					if (string.IsNullOrWhiteSpace(line))
						break;

					if (!RequestParser.TryParseHeader(line, response.Headers))
					{
						Logger.Error(0847, $"Incorrect header: {line}");
						return new Response(HttpStatusCode.InternalServerError)
						{
							Body = $"Could not parse header: {line}"
						};
					}
				}

				if(response.Headers.TryGetValue("Content-Length", out var contentLengthString))
				{
					if (long.TryParse(contentLengthString, out var contentLength))
					{
						var bodyBytes = new char[contentLength];
						var i = await reader.ReadBlockAsync(bodyBytes, cancellationTokenSource.Token);
						var responseBody = new string(bodyBytes);
						response.Body = responseBody;
					}
					else
					{
						var error = $"[{url}] Could not parse content length header value '{contentLengthString}'";
						Logger.Error(12637, error);
						return new Response(HttpStatusCode.InternalServerError)
						{
							Body = error
						};
					}
				}

				return response;
			}
			catch (Exception e)
			{
				Logger.Error(153, e);
				Dispose();
				return new Response(HttpStatusCode.InternalServerError)
				{
					Body = e.ToString()
				};
			}
			finally
			{
				QueueWaiter.Release();
			}
		}




		public void Dispose()
		{
			cancellationTokenSource.Cancel();
			client?.Close();
			client?.Dispose();
			networkStream?.Dispose();
			writer?.Dispose();
			reader?.Dispose();
		}
	}
}
