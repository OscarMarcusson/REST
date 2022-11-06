using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace REST.Utils
{
	internal static class RequestParser
	{
		public static bool TryParseHeader(string line, Dictionary<string,string> headers)
		{
			var index = line.IndexOf(':');
			if (index > -1)
			{
				headers?.Add(line.Substring(0, index).Trim(), line.Substring(index + 1).Trim());
				return true;
			}
			return false;
		}
	}
}
