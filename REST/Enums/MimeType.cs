using System;
using System.Collections.Generic;
using System.Text;

namespace REST
{
	// https://developer.mozilla.org/en-US/docs/Web/HTTP/Basics_of_HTTP/MIME_types/Common_types
	public enum MimeType
	{
		Text,
		HTML,
		CSS,
		JavaScript,

		Icon,
		Gif,
		Jpeg,
		Png,
		Svg,
		Tiff,

		CSV,
		JSon,
		
		MicrosoftWord,
		MicrosoftWordOpenXML,
	}

	internal static class MimeTypeParser
	{
		public static Dictionary<MimeType, string> Parser = new Dictionary<MimeType, string>
		{
			{ MimeType.Text, "text/plain" },
			{ MimeType.HTML, "text/html" },
			{ MimeType.CSS, "text/css" },
			{ MimeType.JavaScript, "text/javascript" },

			{ MimeType.Icon, "image/vnd.microsoft.icon" },
			{ MimeType.Gif, "image/gif" },
			{ MimeType.Jpeg, "image/jpeg" },
			{ MimeType.Png, "image/png" },
			{ MimeType.Svg, "image/svg+xml" },
			{ MimeType.Tiff, "image/tiff" },

			{ MimeType.CSV, "text/csv" },
			{ MimeType.JSon, "application/json" },

			{ MimeType.MicrosoftWord, "application/msword" },
			{ MimeType.MicrosoftWordOpenXML, "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
		};



		static Dictionary<string, MimeType> ExtensionMapper = new Dictionary<string, MimeType>
		{
			{ ".htm",  MimeType.HTML },
			{ ".html", MimeType.HTML },

			{ ".css",  MimeType.CSS },

			{ ".js",   MimeType.JavaScript },
			{ ".mjs",  MimeType.JavaScript },

			{ ".ico",  MimeType.Icon },
			{ ".gif",  MimeType.Gif },
			{ ".jpeg", MimeType.Jpeg },
			{ ".jpg",  MimeType.Jpeg },
			{ ".png",  MimeType.Png },
			{ ".svg",  MimeType.Svg },
			{ ".tif",  MimeType.Tiff },
			{ ".tiff", MimeType.Tiff},

			{ ".csv",  MimeType.CSV},
			{ ".json", MimeType.JSon},

			{ ".doc",  MimeType.MicrosoftWord},
			{ ".docx", MimeType.MicrosoftWordOpenXML},
		};


		public static MimeType Resolve(string extension)
		{
			if (ExtensionMapper.TryGetValue(extension, out var type))
				return type;

			return MimeType.Text;
		}
	}
}
