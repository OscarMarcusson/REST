using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.IO;
using REST.Utils;

namespace REST
{
	public class Client
	{
		public const ushort Connecting = 100;
		public const ushort CouldNotConnect = 100;



		public readonly IPEndPoint EndPoint;
		Task? task;
		CancellationTokenSource cancellationTokenSource;


		public decimal SendTimeout { get; set; } = 1m;
		public decimal ReceiveTimeout { get; set; } = 1m;
		public decimal DelayBetweenConnectionTimeouts { get; set; } = 5m;
		public Encoding Encoding { get; set; } = Encoding.GetEncoding(1252);

		readonly Logger Logger = new Logger();


		public Client(ushort port)
		{
			var isLocal = true;
			EndPoint = new IPEndPoint(isLocal ? IPAddress.Loopback : IPAddress.Any, port);
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




		public Client Start()
		{
			var thread = new Thread(() =>
			{
				Stop();
				WaitForExit();
				cancellationTokenSource = new CancellationTokenSource();
				task = Init();
				try { task.Wait(); }
				catch (Exception) { }
			});
			thread.Start();
			return this;
		}





		async Task Init()
		{
			// var stopwatch = new Stopwatch();
			while (!cancellationTokenSource.IsCancellationRequested)
			{
				try
				{
					using (var client = new Socket(EndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
					{
						client.ReceiveTimeout = Math.Min(10, SecondsToMS(ReceiveTimeout));
						client.SendTimeout = Math.Min(10, SecondsToMS(SendTimeout));
						client.NoDelay = true;
						client.DontFragment = true;

						// stopwatch.Restart();
						Logger.Info(Connecting, $"Connecting to {EndPoint}...");
						await client.ConnectAsync(EndPoint);
						Console.WriteLine("CONNECTED!");
						// TODO:: Connect callback

						// using (var stream = new NetworkStream(client))
						// using (var reader = new StreamReader(stream))
						// using (var writer = new StreamWriter(stream))
						using(var messageWriter = new StringWriter())
						{
							// var test = client.
							// TODO:: A while !cancellationTokenSource.IsCancellationRequested loop that sends / receives messages from
							// a thread safe pool so the client can be used from the UI blocking thread

							/*
							await writer.WriteLineAsync("GET /api/test-1 HTTP/1.1");
							// Request headers
							await writer.WriteLineAsync($"Host: {EndPoint}");
							await writer.WriteLineAsync($"User-Agent: REST client");
							await writer.WriteLineAsync($"Accept: text/html");
							await writer.WriteLineAsync($"Accept-Language: en-US,en;q=0.5");
							await writer.WriteLineAsync($"Accept-Encoding: gzip, deflate");
							// General headers
							await writer.WriteLineAsync($"Connection: keep-alive");
							await writer.WriteLineAsync($"Upgrade-Insecure-Requests: 1");

							// TODO:: Handle
							var test = await reader.ReadToEndAsync();
							Logger.Info(0, test);
							*/
							await Task.Delay(1000, cancellationTokenSource.Token);
						}

						client.Disconnect(false);
						client.Shutdown(SocketShutdown.Both);
						client.Dispose();
					}
				}
				catch (SocketException socketException)
				{
					// Timed out
					if (socketException.NativeErrorCode == 10061)
					{
						await ErrorWait(CouldNotConnect, DelayBetweenConnectionTimeouts);
					}
					else
					{
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine(socketException);
						Console.ResetColor();
					}
				}
				catch (Exception e)
				{
					// TODO:: Proper logger
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine(e);
					Console.ResetColor();
				}


				/*
				// Send message.
				var message = "Hi friends 👋!<|EOM|>";
				var messageBytes = Encoding.UTF8.GetBytes(message);
				_ = await client.SendAsync(messageBytes, SocketFlags.None);
				Console.WriteLine($"Socket client sent message: \"{message}\"");

				// Receive ack.
				var buffer = new byte[1_024];
				var received = await client.ReceiveAsync(buffer, SocketFlags.None);
				var response = Encoding.UTF8.GetString(buffer, 0, received);
				if (response == "<|ACK|>")
				{
					Console.WriteLine(
						$"Socket client received acknowledgment: \"{response}\"");
					break;
				}
				// Sample output:
				//     Socket client sent message: "Hi friends 👋!<|EOM|>"
				//     Socket client received acknowledgment: "<|ACK|>"
				*/
			}
		}



		public Client Stop()
		{
			cancellationTokenSource?.Cancel();
			return this;
		}

		public Client WaitForExit()
		{
			try { task?.Wait(); }
			catch (Exception e)
			{
			}
			return this;
		}

		/// <summary>
		///		A helper method that calls <see cref="Stop"/> and <see cref="Start"/> automatically.
		///		<para>Useful when changing settings during runtime.</para>
		/// </summary>
		public Client Restart() => Stop().Start();







		static int SecondsToMS(decimal d) => (int)Math.Round(d * 1000);

		async Task ErrorWait(ushort errorCode, decimal seconds)
		{
			Logger.Error(errorCode, $"Waiting for {seconds:G29} {(seconds == 1m ? "second" : "seconds")}...");
			var timeToWait = SecondsToMS(seconds);
			if (timeToWait <= 1)
				await Task.Yield();
			else
				await Task.Delay(timeToWait, cancellationTokenSource.Token);
		}








		public async Task<Response> Get(string url)
		{
			return null;
		}
	}
}
