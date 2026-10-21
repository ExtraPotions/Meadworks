using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Small strict JSON reader for mapping files (Core takes no package references). Objects become
    /// <c>Dictionary&lt;string, object?&gt;</c>, arrays <c>List&lt;object?&gt;</c>, numbers double. Bounded size and depth;
    /// every failure is a <see cref="FormatException"/>.
    /// </summary>
    internal static class MiniJson {
        const int MaxLength=1024*1024,MaxDepth=32;
        public static object? Parse(string text){
            if(text==null||text.Length>MaxLength)throw new FormatException("JSON exceeds bounds.");
            int i=0;if(text.Length>0&&text[0]=='﻿')i=1;
            var value=Value(text,ref i,0);Skip(text,ref i);
            if(i!=text.Length)throw new FormatException("Trailing JSON content.");
            return value;
        }
        static void Skip(string s,ref int i){while(i<s.Length&&(s[i]==' '||s[i]=='\t'||s[i]=='\r'||s[i]=='\n'))i++;}
        static object? Value(string s,ref int i,int depth){
            if(depth>MaxDepth)throw new FormatException("JSON nesting too deep.");
            Skip(s,ref i);if(i>=s.Length)throw new FormatException("Unexpected end of JSON.");
            switch(s[i]){
                case '{':return Object(s,ref i,depth);
                case '[':return Array(s,ref i,depth);
                case '"':return String(s,ref i);
                case 't':return Word(s,ref i,"true",true);
                case 'f':return Word(s,ref i,"false",false);
                case 'n':return Word(s,ref i,"null",null);
                default:return Number(s,ref i);
            }
        }
        static object? Word(string s,ref int i,string word,object? value){if(string.CompareOrdinal(s,i,word,0,word.Length)!=0)throw new FormatException("Invalid JSON literal.");i+=word.Length;return value;}
        static Dictionary<string,object?> Object(string s,ref int i,int depth){
            var result=new Dictionary<string,object?>(StringComparer.Ordinal);i++;Skip(s,ref i);
            if(i<s.Length&&s[i]=='}'){i++;return result;}
            while(true){
                Skip(s,ref i);if(i>=s.Length||s[i]!='"')throw new FormatException("Expected a JSON property name.");
                var key=String(s,ref i);Skip(s,ref i);
                if(i>=s.Length||s[i]!=':')throw new FormatException("Expected ':' in JSON object.");i++;
                if(result.ContainsKey(key))throw new FormatException("Duplicate JSON property: "+key);
                result[key]=Value(s,ref i,depth+1);Skip(s,ref i);
                if(i<s.Length&&s[i]==','){i++;continue;}
                if(i<s.Length&&s[i]=='}'){i++;return result;}
                throw new FormatException("Expected ',' or '}' in JSON object.");
            }
        }
        static List<object?> Array(string s,ref int i,int depth){
            var result=new List<object?>();i++;Skip(s,ref i);
            if(i<s.Length&&s[i]==']'){i++;return result;}
            while(true){
                result.Add(Value(s,ref i,depth+1));Skip(s,ref i);
                if(i<s.Length&&s[i]==','){i++;continue;}
                if(i<s.Length&&s[i]==']'){i++;return result;}
                throw new FormatException("Expected ',' or ']' in JSON array.");
            }
        }
        static string String(string s,ref int i){
            var b=new StringBuilder();i++;
            while(i<s.Length){
                var c=s[i++];
                if(c=='"')return b.ToString();
                if(c<0x20)throw new FormatException("Control character in JSON string.");
                if(c!='\\'){b.Append(c);continue;}
                if(i>=s.Length)break;
                var e=s[i++];
                switch(e){
                    case '"':b.Append('"');break;case '\\':b.Append('\\');break;case '/':b.Append('/');break;
                    case 'b':b.Append('\b');break;case 'f':b.Append('\f');break;case 'n':b.Append('\n');break;case 'r':b.Append('\r');break;case 't':b.Append('\t');break;
                    case 'u':
                        if(i+4>s.Length||!int.TryParse(s.Substring(i,4),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out var code))throw new FormatException("Invalid JSON unicode escape.");
                        b.Append((char)code);i+=4;break;
                    default:throw new FormatException("Invalid JSON escape.");
                }
            }
            throw new FormatException("Unterminated JSON string.");
        }
        static double Number(string s,ref int i){
            int start=i;
            while(i<s.Length&&("+-0123456789.eE".IndexOf(s[i])>=0))i++;
            if(start==i||!double.TryParse(s.Substring(start,i-start),NumberStyles.Float,CultureInfo.InvariantCulture,out var number)||double.IsNaN(number)||double.IsInfinity(number))throw new FormatException("Invalid JSON value.");
            return number;
        }
    }
}
