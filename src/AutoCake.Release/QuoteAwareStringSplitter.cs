using System.Collections.Generic;
using System.IO;
using System.Text;

public static class QuoteAwareStringSplitter
{
    // the mono compiler used in Cake totally messes up when giving the quote, even when escaped.
    const char Quote = (char) 0x22;

    /// <summary>
    ///     Splits the provided string on spaces while respecting quoted strings.
    /// </summary>
    /// <param name="text">The string to split.</param>
    /// <returns>The split, individual parts.</returns>
    public static IEnumerable<string> Split(string text)
    {
        return Split(new StringReader(text));
    }

    static IEnumerable<string> Split(StringReader reader)
    {
        while (reader.Peek() != -1)
        {
            var character = (char) reader.Peek();
            switch (character)
            {
                case Quote:
                    yield return ReadQuote(reader);
                    break;
                case ' ':
                    reader.Read();
                    break;
                default:
                    yield return Read(reader);
                    break;
            }
        }
    }

    static string ReadQuote(StringReader reader)
    {
        var accumulator = new StringBuilder();
        accumulator.Append((char) reader.Read());
        while (reader.Peek() != -1)
        {
            var character = (char) reader.Peek();
            if (character == Quote)
            {
                accumulator.Append((char) reader.Read());
                break;
            }
            reader.Read();
            accumulator.Append(character);
        }
        return accumulator.ToString();
    }

    static string Read(StringReader reader)
    {
        var accumulator = new StringBuilder();
        accumulator.Append((char) reader.Read());
        while (reader.Peek() != -1)
            if ((char) reader.Peek() == Quote)
                accumulator.Append(ReadQuote(reader));
            else if ((char) reader.Peek() == ' ')
                break;
            else
                accumulator.Append((char) reader.Read());
        return accumulator.ToString();
    }
}