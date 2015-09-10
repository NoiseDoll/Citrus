﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Yuzu
{
	public class JsonSerializeOptions
	{
		public string FieldSeparator = "\n";
		public string Indent = "\t";
		public string ClassTag = "class";
		public bool EnumAsString = false;
		public bool ArrayLengthPrefix = false;
	};

	public class JsonSerializer : AbstractWriterSerializer
	{
		public JsonSerializeOptions JsonOptions = new JsonSerializeOptions();

		private void WriteInt(object obj)
		{
			WriteStr(obj.ToString());
		}

		private void WriteDouble(object obj)
		{
			WriteStr(((double)obj).ToString(CultureInfo.InvariantCulture));
		}

		private void WriteSingle(object obj)
		{
			WriteStr(((float)obj).ToString(CultureInfo.InvariantCulture));
		}

		private void WriteEnumAsInt(object obj)
		{
			WriteStr(((int)obj).ToString());
		}

		private void WriteString(object obj)
		{
			writer.Write('"');
			WriteStr(obj.ToString());
			writer.Write('"');
		}

		private void WriteList<T>(List<T> list)
		{
			var wf = GetWriteFunc(typeof(T));
			writer.Write('[');
			if (list.Count > 0) {
				var isFirst = true;
				foreach (var elem in list) {
					if (!isFirst)
						writer.Write(',');
					isFirst = false;
					WriteStr(JsonOptions.FieldSeparator);
					wf(elem);
				}
				WriteStr(JsonOptions.FieldSeparator);
			}
			writer.Write(']');
		}

		private void WriteArray<T>(T[] array)
		{
			var wf = GetWriteFunc(typeof(T));
			writer.Write('[');
			if (array.Length > 0) {
				if (JsonOptions.ArrayLengthPrefix)
					WriteStr(array.Length.ToString());
				var isFirst = !JsonOptions.ArrayLengthPrefix;
				foreach (var elem in array) {
					if (!isFirst)
						writer.Write(',');
					isFirst = false;
					WriteStr(JsonOptions.FieldSeparator);
					wf(elem);
				}
				WriteStr(JsonOptions.FieldSeparator);
			}
			writer.Write(']');
		}

		private Action<object> GetWriteFunc(Type t)
		{
			if (t == typeof(int) || t == typeof(uint))
				return WriteInt;
			if (t == typeof(double))
				return WriteDouble;
			if (t == typeof(float))
				return WriteSingle;
			if (t == typeof(string))
				return WriteString;
			if (t.IsEnum) {
				if (JsonOptions.EnumAsString)
					return WriteString;
				else
					return WriteEnumAsInt;
			}
			if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) {
				var m = Utils.GetPrivateCovariantGeneric(GetType(), "WriteList", t);
				return obj => m.Invoke(this, new object[] { obj });
			}
			if (t.IsArray) {
				var m = Utils.GetPrivateCovariantGeneric(GetType(), "WriteArray", t);
				return obj => m.Invoke(this, new object[] { obj });
			}
			if (Utils.IsStruct(t))
				return ToWriter;
			if (t.IsClass)
				return ToWriter;
			throw new NotImplementedException(t.Name);
		}

		private void WriteSep(ref bool isFirst)
		{
			if (!isFirst) {
				writer.Write(',');
				WriteStr(JsonOptions.FieldSeparator);
			}
			isFirst = false;
		}

		private void WriteName(string name, ref bool isFirst)
		{
			WriteSep(ref isFirst);
			WriteStr(JsonOptions.Indent);
			WriteString(name);
			writer.Write(':');
		}

		protected override void ToWriter(object obj)
		{
			writer.Write('{');
			WriteStr(JsonOptions.FieldSeparator);
			var isFirst = true;
			var t = obj.GetType();
			if (Options.ClassNames && !Utils.IsStruct(t)) {
				WriteName(JsonOptions.ClassTag, ref isFirst);
				WriteString(t.FullName);
			}
			foreach (var yi in Utils.GetYuzuItems(t, Options)) {
				WriteName(yi.Name, ref isFirst);
				GetWriteFunc(yi.Type)(yi.GetValue(obj));
			}
			if (!isFirst)
				WriteStr(JsonOptions.FieldSeparator);
			writer.Write('}');
		}

		private void ToWriterCompact(object obj)
		{
			writer.Write('[');
			WriteStr(JsonOptions.FieldSeparator);
			var isFirst = true;
			var t = obj.GetType();
			if (Options.ClassNames && !Utils.IsStruct(t)) {
				WriteSep(ref isFirst);
				WriteString(t.FullName);
			}
			foreach (var yi in Utils.GetYuzuItems(t, Options)) {
				WriteSep(ref isFirst);
				GetWriteFunc(yi.Type)(yi.GetValue(obj));
			}
			if (!isFirst)
				WriteStr(JsonOptions.FieldSeparator);
			writer.Write(']');
		}
	}

	public class JsonDeserializer : AbstractReaderDeserializer
	{
		public static JsonDeserializer Instance = new JsonDeserializer();
		public JsonSerializeOptions JsonOptions = new JsonSerializeOptions();

		public JsonDeserializer()
		{
			Options.Assembly = Assembly.GetCallingAssembly();
		}

		protected char? buf;

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
			char ch = Next();
			while (ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r')
				ch = Reader.ReadChar();
			return ch;
		}

		protected char SkipSpacesCarefully()
		{
			if (buf.HasValue)
				throw new YuzuException();
			while (true) {
				var v = Reader.PeekChar();
				if (v < 0)
					return '\0';
				var ch = (char)v;
				if (ch != ' ' && ch != '\t' || ch != '\n' || ch != '\r')
					return ch;
				Reader.ReadChar();
			}
		}

		protected char Require(params char[] chars)
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

		// Optimization: avoid re-creating StringBuilder.
		private StringBuilder sb = new StringBuilder();

		protected string RequireString()
		{
			sb.Clear();
			Require('"');
			while (true) {
				// Optimization: buf is guaranteed to be empty after Require, so no need to call Next.
				var ch = Reader.ReadChar();
				if (ch == '"')
					break;
				if (ch == '\\')
					ch = JsonUnquote(Reader.ReadChar());
				sb.Append(ch);
			}
			return sb.ToString();
		}

		protected uint RequireUInt()
		{
			var ch = SkipSpaces();
			uint result = 0;
			while ('0' <= ch && ch <= '9') {
				checked { result = result * 10 + (uint)ch - (uint)'0'; }
				ch = Reader.ReadChar();
			}
			PutBack(ch);
			return result;
		}

		protected int RequireInt()
		{
			var ch = SkipSpaces();
			int sign = 1;
			if (ch == '-') {
				sign = -1;
				ch = Reader.ReadChar();
			}
			int result = 0;
			while ('0' <= ch && ch <= '9') {
				checked { result = result * 10 + (int)ch - (int)'0'; }
				ch = Reader.ReadChar();
			}
			PutBack(ch);
			return sign * result;
		}

		private string ParseFloat()
		{
			// Optimization: Do not extract helper methods.
			sb.Clear();
			var ch = SkipSpaces();
			if (ch == '-') {
				sb.Append(ch);
				ch = Reader.ReadChar();
			}
			while ('0' <= ch && ch <= '9') {
				sb.Append(ch);
				ch = Reader.ReadChar();
			}
			if (ch == '.') {
				sb.Append(ch);
				ch = Reader.ReadChar();
				while ('0' <= ch && ch <= '9') {
					sb.Append(ch);
					ch = Reader.ReadChar();
				}
			}
			if (ch == 'e'|| ch == 'E') {
				sb.Append(ch);
				ch = Reader.ReadChar();
				if (ch == '+' || ch == '-') {
					sb.Append(ch);
					ch = Reader.ReadChar();
				}
				while ('0' <= ch && ch <= '9') {
					sb.Append(ch);
					ch = Reader.ReadChar();
				}
			}
			PutBack(ch);
			return sb.ToString();
		}

		protected double RequireDouble()
		{
			return Double.Parse(ParseFloat(), CultureInfo.InvariantCulture);
		}

		protected float RequireSingle()
		{
			return Single.Parse(ParseFloat(), CultureInfo.InvariantCulture);
		}

		protected string GetNextName(bool first)
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

		protected object Make(string typeName)
		{
			var t = Options.Assembly.GetType(typeName);
			if (t == null)
				throw new YuzuException();
			return Activator.CreateInstance(t);
		}

		private List<T> ReadList<T>()
		{
			var list = new List<T>();
			Require('[');
			// ReadValue might invoke a new serializer, so we must not rely on PutBack.
			if (SkipSpacesCarefully() == ']')
				Require(']');
			else {
				var rf = ReadValueFunc(typeof(T));
				do {
					list.Add((T)rf());
				} while (Require(']', ',') == ',');
			}
			return list;
		}

		private T[] ReadArray<T>()
		{
			return ReadList<T>().ToArray();
		}

		private T[] ReadArrayWithLengthPrefix<T>()
		{
			Require('[');
			// ReadValue might invoke a new serializer, so we must not rely on PutBack.
			if (SkipSpacesCarefully() == ']') {
				Require(']');
				return new T[0];
			}
			var array = new T[RequireUInt()];
			var rf = ReadValueFunc(typeof(T));
			for (int i = 0; i < array.Length; ++i) {
				Require(',');
				array[i] = (T)rf();
			}
			Require(']');
			return array;
		}

		// Optimization: Avoid creating trivial closures.
		private object RequireIntObj() { return RequireInt(); }
		private object RequireStringObj() { return RequireString(); }
		private object RequireUIntObj() { return RequireUInt(); }
		private object RequireSingleObj() { return RequireSingle(); }
		private object RequireDoubleObj() { return RequireDouble(); }

		private Func<object> ReadValueFunc(Type t)
		{
			if (t == typeof(int))
				return RequireIntObj;
			if (t == typeof(uint))
				return RequireUIntObj;
			if (t == typeof(string))
				return RequireStringObj;
			if (t == typeof(float))
				return RequireSingleObj;
			if (t == typeof(double))
				return RequireDoubleObj;
			if (t.IsEnum) {
				if (JsonOptions.EnumAsString)
					return () => Enum.Parse(t, RequireString());
				else
					return () => Enum.ToObject(t, RequireInt());
			}
			if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) {
				var m = Utils.GetPrivateCovariantGeneric(GetType(), "ReadList", t);
				return () => m.Invoke(this, new object[] {});
			}
			if (t.IsArray) {
				var n = JsonOptions.ArrayLengthPrefix ? "ReadArrayWithLengthPrefix" : "ReadArray";
				var m = Utils.GetPrivateCovariantGeneric(GetType(), n, t);
				return () => m.Invoke(this, new object[] { });
			}
			if (t.IsClass && Options.ClassNames)
				return FromReaderInt;
			if (t.IsClass && !Options.ClassNames || Utils.IsStruct(t))
				return () => FromReaderInt(Activator.CreateInstance(t));
			throw new NotImplementedException(t.Name);
		}

		protected virtual object ReadFields(object obj, string name)
		{
			foreach (var yi in Utils.GetYuzuItems(obj.GetType(), Options)) {
				if (yi.Name != name) {
					if (!yi.IsOptional)
						throw new YuzuException();
					continue;
				}
				yi.SetValue(obj, ReadValueFunc(yi.Type)());
				name = GetNextName(false);
			}
			Require('}');
			return obj;
		}

		public override object FromReaderInt()
		{
			if (!Options.ClassNames)
				throw new YuzuException();
			buf = null;
			Require('{');
			if (GetNextName(true) != JsonOptions.ClassTag)
				throw new YuzuException();
			return ReadFields(Make(RequireString()), GetNextName(false));
		}

		public override object FromReaderInt(object obj)
		{
			buf = null;
			Require('{');
			string name = GetNextName(true);
			if (Options.ClassNames) {
				if (name != JsonOptions.ClassTag)
					throw new YuzuException();
				if (RequireString() != obj.GetType().FullName)
					throw new YuzuException();
				name = GetNextName(false);
			}
			return ReadFields(obj, name);
		}

		private JsonDeserializerGenBase MakeDeserializer(string className)
		{
			var result = (JsonDeserializerGenBase)(Make(className + "_JsonDeserializer"));
			result.Reader = Reader;
			return result;
		}

		protected object FromReaderIntGenerated()
		{
			if (!Options.ClassNames)
				throw new YuzuException();
			buf = null;
			Require('{');
			if (GetNextName(true) != JsonOptions.ClassTag)
				throw new YuzuException();
			var d = MakeDeserializer(RequireString());
			Require(',');
			return d.FromReaderIntPartial(GetNextName(false));
		}

		protected object FromReaderIntGenerated(object obj)
		{
			buf = null;
			Require('{');
			string name = GetNextName(true);
			if (Options.ClassNames) {
				if (name != JsonOptions.ClassTag)
					throw new YuzuException();
				if (RequireString() != obj.GetType().FullName)
					throw new YuzuException();
				name = GetNextName(false);
			}
			return MakeDeserializer(obj.GetType().FullName).ReadFields(obj, name);
		}
	}

	public abstract class JsonDeserializerGenBase : JsonDeserializer
	{
		public abstract object FromReaderIntPartial(string name);

		public override object FromReaderInt()
		{
			return FromReaderIntGenerated();
		}
	}

	public class JsonDeserializerGenerator: JsonDeserializer
	{
		public static new JsonDeserializerGenerator Instance = new JsonDeserializerGenerator();

		private int indent = 0;
		public StreamWriter GenWriter;

		public JsonDeserializerGenerator()
		{
			Options.Assembly = Assembly.GetCallingAssembly();
		}

		private void PutPart(string s)
		{
			GenWriter.Write(s.Replace("\n", "\r\n"));
		}

		private void Put(string s)
		{
			if (s.StartsWith("}")) // "}\n" or "} while"
				indent -= 1;
			if (s != "\n")
				for (int i = 0; i < indent; ++i)
					PutPart(JsonOptions.Indent);
			PutPart(s);
			if (s.EndsWith("{\n"))
				indent += 1;
		}

		private void PutF(string format, params object[] p)
		{
			Put(String.Format(format, p));
		}

		public void GenerateHeader(string namespaceName)
		{
			Put("using System;\n");
			Put("using System.Collections.Generic;\n");
			Put("using System.Reflection;\n");
			Put("\n");
			Put("using Yuzu;\n");
			Put("\n");
			PutF("namespace {0}\n", namespaceName);
			Put("{\n");
			Put("\n");
		}

		public void GenerateFooter()
		{
			Put("}\n");
		}

		private int tempCount = 0;

		private string GetTypeSpec(Type t)
		{
			return t.IsGenericType ?
				String.Format("{0}<{1}>",
					t.Name.Remove(t.Name.IndexOf('`')),
					String.Join(",", t.GetGenericArguments().Select(GetTypeSpec))) :
				t.Name;
		}

		private void GenerateValue(Type t, string name)
		{
			if (t == typeof(int)) {
				PutPart("RequireInt();\n");
			}
			else if (t == typeof(uint)) {
				PutPart("RequireUInt();\n");
			}
			else if (t == typeof(string)) {
				PutPart("RequireString();\n");
			}
			else if (t == typeof(float)) {
				PutPart("RequireSingle();\n");
			}
			else if (t == typeof(double)) {
				PutPart("RequireDouble();\n");
			}
			else if (t.IsEnum) {
				PutPart(String.Format(
					JsonOptions.EnumAsString ?
						"({0})Enum.Parse(typeof({0}), RequireString());\n" :
						"({0})RequireInt();\n",
					t.Name));
			}
			else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) {
				PutPart(String.Format("new {0}();\n", GetTypeSpec(t)));
				Put("Require('[');\n");
				Put("if (SkipSpacesCarefully() == ']') {\n");
				Put("Require(']');\n");
				Put("}\n");
				Put("else {\n");
				Put("do {\n");
				tempCount += 1;
				var tempName = "tmp" + tempCount.ToString();
				PutF("var {0} = ", tempName);
				GenerateValue(t.GetGenericArguments()[0], tempName);
				PutF("{0}.Add({1});\n", name, tempName);
				Put("} while (Require(']', ',') == ',');\n");
				Put("}\n");
			}
			else if (t.IsArray  && !JsonOptions.ArrayLengthPrefix) {
				PutPart(String.Format("new {0}[0];\n", GetTypeSpec(t.GetElementType())));
				Put("Require('[');\n");
				Put("if (SkipSpacesCarefully() == ']') {\n");
				Put("Require(']');\n");
				Put("}\n");
				Put("else {\n");
				tempCount += 1;
				var tempListName = "tmp" + tempCount.ToString();
				PutF("var {0} = new List<{1}>();\n", tempListName, GetTypeSpec(t.GetElementType()));
				Put("do {\n");
				tempCount += 1;
				var tempName = "tmp" + tempCount.ToString();
				PutF("var {0} = ", tempName);
				GenerateValue(t.GetElementType(), tempName);
				PutF("{0}.Add({1});\n", tempListName, tempName);
				Put("} while (Require(']', ',') == ',');\n");
				PutF("{0} = {1}.ToArray();\n", name, tempListName);
				Put("}\n");
			}
			else if (t.IsArray && JsonOptions.ArrayLengthPrefix) {
				PutPart(String.Format("new {0}[0];\n", GetTypeSpec(t.GetElementType())));
				Put("Require('[');\n");
				Put("if (SkipSpacesCarefully() != ']') {\n");
				tempCount += 1;
				var tempArrayName = "tmp" + tempCount.ToString();
				PutF("var {0} = new {1}[RequireUInt()];\n", tempArrayName, GetTypeSpec(t.GetElementType()));
				tempCount += 1;
				var tempIndexName = "tmp" + tempCount.ToString();
				PutF("for(int {0} = 0; {0} < {1}.Length; ++{0}) {{\n", tempIndexName, tempArrayName);
				Put("Require(',');\n");
				PutF("{0}[{1}] = ", tempArrayName, tempIndexName);
				GenerateValue(t.GetElementType(), String.Format("{0}[{1}]", tempArrayName, tempIndexName));
				Put("}\n");
				PutF("{0} = {1};\n", name, tempArrayName);
				Put("}\n");
				Put("Require(']');\n");
			}
			else if (t.IsClass && Options.ClassNames) {
				PutPart(String.Format("({0})base.FromReaderInt();\n", t.Name));
			}
			else if (t.IsClass && !Options.ClassNames || Utils.IsStruct(t)) {
				PutPart(String.Format("({0}){0}_JsonDeserializer.Instance.FromReader(new {0}(), Reader);\n", t.Name));
			}
			else {
				throw new NotImplementedException(t.Name);
			}
		}

		public void Generate<T>()
		{
			PutF("class {0}_JsonDeserializer : JsonDeserializerGenBase\n", typeof(T).Name);
			Put("{\n");

			PutF("public static new {0}_JsonDeserializer Instance = new {0}_JsonDeserializer();\n", typeof(T).Name);
			Put("\n");

			PutF("public {0}_JsonDeserializer()\n", typeof(T).Name);
			Put("{\n");
			PutF("Options.Assembly = Assembly.Load(\"{0}\");\n", typeof(T).Assembly.FullName);
			foreach (var f in Options.GetType().GetFields()) {
				var v = Utils.CodeValueFormat(f.GetValue(Options));
				if (v != "") // TODO
					PutF("Options.{0} = {1};\n", f.Name, v);
			}
			foreach (var f in JsonOptions.GetType().GetFields()) {
				var v = Utils.CodeValueFormat(f.GetValue(JsonOptions));
				if (v != "") // TODO
					PutF("JsonOptions.{0} = {1};\n", f.Name, v);
			}
			Put("}\n");
			Put("\n");

			Put("public override object FromReaderInt()\n");
			Put("{\n");
			// Since deserializer is dynamically constructed anyway, it is too late to determine object type here.
			PutF("return FromReaderInt(new {0}());\n", typeof(T).Name);
			Put("}\n");
			Put("\n");

			Put("public override object FromReaderIntPartial(string name)\n");
			Put("{\n");
			PutF("return ReadFields(new {0}(), name);\n", typeof(T).Name);
			Put("}\n");
			Put("\n");

			Put("protected override object ReadFields(object obj, string name)\n");
			Put("{\n");
			PutF("var result = ({0})obj;\n", typeof(T).Name);
			tempCount = 0;
			foreach (var yi in Utils.GetYuzuItems(typeof(T), Options)) {
				if (yi.IsOptional) {
					PutF("if (\"{0}\" == name) {{\n", yi.Name);
					PutF("result.{0} = ", yi.Name);
				}
				else {
					PutF("if (\"{0}\" != name) throw new YuzuException();\n", yi.Name);
					PutF("result.{0} = ", yi.Name);
				}
				GenerateValue(yi.Type, "result." + yi.Name);
				Put("name = GetNextName(false);\n");
				if (yi.IsOptional)
					Put("}\n");
			}
			Put("Require('}');\n");
			Put("return result;\n");
			Put("}\n");
			Put("}\n");
			Put("\n");
		}

		public override object FromReaderInt()
		{
			return FromReaderIntGenerated();
		}

		public override object FromReaderInt(object obj)
		{
			return FromReaderIntGenerated(obj);
		}
	}
}
