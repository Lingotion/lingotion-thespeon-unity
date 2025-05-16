// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

// Assets/Converters/IAbbrevationToWordsConverter.cs
namespace Lingotion.Thespeon.Filters
{
    /// <summary>
    /// Interface for converting abbreviations to words.
    /// </summary>
    /// <remarks>
    /// This interface is used to convert abbreviations in a text to their full word forms. It is typically used in conjunction with the Thespeon API to ensure that the input text is properly formatted for synthesis.
    /// </remarks>
    public interface IAbbrevationToWordsConverter
    {
        /// <summary>
        /// Converts abbreviations in the given text to their full word forms.
        /// </summary>
        (string, string) ConvertAbbreviations(string text);
    }
}