using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST.Utils
{
	internal class Logger
	{
		readonly object Locker = new object();

		Action<ushort, object> logInfo;
		Action<ushort, object> logError;



		// Setters
		public void SetLoggers(Action<ushort, object> info, Action<ushort, object> error)
		{
			SetInfoLogger(info);
			SetErrorLogger(error);
		}
		public void SetInfoLogger(Action<ushort, object> info)
		{
			lock (Locker)
				logInfo = info;
		}
		public void SetErrorLogger(Action<ushort, object> error)
		{
			lock (Locker)
				logError = error;
		}


		// Logging calls
		public void Info(ushort code, object message)
		{
			lock (Locker)
			{
				if (logInfo == null)
				{
					Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [INFO ]: {message}");
				}
				else
				{
					logInfo(code, message);
				}
			}
		}
		public void Error(ushort code, object message)
		{
			lock (Locker)
			{
				if (logError == null)
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [ERROR]: {code} - {message}");
					Console.ResetColor();
				}
				else
				{
					logError(code, message);
				}
			}
		}
	}
}
