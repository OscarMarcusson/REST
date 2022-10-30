﻿using System.Net.Sockets;
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

namespace REST
{
	public class Server
	{
		public readonly ushort Port;
		public bool LocalOnly { get; private set; }

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

		public Server MapEmbeddedDirectory(string path, bool minimizeFileContent = true)
		{
			throw new NotImplementedException();
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
					handler = new RequestHandler(EndPoints, cancellation.Token, Logger);
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