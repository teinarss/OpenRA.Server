
#region Copyright & License Information
/*
 * Copyright 2007-2019 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenRa.Server
{
    using MiniYamlNodes = List<MiniYamlNode>;

	public static class MiniYamlExts
	{
		public static void WriteToFile(this MiniYamlNodes y, string filename)
		{
			File.WriteAllLines(filename, y.ToLines().Select(x => x.TrimEnd()).ToArray());
		}

		public static IEnumerable<string> ToLines(this MiniYamlNodes y)
		{
			foreach (var kv in y)
				foreach (var line in kv.Value.ToLines(kv.Key, kv.Comment))
					yield return line;
		}
	}


    public readonly struct MiniYamlNode1
    {
        private readonly MiniYamlDocument  parent;
        private readonly int idx;
    }


	public class MiniYamlNode
	{
		public struct SourceLocation
		{
			public string Filename; public int Line;
			public override string ToString() { return $"{Filename}:{Line}"; }
		}

		public SourceLocation Location;
		public string Key;
		public MiniYaml Value;
		public string Comment;

		public MiniYamlNode(string k, MiniYaml v, string c = null)
		{
			Key = k;
			Value = v;
			Comment = c;
		}

		public MiniYamlNode(string k, MiniYaml v, string c, SourceLocation loc)
			: this(k, v, c)
		{
			Location = loc;
		}

		public MiniYamlNode(string k, string v, string c = null)
			: this(k, v, c, null) { }

		public MiniYamlNode(string k, string v, List<MiniYamlNode> n)
			: this(k, new MiniYaml(v, n), null) { }

		public MiniYamlNode(string k, string v, string c, List<MiniYamlNode> n)
			: this(k, new MiniYaml(v, n), c) { }

		public MiniYamlNode(string k, string v, string c, List<MiniYamlNode> n, SourceLocation loc)
			: this(k, new MiniYaml(v, n), c, loc) { }

		public override string ToString()
		{
			return $"{{YamlNode: {Key} @ {Location}}}";
		}

		public MiniYamlNode Clone()
		{
			return new MiniYamlNode(Key, Value.Clone());
		}
	}

	public class MiniYaml
	{
		const int SpacesPerLevel = 4;
		static readonly Func<string, string> StringIdentity = s => s;
		static readonly Func<MiniYaml, MiniYaml> MiniYamlIdentity = my => my;
		public string Value;
		public List<MiniYamlNode> Nodes;

		public MiniYaml Clone()
		{
			return new MiniYaml(Value, Nodes.Select(n => n.Clone()).ToList());
		}

		public Dictionary<string, MiniYaml> ToDictionary()
		{
			return ToDictionary(MiniYamlIdentity);
		}

		public Dictionary<string, TElement> ToDictionary<TElement>(Func<MiniYaml, TElement> elementSelector)
		{
			return ToDictionary(StringIdentity, elementSelector);
		}

		public Dictionary<TKey, TElement> ToDictionary<TKey, TElement>(
			Func<string, TKey> keySelector, Func<MiniYaml, TElement> elementSelector)
		{
			var ret = new Dictionary<TKey, TElement>();
			foreach (var y in Nodes)
			{
				var key = keySelector(y.Key);
				var element = elementSelector(y.Value);
				try
				{
					ret.Add(key, element);
				}
				catch (ArgumentException ex)
				{
					throw new InvalidDataException($"Duplicate key '{y.Key}' in {y.Location}", ex);
				}
			}

			return ret;
		}

		public MiniYaml(string value)
			: this(value, null) { }

		public MiniYaml(string value, List<MiniYamlNode> nodes)
		{
			Value = value;
			Nodes = nodes ?? new List<MiniYamlNode>();
		}

		public static List<MiniYamlNode> NodesOrEmpty(MiniYaml y, string s)
		{
			var nd = y.ToDictionary();
			return nd.ContainsKey(s) ? nd[s].Nodes : new List<MiniYamlNode>();
		}

		static List<MiniYamlNode> FromLines(IEnumerable<string> lines, string filename, bool discardCommentsAndWhitespace)
		{
			var levels = new List<List<MiniYamlNode>>();
			levels.Add(new List<MiniYamlNode>());

			var lineNo = 0;
			foreach (var ll in lines)
			{
				var line = ll;
				++lineNo;

				var keyStart = 0;
				var level = 0;
				var spaces = 0;
				var textStart = false;

				string key = null;
				string value = null;
				string comment = null;
				var location = new MiniYamlNode.SourceLocation { Filename = filename, Line = lineNo };

				if (line.Length > 0)
				{
					var currChar = line[keyStart];

					while (!(currChar == '\n' || currChar == '\r') && keyStart < line.Length && !textStart)
					{
						currChar = line[keyStart];
						switch (currChar)
						{
							case ' ':
								spaces++;
								if (spaces >= SpacesPerLevel)
								{
									spaces = 0;
									level++;
								}

								keyStart++;
								break;
							case '\t':
								level++;
								keyStart++;
								break;
							default:
								textStart = true;
								break;
						}
					}

					if (levels.Count <= level)
						throw new YamlException( $"Bad indent in miniyaml at {location}");


                    while (levels.Count > level + 1)
						levels.RemoveAt(levels.Count - 1);

					// Extract key, value, comment from line as `<key>: <value>#<comment>`
					// The # character is allowed in the value if escaped (\#).
					// Leading and trailing whitespace is always trimmed from keys.
					// Leading and trailing whitespace is trimmed from values unless they
					// are marked with leading or trailing backslashes
					var keyLength = line.Length - keyStart;
					var valueStart = -1;
					var valueLength = 0;
					var commentStart = -1;
					for (var i = 0; i < line.Length; i++)
					{
						if (valueStart < 0 && line[i] == ':')
						{
							valueStart = i + 1;
							keyLength = i - keyStart;
							valueLength = line.Length - i - 1;
						}

						if (commentStart < 0 && line[i] == '#' && (i == 0 || line[i - 1] != '\\'))
						{
							commentStart = i + 1;
							if (commentStart < keyLength)
								keyLength = i - keyStart;
							else
								valueLength = i - valueStart;

							break;
						}
					}

					if (keyLength > 0)
						key = line.Substring(keyStart, keyLength).Trim();

					if (valueStart >= 0)
					{
						var trimmed = line.Substring(valueStart, valueLength).Trim();
						if (trimmed.Length > 0)
							value = trimmed;
					}

					if (commentStart >= 0 && !discardCommentsAndWhitespace)
						comment = line.Substring(commentStart);

					// Remove leading/trailing whitespace guards
					if (value != null && value.Length > 1)
					{
						var trimLeading = value[0] == '\\' && (value[1] == ' ' || value[1] == '\t') ? 1 : 0;
						var trimTrailing = value[value.Length - 1] == '\\' && (value[value.Length - 2] == ' ' || value[value.Length - 2] == '\t') ? 1 : 0;
						if (trimLeading + trimTrailing > 0)
							value = value.Substring(trimLeading, value.Length - trimLeading - trimTrailing);
					}

					// Remove escape characters from #
					if (value != null && value.IndexOf('#') != -1)
						value = value.Replace("\\#", "#");
				}

				if (key != null || !discardCommentsAndWhitespace)
				{
					var nodes = new List<MiniYamlNode>();
					levels[level].Add(new MiniYamlNode(key, value, comment, nodes, location));

					levels.Add(nodes);
				}
			}

			return levels[0];
		}

		public static List<MiniYamlNode> FromFile(string path, bool discardCommentsAndWhitespace = true)
		{
			return FromLines(File.ReadAllLines(path), path, discardCommentsAndWhitespace);
		}


		public static MiniYamlDocument FromString(string text)
        {

            var data = text.AsSpan();
            var i = 0;

            var level = 0;
            var spaces = 0;
            var textStart = false;
            var keyStart = 0;
            var keyEnd = 0;


            while (i < data.Length)
            {
                var currChar = data[i];
                var lineStart = i;
                
                
                switch (currChar)
                {
                    case ' ':
                        spaces++;
                        if (spaces >= SpacesPerLevel)
                        {
                            spaces = 0;
                            level++;
                        }

                        keyStart++;
                        break;
                    case '\t':
                        level++;
                        keyStart++;
                        break;
                    case ':':
                        keyEnd = i;
                        break;
                    default:
                        textStart = true;
                        break;
                }
            }

            var key = data.Slice(keyStart, keyEnd - keyStart);



            return new MiniYamlDocument()
            {
                Nodes = new List<MiniYamlNode1>()
                {
                    new MiniYamlNode1()
                }
            };
        }

  
		public IEnumerable<string> ToLines(string key, string comment = null)
		{
			var hasKey = !string.IsNullOrEmpty(key);
			var hasValue = !string.IsNullOrEmpty(Value);
			var hasComment = !string.IsNullOrEmpty(comment);
			yield return (hasKey ? key + ":" : "")
				+ (hasValue ? " " + Value.Replace("#", "\\#") : "")
				+ (hasComment ? (hasKey || hasValue ? " " : "") + "#" + comment : "");

			if (Nodes != null)
				foreach (var line in Nodes.ToLines())
					yield return "\t" + line;
		}
    }

    public enum MiniYamlToken
    {
        None,
        Key,
        String,
        Comment,
        Colon,
        Litteral
    }

    public ref struct MiniYamlReader
    {
        private readonly ReadOnlySpan<char> yaml;

        public ReadOnlySpan<char> Value { get; set; }
        public MiniYamlToken CurrentToken;
        private int consumed;

        public MiniYamlReader(ReadOnlySpan<char> yaml)
        {
            CurrentToken = MiniYamlToken.None;
            Value = ReadOnlySpan<char>.Empty;
            this.yaml = yaml;
            consumed = 0;
        }

        public bool Read()
        {
            var c = NextChar();

            while (true)
            {
                switch (c)
                {
                    //case '!':
                    //    if (PeekChar() == '=')
                    //    {
                    //        i++;
                    //        return MiniYamlToken.NotEquals;
                    //    }

                    //    return MiniYamlToken.Not;

                    //case '<':
                    //    if (PeekChar()  == '=')
                    //    {
                    //        i++;
                    //        return TokenType.LessThanOrEqual;
                    //    }

                    //    return TokenType.LessThan;

                    //case '>':
                    //    if (PeekChar()  == '=')
                    //    {
                    //        i++;
                    //        return TokenType.GreaterThanOrEqual;
                    //    }

                    //    return TokenType.GreaterThan;

                    //case '=':
                    //    if (PeekChar()  == '=')
                    //    {
                    //        i++;
                    //        return TokenType.Equals;
                    //    }

                    //    throw new InvalidDataException("Unexpected character '=' at index {0} - should it be `==`?".F(start));

                    //case '&':
                    //    if (PeekChar()  == '&')
                    //    {
                    //        i++;
                    //        return TokenType.And;
                    //    }

                    //    throw new InvalidDataException("Unexpected character '&' at index {0} - should it be `&&`?".F(start));

                    //case '|':
                    //    if (PeekChar()  == '|')
                    //    {
                    //        i++;
                    //        return TokenType.Or;
                    //    }

                    //    throw new InvalidDataException("Unexpected character '|' at index {0} - should it be `||`?".F(start));

                    //case '(':
                    //    return TokenType.OpenParen;

                    //case ')':
                    //    return TokenType.CloseParen;

                    //case '~':
                    //    return TokenType.OnesComplement;
                    //case '+':
                    //    return TokenType.Add;

                    //case '-':
                    //    if (++i < expression.Length && ScanIsNumber(expression, start, ref i))
                    //        return TokenType.Number;

                    //    i = start + 1;
                    //    if (IsLeftOperandOrNone(lastType))
                    //        return TokenType.Negate;
                    //    return TokenType.Subtract;

                    //case '*':
                    //    return TokenType.Multiply;

                    //case '/':
                    //    i++;
                    //    return TokenType.Divide;

                    //case '%':
                    //    return TokenType.Modulo;
                    case ':':
                        CurrentToken = MiniYamlToken.Colon;
                        Value = yaml.Slice(consumed, 1);
                        return true;
                        break;
                    case '#':
                        CurrentToken = MiniYamlToken.Comment;
                        SkipComment();
                        break;
                    default:
                        CurrentToken = MiniYamlToken.Litteral;
                        ConsumeKey();
                        return true;

                }
            }
            

            if (yaml[consumed] == '#')
            {
                SkipComment();
            }

            if(CurrentToken == MiniYamlToken.None)
                return ConsumeKey();
            if (CurrentToken == MiniYamlToken.Key)
                return ConsumeValue();

            if (CurrentToken == MiniYamlToken.String)
                return ConsumeKey();

            return false;
        }

        char NextChar()
        {
            return yaml[consumed++];
        }

        char PeekChar()
        {
            return yaml[consumed];
        }

        private bool ConsumeValue()
        {
            var local = yaml.Slice(consumed + 1);
            var start = 0;
            var i = 0;
            for (; i < local.Length; i++)
            {
                consumed++;
                if (local[i] == ' ')
                    start++;

                if (local[i] == '\n')
                {
                    consumed++;
                    i--;
                    break;
                }

                if (local[i] == '#')
                {
                    SkipComment();
                    break;
                }
                    
            }

            Value = local.Slice(start, i - start);
            CurrentToken = MiniYamlToken.String;

            return true;
        }

        bool ConsumeKey()
        {
            var local = yaml.Slice(consumed);
            var i = 0;
            var c = local[i];
            while (char.IsLetter(c))
            {
                c = local[i++];
                consumed++;
            }

            Value = local.Slice(0, i);

            return true;
        }

        void SkipComment()
        {
            var local = yaml.Slice(consumed);
            var i = 0;

            for (; i < local.Length; i++)
            {
                consumed++;
                if (local[i] == '\n')
                    break;
                
            }
        }
    }

    public class MiniYamlDocument
    {
        ReadOnlyMemory<char> yaml;
        public IEnumerable<MiniYamlNode1> Nodes { get; set; }
    }

    [Serializable]
	public class YamlException : Exception
	{
		public YamlException(string s)
			: base(s) { }
	}
}
