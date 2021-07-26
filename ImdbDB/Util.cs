using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FxMovies.ImdbDB
{
    public static class Util
    {
        private static readonly IList<Tuple<string, string>> RomanNumbers = new List<Tuple<string, string>>
        {
            Tuple.Create("^(.*) I$", "$1 1"), // Only match the end, to prevent transforming "I have..."
            Tuple.Create("^(.*) II( .*)?$", "$1 2$2"),
            Tuple.Create("^(.*) III( .*)?$", "$1 3$2"),
            Tuple.Create("^(.*) IV( .*)?$", "$1 4$2"),
            Tuple.Create("^(.*) V( .*)?$", "$1 5$2"),
            Tuple.Create("^(.*) VI( .*)?$", "$1 6$2"),
            Tuple.Create("^(.*) VII( .*)?$", "$1 7$2"),
            Tuple.Create("^(.*) VIII( .*)?$", "$1 8$2"),
            Tuple.Create("^(.*) IX( .*)?$", "$1 9$2"),
            Tuple.Create("^(.*) X( .*)?$", "$1 10$2"),
        }.AsReadOnly();

        public static string NormalizeTitle(string title)
        {
            title = title.Normalize();
            title = Regex.Replace(title, @"[^\w\s]", "");
            title = Regex.Replace(title, @"\s+", " ");
            title = title.ToUpperInvariant();
            foreach (var item in RomanNumbers)
                title = Regex.Replace(title, item.Item1, item.Item2);
            //title = System.Text.Encoding.UTF8.GetString(System.Text.Encoding.GetEncoding(28598).GetBytes(title));
            return title;
        }
    }
}