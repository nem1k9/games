using System;
using System.Text;

namespace Gnomes.Core.Protocol
{
    /// <summary>Little-endian growable byte writer.</summary>
    public sealed class NetWriter
    {
        byte[] buf;
        int len;

        public NetWriter(int capacity = 256) { buf = new byte[capacity]; }

        public int Length => len;
        public byte[] Buffer => buf;
        public void Reset() => len = 0;

        public byte[] ToArray()
        {
            var a = new byte[len];
            System.Buffer.BlockCopy(buf, 0, a, 0, len);
            return a;
        }

        void Ensure(int n)
        {
            if (len + n <= buf.Length) return;
            int cap = buf.Length * 2;
            while (cap < len + n) cap *= 2;
            Array.Resize(ref buf, cap);
        }

        public void U8(byte v) { Ensure(1); buf[len++] = v; }
        public void Bool(bool v) => U8(v ? (byte)1 : (byte)0);

        public void U16(ushort v)
        {
            Ensure(2);
            buf[len++] = (byte)v;
            buf[len++] = (byte)(v >> 8);
        }

        public void I16(short v) => U16(unchecked((ushort)v));

        public void I32(int v)
        {
            Ensure(4);
            buf[len++] = (byte)v;
            buf[len++] = (byte)(v >> 8);
            buf[len++] = (byte)(v >> 16);
            buf[len++] = (byte)(v >> 24);
        }

        public void F32(float v)
        {
            int i = BitConverter.SingleToInt32Bits(v);
            I32(i);
        }

        public void Str(string s)
        {
            if (s == null) s = "";
            var b = Encoding.UTF8.GetBytes(s);
            if (b.Length > ushort.MaxValue) throw new ArgumentException("string too long");
            U16((ushort)b.Length);
            Ensure(b.Length);
            System.Buffer.BlockCopy(b, 0, buf, len, b.Length);
            len += b.Length;
        }

        public void Vec(V3 v) { F32(v.x); F32(v.y); F32(v.z); }

        /// <summary>Quaternion packed as 4 x int16 (plenty for rendering).</summary>
        public void Quat(Q4 q)
        {
            float n = (float)Math.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (n < 1e-6f) { q = Q4.identity; n = 1; }
            if (q.w < 0) n = -n; // canonical hemisphere
            I16(PackUnit(q.x / n));
            I16(PackUnit(q.y / n));
            I16(PackUnit(q.z / n));
            I16(PackUnit(q.w / n));
        }

        static short PackUnit(float v) => (short)Math.Round(Math.Max(-1f, Math.Min(1f, v)) * 32767f);

        /// <summary>Angle in radians packed into 16 bits.</summary>
        public void Angle(float radians)
        {
            double a = radians % (2 * Math.PI);
            if (a < 0) a += 2 * Math.PI;
            U16((ushort)Math.Round(a / (2 * Math.PI) * 65535.0));
        }

        public void Bytes(byte[] b, int offset, int count)
        {
            Ensure(count);
            System.Buffer.BlockCopy(b, offset, buf, len, count);
            len += count;
        }
    }

    /// <summary>Reader matching <see cref="NetWriter"/>. Throws <see cref="IndexOutOfRangeException"/> on truncated data.</summary>
    public sealed class NetReader
    {
        readonly byte[] buf;
        int pos;
        readonly int end;

        public NetReader(byte[] data) : this(data, 0, data.Length) { }

        public NetReader(byte[] data, int offset, int count)
        {
            buf = data;
            pos = offset;
            end = offset + count;
        }

        public int Remaining => end - pos;
        public bool AtEnd => pos >= end;

        void Need(int n)
        {
            if (pos + n > end) throw new IndexOutOfRangeException("NetReader: truncated message");
        }

        public byte U8() { Need(1); return buf[pos++]; }
        public bool Bool() => U8() != 0;

        public ushort U16()
        {
            Need(2);
            ushort v = (ushort)(buf[pos] | (buf[pos + 1] << 8));
            pos += 2;
            return v;
        }

        public short I16() => unchecked((short)U16());

        public int I32()
        {
            Need(4);
            int v = buf[pos] | (buf[pos + 1] << 8) | (buf[pos + 2] << 16) | (buf[pos + 3] << 24);
            pos += 4;
            return v;
        }

        public float F32() => BitConverter.Int32BitsToSingle(I32());

        public string Str()
        {
            int n = U16();
            Need(n);
            var s = Encoding.UTF8.GetString(buf, pos, n);
            pos += n;
            return s;
        }

        public V3 Vec() => new V3(F32(), F32(), F32());

        public Q4 Quat() => new Q4(I16() / 32767f, I16() / 32767f, I16() / 32767f, I16() / 32767f);

        public float Angle() => (float)(U16() / 65535.0 * 2 * Math.PI);
    }
}
