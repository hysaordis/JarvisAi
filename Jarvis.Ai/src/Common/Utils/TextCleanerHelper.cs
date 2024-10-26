namespace Jarvis.Ai.Common.Utils;

using System.Text.RegularExpressions;
public static class TextCleanerHelper
{
    /// <summary>
    /// Rimuove tutti i tag, parentesi quadre, tonde e graffe dal testo
    /// </summary>
    public static string RemoveAllTags(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        // Rimuove testo tra parentesi quadre [...]
        input = Regex.Replace(input, @"\[([^\]]*)\]", string.Empty);
        
        // Rimuove testo tra parentesi tonde (...)
        input = Regex.Replace(input, @"\(([^\)]*)\)", string.Empty);
        
        // Rimuove testo tra parentesi graffe {...}
        input = Regex.Replace(input, @"\{([^\}]*)\}", string.Empty);
        
        // Rimuove tag HTML/XML <...>
        input = Regex.Replace(input, @"<[^>]+>", string.Empty);

        // Opzionale: rimuove tag personalizzati come [[tag]]
        input = Regex.Replace(input, @"\[\[[^\]]+\]\]", string.Empty);

        // Rimuove spazi multipli creati dalla rimozione dei tag
        input = Regex.Replace(input, @"\s+", " ");

        // Rimuove spazi prima della punteggiatura
        input = Regex.Replace(input, @"\s+([.,!?])", "$1");

        // Rimuove spazi all'inizio e alla fine
        return input.Trim();
    }

    /// <summary>
    /// Versione più flessibile che permette di specificare quali tipi di tag rimuovere
    /// </summary>
    public static string RemoveTags(string input, TagRemovalOptions options)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        if (options.HasFlag(TagRemovalOptions.SquareBrackets))
            input = Regex.Replace(input, @"\[([^\]]*)\]", string.Empty);

        if (options.HasFlag(TagRemovalOptions.RoundBrackets))
            input = Regex.Replace(input, @"\(([^\)]*)\)", string.Empty);

        if (options.HasFlag(TagRemovalOptions.CurlyBrackets))
            input = Regex.Replace(input, @"\{([^\}]*)\}", string.Empty);

        if (options.HasFlag(TagRemovalOptions.HtmlTags))
            input = Regex.Replace(input, @"<[^>]+>", string.Empty);

        if (options.HasFlag(TagRemovalOptions.CustomTags))
            input = Regex.Replace(input, @"\[\[[^\]]+\]\]", string.Empty);

        // Pulizia finale
        input = Regex.Replace(input, @"\s+", " ");
        input = Regex.Replace(input, @"\s+([.,!?])", "$1");
        
        return input.Trim();
    }

    /// <summary>
    /// Versione avanzata che permette di preservare il contenuto specifico dentro i tag
    /// </summary>
    public static string RemoveTagsPreserveContent(string input, bool preserveContent = false)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        string pattern = preserveContent ? 
            @"[\[\({<]([^\]\)}>]*)[\]\)}>]" :  // Preserva il contenuto
            @"[\[\({<][^\]\)}>]*[\]\)}>]";     // Rimuove tutto

        var regex = new Regex(pattern);
        input = regex.Replace(input, preserveContent ? "$1" : string.Empty);

        // Pulizia finale
        input = Regex.Replace(input, @"\s+", " ");
        input = Regex.Replace(input, @"\s+([.,!?])", "$1");

        return input.Trim();
    }
}

[Flags]
public enum TagRemovalOptions
{
    None = 0,
    SquareBrackets = 1,
    RoundBrackets = 2,
    CurlyBrackets = 4,
    HtmlTags = 8,
    CustomTags = 16,
    All = SquareBrackets | RoundBrackets | CurlyBrackets | HtmlTags | CustomTags
}

#region Extension Methods
public static class StringExtensions
{
    public static string RemoveTags(this string input) => 
        TextCleanerHelper.RemoveAllTags(input);

    public static string RemoveTags(this string input, TagRemovalOptions options) => 
        TextCleanerHelper.RemoveTags(input, options);

    public static string RemoveTagsPreserveContent(this string input, bool preserveContent = false) => 
        TextCleanerHelper.RemoveTagsPreserveContent(input, preserveContent);
}
#endregion