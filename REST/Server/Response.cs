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
				Builder.Append(bodyCache);
			}

			return Builder.ToString();
		}


		public async Task Send(StreamWriter writer, Encoding encoding) => await writer.WriteLineAsync(ToHttpResponse(encoding));


		internal static readonly Response BadRequest = new Response(HttpStatusCode.BadRequest);
		internal static readonly Response NotFound = new Response(HttpStatusCode.NotFound);
		internal static readonly Response NotConnected = new Response(HttpStatusCode.BadRequest, "NotConnected");
	}
}
