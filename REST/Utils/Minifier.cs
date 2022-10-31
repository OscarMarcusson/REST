using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace REST.Utils
{
	static class Minifier
	{
		static string[] GetRows(string file, Func<string,bool> includeRow = null)
			=> file
			     .Split('\n')
			     .Select(x => x.Trim(' ', '\t', '\r'))
			     .Where(x => x.Length > 0 && (includeRow?.Invoke(x) ?? true))
			     .ToArray()
			     ;



		public static string HTML(string file)
		{
			var builder = new StringBuilder();
			for(int i = 0; i < file.Length; i++)
			{
				Skip(file, ref i, ' ', '\t', '\r', '\n');

				// Elements or comments
				if (file[i] == '<')
				{
					i++;
					// Comment?
					if(i + 4 < file.Length && file[i] == '!' && file[i+1] == '-' && file[i+2] == '-' && file[i+3] != '[')
					{
						i = file.IndexOf("-->", i + 2);
						if(i < 0)
							break;

						i += 3;
						continue;
					}

					SkipWhiteSpace(file, ref i);

					var name = "";
					while (i < file.Length && (file[i] != ' ' && file[i] != '\t' && file[i] != '>'))
					{
						name += file[i];
						i++;
					}

					builder.Append('<');
					builder.Append(name);
					SkipWhiteSpace(file, ref i);
					while(i < file.Length && file[i] != '>')
					{
						builder.Append(' ');

						AppendNextWord(builder, file, ref i, '>', '=');
						SkipWhiteSpace(file, ref i);

						if (file[i] == '>')
							break;
						
						if (file[i] == '=')
						{
							i++;
							builder.Append('=');
							SkipWhiteSpace(file, ref i);

							var first = file[i];
							// The attribute value should be enclosed like "value" or 'value', but if not we just brute force print the next word
							if(first != '\'' && first != '"')
							{
								i--;
								AppendNextWord(builder, file, ref i, '>', '=');
							}
							else
							{
								builder.Append(first);
								i++;
								AppendUntil(builder, file, ref i, c => c == first);
								builder.Append(first);
								i++;
								SkipWhiteSpace(file, ref i);
							}
						}

						SkipWhiteSpace(file, ref i);
					}
					builder.Append('>');
					continue;
				}

				// Element value, like the text of a <p> element
				else
				{
					while(i < file.Length && file[i] != '<')
					{
						switch (file[i])
						{
							case '\t':
							case ' ':
								builder.Append(' ');
								SkipWhiteSpace(file, ref i);
								continue;

							case '\r':
								i++;
								continue;

							case '\n':
								i++;
								builder.Append(' ');
								Skip(file, ref i, ' ', '\t', '\r', '\n');
								continue;

							default:
								builder.Append(file[i]);
								break;
						}
						i++;
					}
					i--;
					continue;
				}
			}

			file = builder.ToString();
			return file;
		}


		public static string JavaScript(string file)
		{
			file = file.Trim(' ', '\t', '\r', '\n');
			var rows = GetRows(file, x => !x.StartsWith("//"));

			file = string.Join(" ", rows);
			var builder = new StringBuilder();
			var cache = new StringBuilder();
			for (int i = 0; i < file.Length; i++)
			{
				if (char.IsWhiteSpace(file[i]))
				{
					cache.Append(' ');
					Skip(file, ref i, ' ', '\t', '\r', '\n');
					i--;
					continue;
				}

				if(i + 1 < file.Length && file[i] == '/' && file[i+1] == '*')
				{
					var end = file.IndexOf("*/", i+2);
					if (end > 0)
					{
						i = end + 1;
						continue;
					}
					break;
				}

				// String? Do NOT modify the content of that
				if (file[i] == '\'' || file[i] == '"')
				{
					var tmp = cache.ToString();
					tmp = tmp.ClearJavaScriptCode();
					builder.Append(tmp);
					cache.Clear();

					var first = file[i];
					builder.Append(file[i]);
					while(++i < file.Length)
					{
						builder.Append(file[i]);
						if (file[i] == first && file[i-1] != '\\')
							break;
					}
					continue;
				}

				cache.Append(file[i]);
			}

			if(cache.Length > 0)
			{
				var tmp = cache.ToString();
				tmp = tmp.ClearJavaScriptCode();
				builder.Append(tmp);
			}

			file = builder.ToString();
			return file;
		}



		public static string CSS(string file)
		{
			var builder = new StringBuilder();
			for(int i = 0; i < file.Length; i++)
			{
				Skip(file, ref i, ' ', '\t', '\r', '\n');

				if (i >= file.Length)
					break;

				// Strip comments
				if(i + 2 < file.Length && file[i] == '/' && file[i+1] == '*')
				{
					var end = file.IndexOf("*/", i + 2);
					if (end < 0)
						break;

					i = end + 2;
					continue;
				}

				switch (file[i])
				{
					case '{':
					case '}':
					case ',':
					case '.':
					case '#':
						builder.Append(file[i]);
						SkipWhiteSpace(file, ref i);
						break;

					case ';':
						// The semicolon is added for all except the last value, the } words as well so we
						// save one character by skipping ;} and just doing }
						i++;
						SkipWhiteSpace(file, ref i);
						if(i < file.Length && file[i] == '}')
						{
							builder.Append('}');
						}
						else
						{
							builder.Append(';');
							i--;
						}
						break;

					case ':':
						i++;
						builder.Append(':');
						SkipWhiteSpace(file, ref i);
						while(i < file.Length && file[i] != ';' && file[i] != '}')
						{
							switch (file[i])
							{
								case ' ':
								case '\t':
									if (file[i-1] != ',')
										builder.Append(' ');
									SkipWhiteSpace(file, ref i);
									continue;

								default:
									builder.Append(file[i]);
									i++;
									continue;
							}
						}
						i--;
						break;

					default:
						builder.Append(file[i]);
						break;
				}
			}

			file = builder.ToString();
			return file;
		}











		static string ClearJavaScriptCode(this string str)
			=> str
				.TrimSpacesAround(
					'(', ')',
					'{', '}',
					'[', ']',
					'[', ']',
					'=', '*', '+', '/', '-',
					';', ':'
				);

		static string TrimSpacesAround(this string str, char c)
		{
			str = str.Replace($" {c}", c.ToString());
			str = str.Replace($"{c} ", c.ToString());
			return str;
		}

		static string TrimSpacesAround(this string str, params char[] chars)
		{
			foreach (var c in chars)
				str = str.TrimSpacesAround(c);
			return str;
		}

		static void SkipWhiteSpace(string str, ref int i)
		{
			while (i < str.Length && char.IsWhiteSpace(str[i]))
				i++;
		}

		static void Skip(string str, ref int i, params char[] chars)
		{
			while (i < str.Length && chars.Contains(str[i]))
				i++;
		}

		static void AppendNextWord(StringBuilder builder, string str, ref int i, params char[] limit)
		{
			SkipWhiteSpace(str, ref i);
			while (i < str.Length && !char.IsWhiteSpace(str[i]) && !limit.Contains(str[i]))
			{
				builder.Append(str[i]);
				i++;
			}
		}

		static void AppendUntil(StringBuilder builder, string str, ref int i, Func<char, bool> limit)
		{
			var test = "";
			while (i < str.Length && !limit(str[i]))
			{
				test += str[i];
				builder.Append(str[i]);
				i++;
			}
		}
	}
}
