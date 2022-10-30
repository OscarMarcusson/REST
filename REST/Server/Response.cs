using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TinyJson;

namespace REST
{
	public class Response
	{
		readonly StringBuilder Builder = new StringBuilder();

		public HttpStatusCode Code { get; set; } = HttpStatusCode.OK;
		public MimeType Type { get; set; } = MimeType.Text;
		public readonly Dictionary<string, string> Headers = new Dictionary<string, string>();

		public object? Body { get; set; }

		string? bodyCache;


		public string ToHttpResponse(Encoding encoding)
		{
			Builder.Clear();

			Builder.AppendLine($"HTTP/1.1 {(int)Code} {Code}");
			Builder.AppendLine($"Date: {DateTime.Now}");
			Builder.AppendLine($"Server: Potato 123");
			Builder.AppendLine($"Connection: Closed");

			if(bodyCache != null)
			{
				Builder.AppendLine($"Content-Type: {MimeTypeParser.Parser[Type]}; charset={encoding.WebName}");
				Builder.Append(bodyCache);
			}
			else if (Body != null)
			{
				var type = Body.GetType();
				if (type == typeof(string))
				{
					bodyCache = Body.ToString();
				}
				else
				{
					if (type.IsValueType) bodyCache = Body.ToString();
					else if (type.IsEnum) bodyCache = ((int)Body).ToString();
					else
					{
						Type = MimeType.JSon;
						bodyCache = Body?.ToJson() ?? "";
					}
				}

				Builder.AppendLine($"Content-Type: {MimeTypeParser.Parser[Type]}; charset={encoding.WebName}");

				bodyCache = $"Content-Length: {bodyCache.Length}\n\n{bodyCache}";
				Builder.AppendLine(bodyCache);
			}

			return Builder.ToString();
		}


		public async Task Send(StreamWriter writer, Encoding encoding) => await writer.WriteLineAsync(ToHttpResponse(encoding));


		internal static readonly Response BadRequest = new Response { Code = System.Net.HttpStatusCode.BadRequest };
		internal static readonly Response NotFound = new Response { Code = System.Net.HttpStatusCode.NotFound };
	}
}
