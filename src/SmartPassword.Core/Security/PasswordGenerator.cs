using System.Security.Cryptography;

namespace SmartPassword.Core.Security;

public sealed record PasswordOptions(
    int Length = 20,
    bool Upper = true,
    bool Lower = true,
    bool Digits = true,
    bool Symbols = true,
    bool AvoidAmbiguous = false);

/// <summary>Cryptographically random passwords (F-50, F-51).</summary>
public static class PasswordGenerator
{
    private const string UpperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string LowerChars = "abcdefghijklmnopqrstuvwxyz";
    private const string DigitChars = "0123456789";
    private const string SymbolChars = "!#$%&*+-=?@^_.:;~";
    private const string Ambiguous = "0O1lI|";

    public static string Generate(PasswordOptions options)
    {
        var classes = new List<string>();
        if (options.Upper) classes.Add(UpperChars);
        if (options.Lower) classes.Add(LowerChars);
        if (options.Digits) classes.Add(DigitChars);
        if (options.Symbols) classes.Add(SymbolChars);
        if (classes.Count == 0) throw new ArgumentException("Vælg mindst én tegntype.");
        if (options.AvoidAmbiguous)
            classes = classes.Select(c => new string(c.Where(ch => !Ambiguous.Contains(ch)).ToArray())).ToList();

        var length = Math.Max(options.Length, classes.Count);
        var chars = new char[length];

        // One character from each selected class, the rest from all of them, then shuffle.
        for (var i = 0; i < classes.Count; i++)
            chars[i] = classes[i][RandomNumberGenerator.GetInt32(classes[i].Length)];
        var all = string.Concat(classes);
        for (var i = classes.Count; i < length; i++)
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        RandomNumberGenerator.Shuffle(chars.AsSpan());

        return new string(chars);
    }
}
