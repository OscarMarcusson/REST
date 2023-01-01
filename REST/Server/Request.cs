using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace REST
{
	public class Request
	{
		public readonly EndPoint? IP;

		public readonly string Body;
		public readonly bool HasBody;

		public readonly NameValueCollection? Filters;
		public readonly bool HasFilter;

		internal readonly Dictionary<string, string> Headers;



		internal Request(EndPoint? ip, NameValueCollection? filter, Dictionary<string,string> headers, string? body)
		{
			IP = ip;
			Headers = headers;

			Body = string.IsNullOrWhiteSpace(body) ? string.Empty : body;
			HasBody = body != null;

			HasFilter = filter != null && filter.HasKeys();
			Filters = HasFilter ? filter: null;
		}




		public string? GetHeader(string key) 
			=> Headers.TryGetValue(key, out var value) ? value : null;

		public bool TryGetHeader(string key, out string? value) 
			=> Headers.TryGetValue(key, out value);

		public KeyValuePair<string, string>[] GetHeaders() => Headers.ToArray();
	}
}
