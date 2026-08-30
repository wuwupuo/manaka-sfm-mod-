using System;
using System.Text;

namespace SFMOnline
{
    // 字符串加密/解密：关键敏感字符串（服务器地址、密钥、协议参数）编译为密文，
    // 运行时解密。不混淆任何类型/方法，保证 Unity 生命周期稳定。
    internal static class CryptoText
    {
        // 固定混淆 key（异或 + 置换），不依赖 .NET 加密 API，稳定且轻量
        private static readonly byte[] Key = {
            0x5A, 0x2B, 0x8C, 0x1F, 0xE4, 0x73, 0x99, 0x0D,
            0x36, 0xF7, 0x48, 0xA2, 0x6D, 0xC1, 0x85, 0x29
        };

        public static string Enc(string plain)
        {
            if (plain == null) return "";
            byte[] data = Encoding.UTF8.GetBytes(plain);
            byte[] outBuf = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                byte k = Key[i % Key.Length];
                byte b = (byte)(data[i] ^ k);
                b = (byte)((b + i) & 0xFF);
                outBuf[i] = b;
            }
            return Convert.ToBase64String(outBuf);
        }

        public static string Dec(string cipher)
        {
            if (string.IsNullOrEmpty(cipher)) return "";
            try
            {
                byte[] data = Convert.FromBase64String(cipher);
                byte[] outBuf = new byte[data.Length];
                for (int i = 0; i < data.Length; i++)
                {
                    byte b = data[i];
                    b = (byte)((b - i) & 0xFF);
                    byte k = Key[i % Key.Length];
                    outBuf[i] = (byte)(b ^ k);
                }
                return Encoding.UTF8.GetString(outBuf);
            }
            catch { return ""; }
        }

        // 兼容旧明文（未加密时原样返回），便于逐步迁移
        public static string Str(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            // 以 "enc:" 开头视为密文
            if (s.Length > 4 && s[0] == 'e' && s[1] == 'n' && s[2] == 'c' && s[3] == ':')
                return Dec(s.Substring(4));
            return s;
        }
    }
}
