using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST
{
	internal class EndPointsMethodGroup
	{
		readonly Dictionary<string, Func<Request, Task<Response>>> Handlers = new Dictionary<string, Func<Request, Task<Response>>>();


		public void Add(string url, Func<Request, Task<Response>> handler)
		{
			url = TrimUrl(url);

			if (Handlers.ContainsKey(url))
				throw new DuplicateNameException($"The URL '{url}' was added twice");

			Handlers[url] = handler;
		}


		public bool TryGet(string url, out Func<Request, Task<Response>>? handler)
		{
			url = TrimUrl(url);
			return Handlers.TryGetValue(url, out handler);
		}


		static string TrimUrl(string url) => url.Trim(' ', '\t', '\\', '/');
	}
}
