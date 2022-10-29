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
		public HttpStatusCode Code { get; set; } = HttpStatusCode.OK;

		public object? Body { get; set; }

		string? cache;

		
		public string ToHttpResponse()
		{
			if (cache != null)
				return cache;

			using (var writer = new StringWriter())
			{
				writer.WriteLine($"HTTP/1.1 {(int)Code} {Code}");
				writer.WriteLine($"Date: {DateTime.Now}");
				writer.WriteLine($"Server: Potato 123");
				writer.WriteLine($"Connection: Closed");

				if (Body != null)
				{
					var contentType = ContentTypeText;
					var type = Body.GetType();
					string body;
					if (type == typeof(string))
					{
						body = Body.ToString();
					}
					else
					{
						if (type.IsValueType) body = Body.ToString();
						else if (type.IsEnum) body = ((int)Body).ToString();
						else
						{
							contentType = ContentTypeJson;
							body = Body?.ToJson() ?? "";
						}
					}

					writer.WriteLine(contentType);
					writer.WriteLine($"Content-Length: {body.Length}");
					writer.WriteLine();
					writer.WriteLine(body);
				}
				else
				{
					writer.WriteLine(ContentTypeText);
					writer.WriteLine($"Content-Length: 0");
				}

				cache = writer.ToString();
				return cache;
			}
		}


		public async Task Send(StreamWriter writer) => await writer.WriteLineAsync(ToHttpResponse());



		const string ContentTypeText = "Content-type: text/plain; charset=UTF-8";
		const string ContentTypeJson = "Content-type: application/json; charset=UTF-8";



		internal static readonly Response BadRequest = new Response { Code = System.Net.HttpStatusCode.BadRequest };
		internal static readonly Response NotFound = new Response { Code = System.Net.HttpStatusCode.NotFound };
	}
}
