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
		readonly byte[] Buffer = new byte[1024];
		readonly EndPointsManager EndPoints;
		readonly CancellationToken Cancellation;
		readonly Logger Logger;
		internal Task? Task;
		readonly ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[8192]);

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
		Response response;
		EndPointsMethodGroup? endPointsGroup;
		NameValueCollection? filters;
		ReadMode readMode;
		string url;
		Func<Request, Task<Response>>? handler;
		Dictionary<string, string> headers = new Dictionary<string, string>();
		readonly List<byte> bodyBytes = new List<byte>();


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

		public void Handle(Socket client) => Task = Task.Run(async () => await HandleAsync(client));

		async Task HandleAsync(Socket client)
		{
			var memoryStream = new MemoryStream();
			var reader = new StreamReader(memoryStream, Encoding.UTF8);

			try
			{
				using (client)
				// using (var stream = new NetworkStream(client))
				{
					readMode = ReadMode.Root;

					var line = "";
					int rawCharValue;
					var lineBuilder = new StringBuilder();
					long bodyLength = 0;
					long contentLength = 0;
					var hasLine = false;
					var hasRemainingData = false;
					var hasResponse = false;
					var killConnection = false;
					bodyBytes.Clear();

					while (!killConnection && client.Connected)
					{
						if (!hasRemainingData)
						{
							hasRemainingData = true;
							var result = await client.ReceiveAsync(buffer, SocketFlags.None, Cancellation);
							memoryStream.Write(buffer.Array, buffer.Offset, result);
							memoryStream.Position -= result;
						}

						// Body
						if (readMode == ReadMode.Body)
						{
							// Read every single byte until we reach the target body length
							while (bodyLength < contentLength)
							{

								rawCharValue = reader.Read();
								if (rawCharValue < 0)
								{
									hasRemainingData = false;
									break;
								}

								lineBuilder.Append((char)rawCharValue);
								bodyLength++;
							}

							// Have to check since it might break before reaching end of body
							if (bodyLength >= contentLength)
							{
								var body = lineBuilder.ToString();
								lineBuilder.Clear();
								request = new Request(client.RemoteEndPoint, filters, headers, body);
								hasResponse = true;

								if(reader.Peek() < 0)
								{
									hasRemainingData = false;
									reader.Dispose();
									memoryStream.Dispose();
									memoryStream = new MemoryStream();
									reader = new StreamReader(memoryStream, Encoding.UTF8);
								}
								// TODO:: ELSE we should probably to the same thing, but write the current remainder to the new stream?
							}
						}

						// Root / headers
						else
						{
							while (true)
							{
								rawCharValue = reader.Read();
								if (rawCharValue < 0)
								{
									hasRemainingData = false;
									break;
								}

								// TODO:: some flag for if we're in a pure data read (like byte body)
								// if so we should not do this, just append append append until length is done
								if (rawCharValue == '\n' || rawCharValue == '\r')
								{
									hasLine = true;
									// Due to Windows shenanigans we just have to ensure to strip "\r\n" as a single "\n"
									if (rawCharValue == '\r')
									{
										rawCharValue = reader.Peek();
										if (rawCharValue == '\n')
											rawCharValue = reader.Read();
									}
									break;
								}
								else
								{
									lineBuilder.Append((char)rawCharValue);
								}
							}

							if (hasLine)
							{
								hasLine = false;
								line = lineBuilder.ToString();
								lineBuilder.Clear();

								switch (readMode)
								{
									case ReadMode.Root:
										if (ParseRoot(line, client))
										{
											readMode = ReadMode.Headers;
										}
										else
										{
											hasResponse = true;
											killConnection = true;
										}
										break;

									case ReadMode.Headers:
										if (line.Length > 0)
										{
											if (!ParseHeader(line))
											{
												hasResponse = true;
												killConnection = true;
											}
										}
										else
										{
											if(headers.TryGetValue("Content-Length", out var contentLengthString))
											{
												if(!long.TryParse(contentLengthString, out contentLength))
												{
													// TODO:: Flag for forced kill connection, used for errors to avoid stream garbage
													Logger.Error(12637, $"{client.RemoteEndPoint} [{url}] Could not parse content length header value '{contentLengthString}'");
													contentLength = 0;
												}
												readMode = ReadMode.Body;
											}
											else
											{
												hasResponse = true;
												request = new Request(client.RemoteEndPoint, filters, headers, null);
											}
										}
										break;
								}
							}
						}


						// Is the request done?
						if (hasResponse)
						{
							hasResponse = false;

							// Reset shared variables for the next round
							readMode = ReadMode.Root;
							bodyLength = 0;
							contentLength = 0;
							headers = new Dictionary<string, string>();
							filters = null;
							bodyBytes.Clear();

							if (handler == null || killConnection)
							{
								var message = response.Body?.ToString() ?? $"{client.RemoteEndPoint}: {(int)response.Code} {response.Code}";
								if(message != null)
									Logger.Error(987, message);
								killConnection = true;
							}
							else
							{
								response = await handler.Invoke(request);
							}

							var responseHttp = response.ToHttpResponse();
							var bytes = Encoding.UTF8.GetBytes(responseHttp);
							var data = await client.SendAsync(bytes, SocketFlags.None, Cancellation);
							// await response.Send(writer);
							handler = null;

							if (killConnection)
								return;
						}
					}
				}
			}
			catch (Exception e)
			{
				Logger.Error(500, e.ToString());
			}
			finally
			{
				reader.Dispose();
				memoryStream.Dispose();

				lock (AccessLocker)
					isBusy = false;
			}
		}



		// The first parse of a message, to find the method (GET/POST/etc) and URL
		bool ParseRoot(string line, Socket client)
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
				Logger.Error(9487, $"{client.RemoteEndPoint}: Could not find url: {url}");
				return false;
			}
			
			// Just for the sake of logging
			Logger.Info(21487, $"{client.RemoteEndPoint} requested {url}");
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



		readonly Response InvalidMethodResponse = new Response
		{
			Code = HttpStatusCode.BadRequest,
			Body = "Invalid REST method type"
		};


		static byte[] ReceiveAll(Socket socket)
		{
			var buffer = new List<byte>();

			while (socket.Available > 0)
			{
				var currByte = new Byte[1];
				var byteCounter = socket.Receive(currByte, currByte.Length, SocketFlags.None);

				if (byteCounter.Equals(1))
				{
					buffer.Add(currByte[0]);
				}
			}

			return buffer.ToArray();
		}
	}
}
