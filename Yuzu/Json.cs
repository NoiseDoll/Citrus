﻿using System;
using System.Text;

namespace Yuzu
{
	public class JsonSerializeOptions
	{
		public string FieldSeparator = "\n";
		public string Indent = "\t";
	};

	public class JsonSerializer : AbstractWriterSerializer
	{
		public JsonSerializeOptions JsonOptions = new JsonSerializeOptions();

		protected override void ToWriter(object obj)
		{
			writer.Write('{');
			writer.Write('\n');
			var first = true;
			foreach (var f in obj.GetType().GetFields()) {
				if (!first) {
					writer.Write(',');
					WriteStr(JsonOptions.FieldSeparator);
				}
				first = false;
				WriteStr(JsonOptions.Indent);
				writer.Write('"');
				WriteStr(f.Name);
				writer.Write('"');
				writer.Write(':');
				var t = f.FieldType;
				if (t == typeof(int)) {
					WriteStr(f.GetValue(obj).ToString());
				}
				else if (t == typeof(string)) {
					writer.Write('"');
					WriteStr(f.GetValue(obj).ToString());
					writer.Write('"');
				}
				else {
					throw new NotImplementedException(t.Name);
				}
			}
			if (!first)
				writer.Write('\n');
			writer.Write('}');
		}
	};

	public class JsonDeserializer : AbstractDeserializer
	{
		public static JsonDeserializer Instance = new JsonDeserializer();

		private char? buf;

		private char Next()
		{
			if (!buf.HasValue)
				return Reader.ReadChar();
			var result = buf.Value;
			buf = null;
			return result;
		}

		private void PutBack(char ch)
		{
			if (buf.HasValue)
				throw new YuzuException();
			buf = ch;
		}

		private char SkipSpaces()
		{
			char ch;
			do {
				ch = Next();
			} while (ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r');
			return ch;
		}

		private char Require(params char[] chars)
		{
			var ch = SkipSpaces();
			if (Array.IndexOf(chars, ch) < 0)
				throw new YuzuException();
			return ch;
		}

		private char JsonUnquote(char ch)
		{
			switch (ch) {
				case '"':
					return '"';
				case '\\':
					return '\\';
				case 'n':
					return '\n';
				case 't':
					return '\t';
			}
			throw new YuzuException();
		}

		private string RequireString()
		{
			var result = "";
			Require('"');
			while (true) {
				var ch = Next();
				if (ch == '"')
					break;
				if (ch == '\\')
					ch = JsonUnquote(Reader.ReadChar());
				result += ch;
			}
			return result;
		}

		private int RequireInt()
		{
			var result = "";
			var ch = SkipSpaces();
			while ('0' <= ch && ch <= '9') {
				result += ch;
				ch = Next();
			}
			PutBack(ch);
			return int.Parse(result);
		}

		private string GetNextName(bool first)
		{
			var ch = SkipSpaces();
			if (ch == ',') {
				if (first)
					throw new YuzuException();
				ch = SkipSpaces();
			}
			PutBack(ch);
			if (ch == '}')
				return "";
			var result = RequireString();
			Require(':');
			return result;
		}

		public override void FromReader(object obj)
		{
			buf = null;
			Require('{');
			string name = GetNextName(true);
			foreach (var f in obj.GetType().GetFields()) {
				if (f.Name != name) {
					if (!f.IsDefined(Options.DefaultAttribute, true))
						throw new YuzuException();
					continue;
				}
				var t = f.FieldType;
				if (t == typeof(int)) {
					f.SetValue(obj, RequireInt());
				}
				else if (t == typeof(string)) {
					f.SetValue(obj, RequireString());
				}
				else {
					throw new NotImplementedException(t.Name);
				}
				name = GetNextName(false);
			}
		}
	}
}
