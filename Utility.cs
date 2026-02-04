
namespace Kosync;

public static class Utility
{
    public static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return string.Empty;

        using var md5 = MD5.Create();
        // KOReader a MD5 obvykle očekávají UTF-8 pro správné kódování speciálních znaků
        byte[] inputBytes = Encoding.UTF8.GetBytes(password);
        byte[] hashBytes = md5.ComputeHash(inputBytes);

        StringBuilder strBuilder = new StringBuilder();
        for (int i = 0; i < hashBytes.Length; i++)
        {
            strBuilder.Append(hashBytes[i].ToString("x2"));
        }

        return strBuilder.ToString();
    }
}
