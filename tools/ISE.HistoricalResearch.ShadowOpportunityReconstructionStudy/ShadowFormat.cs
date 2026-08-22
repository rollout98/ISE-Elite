using System.Globalization;

global using static ShadowFormat;

internal static class ShadowFormat
{
    public static string F(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
