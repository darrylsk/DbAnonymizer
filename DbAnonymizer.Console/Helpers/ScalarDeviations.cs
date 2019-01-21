using System;
using System.Linq;

namespace DbAnonymizer.Console.Helpers
{
    public enum StringStyles
    {
        AlphaLower,
        AlphaMixed,
        AlphaUpper,
        AlphaNumeric,
        Numeric,
        Phone,
        Email
    }

    public static class ScalarDeviations
    {
        private static Random _random;
        private static Random Random => _random ?? (_random = new Random());
        private static (char[] source, int size) Email { get; set; }
        private static (char[] source, int size) Phone { get; set; }
        private static (char[] source, int size) Numeric { get; set; }
        private static (char[] source, int size) AlphaNumeric { get; set; }
        private static (char[] source, int size) AlphaUpper { get; set; }
        private static (char[] source, int size) AlphaMixed { get; set; }
        private static (char[] source, int size) AlphaLower { get; set; }

        public static string RandomStringOfLength(this int length, StringStyles style)
        {
            var lowerCaseAscii = (lower: 97, upper: 122);
            var upperCaseAscii = (lower: 65, upper: 90);
            var digitsAscii = (lower: 48, upper: 57);

            (char[] source, int size) arrayTuple;
            switch (style)
            {
                case StringStyles.AlphaLower:
                    arrayTuple = AlphaLower.size == 0 ? (AlphaLower = GetSourceArray(lowerCaseAscii)) : AlphaLower;
                    break;
                case StringStyles.AlphaMixed:
                    arrayTuple = AlphaMixed.size == 0 ? (AlphaMixed = GetSourceArray(lowerCaseAscii, upperCaseAscii)) : AlphaMixed;
                    break;
                case StringStyles.AlphaUpper:
                    arrayTuple = AlphaUpper.size == 0 ? (AlphaUpper = GetSourceArray(upperCaseAscii)) : AlphaUpper;
                    break;
                case StringStyles.AlphaNumeric:
                    arrayTuple = AlphaNumeric.size == 0 ? (AlphaNumeric = GetSourceArray(lowerCaseAscii, upperCaseAscii, digitsAscii)) : AlphaNumeric;
                    break;
                case StringStyles.Numeric:
                    arrayTuple = Numeric.size == 0 ? (Numeric = GetSourceArray(digitsAscii)) : Numeric;
                    break;
                case StringStyles.Phone:
                    arrayTuple = Phone.size == 0 ? (Phone = GetSourceArray(digitsAscii)) : Phone;
                    break;
                case StringStyles.Email:
                    arrayTuple = Email.size == 0 ? (Email = GetSourceArray(lowerCaseAscii)) : Email;
                    break;
                default:
                    arrayTuple = AlphaLower.size == 0 ? (AlphaLower = GetSourceArray(lowerCaseAscii)) : AlphaLower;
                    break;
            }


            char[] characterArray = new char[length];
            //var rnd = new Random();
            for (int n = 0; n < length; n++)
            {
                characterArray[n] = arrayTuple.source[Random.Next(arrayTuple.size)];
            }

            if (style == StringStyles.Email)
            {
                characterArray[length / 2] = '@';
                characterArray[length - 4] = '.';
            }

            if (style == StringStyles.Phone)
            {
                characterArray[0] = '(';
                characterArray[4] = ')';
                characterArray[5] = ' ';
                characterArray[9] = '-';
            }

            var result = string.Join(null, characterArray);
            return result;
        }

        public static int? Tweak(this int? source)
        {
            if (source.HasValue == false) return null;
            var varyRange = source.Value / 5;
            return Random.Next(source.Value - varyRange, source.Value + varyRange);
        }

        public static int Tweak(this int source)
        {
            var varyRange = source / 5;
            return Random.Next(source - varyRange, source + varyRange);
        }

        public static decimal? Tweak(this decimal? source)
        {
            if (source.HasValue == false) return null;

            var varyRange = (int)source * 20;
            var variation = Random.Next(-varyRange, varyRange);

            return source.Value + variation / 100M;
        }

        public static decimal Tweak(this decimal source)
        {
            var varyRange = (int) source * 20;

            var variation = Random.Next(-varyRange, varyRange);

            return source + variation/100M;
        }

        public static DateTime Tweak(this DateTime source)
        {

            var yearVariation = Random.Next(-3, 0);
            var monthVariation = Random.Next(-12, 0);
            var dayVariation = Random.Next(-30, 0);
            var hourVariation = Random.Next(-24, 0);
            var minuteVariation = Random.Next(-60, 0);

            return source.AddYears(yearVariation).AddMonths(monthVariation)
                .AddDays(dayVariation).AddHours(hourVariation).AddMinutes(minuteVariation);
        }

        private static (char[] source, int length) GetSourceArray(params (int lower, int upper)[] rangeList)
        {
            var sourceArrayLength = rangeList.Sum(x => x.upper - x.lower + 1);
            var sourceArray = new char[sourceArrayLength];

            var n = 0;
            foreach (var range in rangeList)
            {
                for (int asciiIndex = range.lower; asciiIndex <= range.upper; asciiIndex++)
                {
                    sourceArray[n++] = (char)asciiIndex;
                }
            }

            return (sourceArray, sourceArrayLength);
        }
    }
}
