global using static ShadowFormat;

using System.Globalization;

internal static class ShadowFormat
{
    public static string F(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
