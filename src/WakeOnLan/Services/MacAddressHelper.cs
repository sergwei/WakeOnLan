namespace WakeOnLan.Services;

public static class MacAddressHelper
{
    /// <summary>Acepta AA:BB:CC:DD:EE:FF, AA-BB-..., AABB.CCDD.EEFF o AABBCCDDEEFF.</summary>
    public static bool TryParse(string? input, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var hex = new List<char>(12);
        foreach (var c in input.Trim())
        {
            if (Uri.IsHexDigit(c))
                hex.Add(c);
            else if (c is not (':' or '-' or '.' or ' '))
                return false;
        }

        if (hex.Count != 12)
            return false;

        bytes = Convert.FromHexString(new string(hex.ToArray()));
        return true;
    }

    public static string Format(byte[] bytes) => string.Join(":", bytes.Take(6).Select(b => b.ToString("X2")));

    public static string Normalize(string? input) => TryParse(input, out var bytes) ? Format(bytes) : input?.Trim() ?? string.Empty;
}
