using System.Text;
using System.Text.RegularExpressions;
using Humanizer;

namespace WebScrapping._SS
{
    public static class Extensions
    {
        public static string Change(this string? value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var cm = value.Split(' ').ToList();
            var sb = new StringBuilder();
            foreach (var c in cm)
            {
                if (c.Length > 3)
                {
                    var cm1 = c.Split('-');
                    if (cm1.Length > 1)
                    {
                        foreach (var c1 in cm1)
                        {
                            if (c1.Length > 3)
                            {
                                sb.Append(CheckDigit(c1));
                                sb.Append('-');
                            }
                            else
                            {
                                sb.Append(c1.Transform(To.UpperCase));
                                sb.Append('-');
                            }
                        }

                        sb.Remove(sb.Length - 1, 1);
                    }
                    else
                    {
                        sb.Append(CheckDigit(c));
                        sb.Append(' ');
                    }
                }
                else
                {
                    sb.Append(c.Transform(To.UpperCase));
                    sb.Append(' ');
                }
            }

            return sb.Remove(sb.Length - 1, 1).ToString();
        }

        private static string CheckDigit(string c)
        {
            if (Regex.Match(c, "\\w+\\d+", RegexOptions.IgnoreCase).Success)
            {
                return c.Transform(To.UpperCase);
            }

            return c.Transform(To.LowerCase, To.TitleCase);
        }
    }
}
