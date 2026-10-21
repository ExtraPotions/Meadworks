using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Strict reader and writer for the Meadworks translation file schema (Wildroot format):
    /// <c>{"schema":1,"strings":{"key":"text"}}</c>. Dependency-free so Core stays testable without Newtonsoft.
    /// Every failure is a <see cref="FormatException"/>.
    /// </summary>
    public static class TranslationJson {
        public const int MaximumBytes=262144,MaximumKeys=4096,MaximumValueLength=4096;
        static readonly Regex KeyShape=new Regex("^[a-z][a-z0-9_]{0,127}$",RegexOptions.CultureInvariant);
        static readonly object[] PlaceholderArguments=Enumerable.Range(0,32).Select(i=>(object)"").ToArray();
        public static bool IsValidKey(string? key)=>key!=null&&KeyShape.IsMatch(key);

        public static IReadOnlyDictionary<string,string> Parse(string json){
            if(json==null||json.Length>MaximumBytes)throw new FormatException("Translation file exceeds 256 KiB.");
            var reader=new Reader(json.TrimStart('﻿'));
            int? schema=null;Dictionary<string,string>? strings=null;
            reader.Expect('{');
            if(!reader.TryConsume('}'))do{
                var name=reader.String();reader.Expect(':');
                if(name=="schema"&&schema==null)schema=reader.Integer();
                else if(name=="strings"&&strings==null)strings=Strings(reader);
                else throw new FormatException("Unsupported translation schema property: "+name);
            }while(reader.TryConsume(','));
            reader.Expect('}');reader.End();
            if(schema!=1||strings==null)throw new FormatException("Unsupported translation schema.");
            return strings;
        }
        static Dictionary<string,string> Strings(Reader reader){
            var result=new Dictionary<string,string>(StringComparer.Ordinal);
            reader.Expect('{');
            if(reader.TryConsume('}'))return result;
            do{
                var key=reader.String();reader.Expect(':');
                if(!IsValidKey(key))throw new FormatException("Invalid translation key: "+key);
                if(result.ContainsKey(key))throw new FormatException("Duplicate translation key: "+key);
                if(result.Count>=MaximumKeys)throw new FormatException("Too many translation keys.");
                var value=reader.String();
                if(value.Length>MaximumValueLength)throw new FormatException("Translation value too long: "+key);
                Placeholders(value);result.Add(key,value);
            }while(reader.TryConsume(','));
            reader.Expect('}');return result;
        }

        /// <summary>Indexed placeholders used by a format string; validates brace syntax (indices 0..31).</summary>
        public static ISet<string> Placeholders(string text){
            try{string.Format(CultureInfo.InvariantCulture,text,PlaceholderArguments);}
            catch(FormatException){throw new FormatException("Invalid placeholder syntax: "+text);}
            var unescaped=text.Replace("{{","").Replace("}}","");
            return new HashSet<string>(Regex.Matches(unescaped,@"\{(\d+)(?:[^{}]*)\}").Cast<Match>().Select(m=>m.Groups[1].Value),StringComparer.Ordinal);
        }

        /// <summary>Writes the schema with keys sorted ordinally, indented for hand editing.</summary>
        public static string Write(IReadOnlyDictionary<string,string> strings){
            var builder=new StringBuilder();builder.Append("{\n  \"schema\": 1,\n  \"strings\": {");
            bool first=true;
            foreach(var pair in strings.OrderBy(p=>p.Key,StringComparer.Ordinal)){
                builder.Append(first?"\n":",\n");first=false;
                builder.Append("    ");Quote(builder,pair.Key);builder.Append(": ");Quote(builder,pair.Value);
            }
            builder.Append(first?"}\n}\n":"\n  }\n}\n");return builder.ToString();
        }
        static void Quote(StringBuilder builder,string value){
            builder.Append('"');
            foreach(var c in value){
                switch(c){
                    case '"':builder.Append("\\\"");break;case '\\':builder.Append("\\\\");break;
                    case '\n':builder.Append("\\n");break;case '\r':builder.Append("\\r");break;case '\t':builder.Append("\\t");break;
                    default:if(c<0x20)builder.Append("\\u").Append(((int)c).ToString("x4",CultureInfo.InvariantCulture));else builder.Append(c);break;
                }
            }
            builder.Append('"');
        }

        sealed class Reader {
            readonly string text;int position;
            public Reader(string text){this.text=text;}
            void Skip(){while(position<text.Length&&(text[position]==' '||text[position]=='\t'||text[position]=='\n'||text[position]=='\r'))position++;}
            public bool TryConsume(char c){Skip();if(position<text.Length&&text[position]==c){position++;return true;}return false;}
            public void Expect(char c){if(!TryConsume(c))throw new FormatException("Expected '"+c+"' at offset "+position+".");}
            public void End(){Skip();if(position!=text.Length)throw new FormatException("Unexpected content after translation object.");}
            public int Integer(){
                Skip();int start=position;while(position<text.Length&&char.IsDigit(text[position]))position++;
                if(start==position||!int.TryParse(text.Substring(start,position-start),NumberStyles.None,CultureInfo.InvariantCulture,out var value))throw new FormatException("Expected integer schema.");
                return value;
            }
            public string String(){
                Skip();if(position>=text.Length||text[position]!='"')throw new FormatException("Expected string at offset "+position+".");
                position++;var builder=new StringBuilder();
                while(true){
                    if(position>=text.Length)throw new FormatException("Unterminated string.");
                    var c=text[position++];
                    if(c=='"')return builder.ToString();
                    if(c<0x20)throw new FormatException("Control character in string.");
                    if(c!='\\'){builder.Append(c);continue;}
                    if(position>=text.Length)throw new FormatException("Unterminated escape.");
                    var e=text[position++];
                    switch(e){
                        case '"':builder.Append('"');break;case '\\':builder.Append('\\');break;case '/':builder.Append('/');break;
                        case 'b':builder.Append('\b');break;case 'f':builder.Append('\f');break;case 'n':builder.Append('\n');break;
                        case 'r':builder.Append('\r');break;case 't':builder.Append('\t');break;
                        case 'u':
                            if(position+4>text.Length||!int.TryParse(text.Substring(position,4),NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out var code))throw new FormatException("Invalid unicode escape.");
                            builder.Append((char)code);position+=4;break;
                        default:throw new FormatException("Invalid escape '\\"+e+"'.");
                    }
                }
            }
        }
    }
}
