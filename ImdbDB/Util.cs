using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FxMovies.ImdbDB
{
    public static class Util
    {
        private static readonly IList<Tuple<string, string>> RomanNumbers = new List<Tuple<string, string>>
        {
            //Tuple.Create("I", "1"), // Will also transform "I have..."
            Tuple.Create("II", "2"),
            Tuple.Create("III", "3"),
            Tuple.Create("IV", "4"),
            Tuple.Create("V", "5"),
            Tuple.Create("VI", "6"),
            Tuple.Create("VII", "7"),
            Tuple.Create("VIII", "8"),
            Tuple.Create("IX", "9"),
            Tuple.Create("X", "10"),
        }.AsReadOnly();

        public static string NormalizeTitle(string title)
        {
            title = Regex.Replace(title, @"[^\w\s]", "");
            title = Regex.Replace(title, @"\s+", " ");
            title = title.ToUpperInvariant();
            foreach (var item in RomanNumbers)
                title = title.Replace(item.Item1, item.Item2);
            return title;
        }
    }
}