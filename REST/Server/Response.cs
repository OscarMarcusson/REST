using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using TinyJson;

namespace REST
{
	public class Response
	{
		readonly StringBuilder Builder = new StringBuilder();

		public HttpStatusCode StatusCode { get; private set; }
		public string StatusCodeDescription { get; private set; }
		public MimeType Type { get; set; } = MimeType.Text;
		public readonly Dictionary<string, string> Headers = new Dictionary<string, string>();

		public object? Body { get; set; }
		string? bodyCache;

		/// <summary> Equivalent of checking <c><see cref="StatusCode"/> == <see cref="HttpStatusCode.OK"/></c></summary>
		public bool OK => StatusCode == HttpStatusCode.OK;


		public Response() : this(HttpStatusCode.OK) { }
		public Response(HttpStatusCode statusCode, string? statusCodeDescription = null)
		{
			StatusCode = statusCode;
			StatusCodeDescription = !string.IsNullOrWhiteSpace(statusCodeDescription)
										? statusCodeDescription
										: statusCode.ToString()
										;
		}



		public string ToHttpResponse(Encoding encoding)
		{
			Builder.Clear();

			Builder.AppendLine($"HTTP/1.1 {(int)StatusCode} {StatusCode}");
			Builder.AppendLine(GetFormattedHeader("Date", DateTime.Now.ToString()));
			Builder.AppendLine(GetFormattedHeader("Server", "Potato 123"));
			Builder.AppendLine(GetFormattedHeader("Connection", "Closed"));

			foreach(var header in Headers)
			{
				switch (header.Key)
				{
					// Ignored headers, already handled or will be after this
					case "Date":
					case "Server":
					case "Connection":
					case "Content-Type":
					case "Content-Length":
						break;

					// Custm headers
					default: Builder.AppendLine($"{header.Key}: {header.Value}"); break;
				}
			}

			if (bodyCache != null)
			{
				Builder.AppendLine($"Content-Type: {Headers["Content-Type"]}");
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

				var contentType = $"{MimeTypeParser.Parser[Type]}; charset={encoding.WebName}";
				Headers["Content-Type"] = contentType;
				Builder.AppendLine($"Content-Type: {contentType}");

				bodyCache = $"Content-Length: {bodyCache.Length}\n\n{bodyCache}";
				Builder.Append(bodyCache);
			}
			else
			{
				Builder.AppendLine("Content-Length: 0");
				Builder.AppendLine();
			}

			return Builder.ToString();
		}

		string GetFormattedHeader(string key, string defaultValue) => $"{key}: {(Headers.TryGetValue(key, out var value) ? value : defaultValue)}";

		public async Task Send(StreamWriter writer, Encoding encoding) => await writer.WriteLineAsync(ToHttpResponse(encoding));


		internal static readonly Response BadRequest = new Response(HttpStatusCode.BadRequest);
		internal static readonly Response NotFound = new Response(HttpStatusCode.NotFound);
		internal static readonly Response NotConnected = new Response(HttpStatusCode.BadRequest, "NotConnected");
	}
}
