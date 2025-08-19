// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

namespace Lingotion.Thespeon.Core
{
    /// <summary>
    /// Abstract class for converting numbers to a specific format.
    /// This class is intended to be extended for specific number conversion implementations.
    /// </summary>
    public abstract class NumberConverter
    {
        protected const long MAX_SAFE_INTEGER = 9007199254740991;
        public abstract string ConvertNumber(string input);
    }
}