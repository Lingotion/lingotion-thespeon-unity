// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

// Assets/Converters/IAbbrevationToWordsConverter.cs
namespace Lingotion.Thespeon.Filters
{
    public interface IAbbrevationToWordsConverter
    {
        (string, string) ConvertAbbreviations(string text);
    }
}