using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ES.Sim
{
    /// <summary>
    /// Hash de conteúdo (FNV-1a 64) sobre uma escrita binária canônica: campos
    /// públicos ordenados por nome, floats pelos bits, strings UTF-8, null ≡ vazio.
    /// Host e clientes comparam este valor para recusar builds diferentes.
    /// </summary>
    public static class ContentCanonical
    {
        const ulong Offset = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;

        static readonly Dictionary<Type, FieldInfo[]> FieldCache = new Dictionary<Type, FieldInfo[]>();

        public static ulong Hash(ContentDef def)
        {
            ulong h = Offset;
            Write(ref h, def, typeof(ContentDef));
            return h;
        }

        static void Write(ref ulong h, object value, Type type)
        {
            if (type == typeof(string)) { WriteString(ref h, (string)value ?? ""); return; }
            if (type == typeof(float)) { WriteInt(ref h, BitConverter.SingleToInt32Bits((float)value)); return; }
            if (type == typeof(int)) { WriteInt(ref h, (int)value); return; }
            if (type == typeof(bool)) { WriteInt(ref h, (bool)value ? 1 : 0); return; }

            if (type.IsArray)
            {
                var arr = (Array)value;
                int n = arr?.Length ?? 0;
                WriteInt(ref h, n);
                var elem = type.GetElementType();
                for (int i = 0; i < n; i++) Write(ref h, arr.GetValue(i), elem);
                return;
            }

            if (type.IsClass)
            {
                if (value == null) value = Activator.CreateInstance(type);
                foreach (var f in Fields(type))
                {
                    WriteString(ref h, f.Name);
                    Write(ref h, f.GetValue(value), f.FieldType);
                }
                return;
            }

            throw new NotSupportedException("tipo não suportado no hash de conteúdo: " + type.Name);
        }

        static FieldInfo[] Fields(Type type)
        {
            lock (FieldCache)
            {
                if (FieldCache.TryGetValue(type, out var cached)) return cached;
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
                FieldCache[type] = fields;
                return fields;
            }
        }

        static void WriteString(ref ulong h, string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            WriteInt(ref h, bytes.Length);
            foreach (var b in bytes) { h ^= b; h *= Prime; }
        }

        static void WriteInt(ref ulong h, int v)
        {
            for (int i = 0; i < 4; i++)
            {
                h ^= (byte)(v >> (8 * i));
                h *= Prime;
            }
        }
    }
}
