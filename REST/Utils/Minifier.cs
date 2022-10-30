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




		static string StripComments(string file, string start, string end, Func<string,bool> includeComment = null)
		{
			var builder = new StringBuilder();
			var takeFrom = 0;
			var startIndex = file.IndexOf(start);
			while(startIndex > -1)
			{
				if(takeFrom < startIndex)
				{
					var content = file.Substring(takeFrom, startIndex - takeFrom);
					builder.Append(content);
					takeFrom = startIndex;
				}

				var endIndex = file.IndexOf(end, startIndex + start.Length);
				if(endIndex > 0)
				{
					if(includeComment != null)
					{
						var comment = file.Substring(startIndex + start.Length, endIndex - startIndex - start.Length);
						if (includeComment(comment))
						{
							builder.Append(start);
							builder.Append(comment);
							builder.Append(end);
						}
					}
					takeFrom = endIndex + end.Length;
					startIndex = file.IndexOf(start, takeFrom);
				}
				// Comment without end, ignore and leave
				else
				{
					takeFrom = file.Length;
					break;
				}
			}

			// Any remaining content after the last comment is added to the builder
			if(takeFrom < file.Length)
			{
				var content = file.Substring(takeFrom);
				builder.Append(content);
			}

			return builder.ToString();
		}


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
			file = StripComments(file, "/*", "*/");
			var rows = GetRows(file, x => !x.StartsWith("//"));
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
