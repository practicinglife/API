public static class Totp
{
    public static int Generate(string base32Secret, DateTime? utcNow = null, int digits = 6, int stepSeconds = 30)
    {
        var secretBytes = Base32Decode(base32Secret);
        long counter = (long)Math.Floor(((utcNow ?? DateTime.UtcNow) - DateTime.UnixEpoch).TotalSeconds / stepSeconds);
        var msg = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(msg);
        using var hmac = new System.Security.Cryptography.HMACSHA1(secretBytes);
        var hash = hmac.ComputeHash(msg);
        int offset = hash[^1] & 0x0F;
        int binary =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);
        int otp = binary % (int)Math.Pow(10, digits);
        return otp;
    }

    public static bool Verify(string base32Secret, string userCode, int driftSteps = 1)
    {
        var now = DateTime.UtcNow;
        for (int i = -driftSteps; i <= driftSteps; i++)
        {
            var code = Generate(base32Secret, now.AddSeconds(i * 30));
            if (code.ToString().PadLeft(6, '0') == userCode) return true;
        }
        return false;
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var clean = input.Replace(" ", "").Replace("=", "").ToUpperInvariant();
        var bits = new System.Collections.Generic.List<bool>();
        foreach (var c in clean)
        {
            int val = alphabet.IndexOf(c);
            if (val < 0) throw new FormatException("Invalid Base32");
            for (int i = 4; i >= 0; i--) bits.Add((val & (1 << i)) != 0);
        }
        var bytes = new System.Collections.Generic.List<byte>();
        for (int i = 0; i + 7 < bits.Count; i += 8)
        {
            byte b = 0;
            for (int j = 0; j < 8; j++)
                if (bits[i + j]) b |= (byte)(1 << (7 - j));
            bytes.Add(b);
        }
        return bytes.ToArray();
    }
}