using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace DbAnonymizer.Console.Helpers
{
    public static class StringExtentions
    {
        public static bool IsNumeric(this string value)
        {
            return value.ToCharArray().All(Char.IsNumber);
        }

        public static bool IsAlphameric(this string value)
        {
            return value.ToCharArray().All(Char.IsLetter);
        }

        public static string RandomOfLength(int length)
        {
            var rnd = new Random();
            var characterArray = new char[length];
            var s = "";
            for (var n = 0; n < length; n++)
            {
                characterArray[n] = (char)rnd.Next(97, 122);
                var c = (char) rnd.Next(97, 122);
                s = s + c;
            }

            var result = string.Join(null, characterArray);
            return result;
        }
    }
}
