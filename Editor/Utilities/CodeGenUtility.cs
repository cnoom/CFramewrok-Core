using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CFramework.Core.Editor.Utilities
{
    public static class CodeGenUtility
    {
        public static readonly HashSet<string> CSharpKeywords = new HashSet<string>(new[]
        {
            "abstract",
            "as",
            "base",
            "bool",
            "break",
            "byte",
            "case",
            "catch",
            "char",
            "checked",
            "class",
            "const",
            "continue",
            "decimal",
            "default",
            "delegate",
            "do",
            "double",
            "else",
            "enum",
            "event",
            "explicit",
            "extern",
            "false",
            "finally",
            "fixed",
            "float",
            "for",
            "foreach",
            "goto",
            "if",
            "implicit",
            "in",
            "int",
            "interface",
            "internal",
            "is",
            "lock",
            "long",
            "namespace",
            "new",
            "null",
            "object",
            "operator",
            "out",
            "override",
            "params",
            "private",
            "protected",
            "public",
            "readonly",
            "ref",
            "return",
            "sbyte",
            "sealed",
            "short",
            "sizeof",
            "stackalloc",
            "static",
            "string",
            "struct",
            "switch",
            "this",
            "throw",
            "true",
            "try",
            "typeof",
            "uint",
            "ulong",
            "unchecked",
            "unsafe",
            "ushort",
            "using",
            "virtual",
            "void",
            "volatile",
            "while",
            "add",
            "alias",
            "ascending",
            "async",
            "await",
            "by",
            "descending",
            "dynamic",
            "equals",
            "from",
            "get",
            "global",
            "group",
            "into",
            "join",
            "let",
            "nameof",
            "not",
            "notnull",
            "on",
            "or",
            "orderby",
            "partial",
            "remove",
            "select",
            "set",
            "unmanaged",
            "value",
            "var",
            "when",
            "where",
            "with",
            "yield"
        }, StringComparer.Ordinal);

        public static string ToIdentifier(string value, string emptyFallback = "_Empty")
        {
            if(string.IsNullOrEmpty(value)) return emptyFallback;

            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char ch in value)
            {
                if(char.IsLetterOrDigit(ch)) builder.Append(ch);
                else builder.Append('_');
            }

            if(builder.Length == 0) builder.Append(emptyFallback);
            else if(char.IsDigit(builder[0])) builder.Insert(0, '_');

            return builder.ToString();
        }

        public static string ToTypeIdentifier(string name, string emptyFallback = "_Empty")
        {
            string id = ToIdentifier(name, emptyFallback);
            if(id.Length > 0) id = char.ToUpperInvariant(id[0]) + (id.Length > 1 ? id.Substring(1) : string.Empty);
            return id;
        }

        public static string MakeUnique(string identifier, ISet<string> used, string separator = "_")
        {
            if(used == null) return identifier;
            string unique = identifier;
            var suffix = 1;
            while (!used.Add(unique))
            {
                unique = string.Concat(identifier, separator, suffix++);
            }
            return unique;
        }

        public static void WriteIfChanged(string filePath, string content)
        {
            if(File.Exists(filePath))
            {
                string old = File.ReadAllText(filePath);
                if(string.Equals(old, content, StringComparison.Ordinal)) return;
            }

            File.WriteAllText(filePath, content, new UTF8Encoding(false));
        }
    }
}