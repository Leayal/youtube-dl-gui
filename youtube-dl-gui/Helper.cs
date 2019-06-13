using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lamie.Environment;

namespace youtube_dl_gui
{
    static class Helper
    {
        public static string FirstLetterToUpper(this string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return str;

            if (str.Length == 1)
            {
                return str.ToUpper();
            }
            else
            {
                return char.ToUpper(str[0]) + str.Substring(1);
            }
        }
        public static string GetArg(IEnumerable<string> args)
        {
            StringBuilder sb = new StringBuilder();
            bool firstAppend = true;
            foreach (string str in args)
            {
                if (!string.IsNullOrEmpty(str))
                {
                    if (firstAppend)
                    {
                        firstAppend = false;
                    }
                    else
                    {
                        sb.Append(' ');
                    }
                    if (str.IndexOf(' ') == -1)
                    {
                        sb.Append(str);
                    }
                    else
                    {
                        sb.Append('"');
                        sb.Append(str);
                        sb.Append('"');
                    }
                }
            }
            return sb.ToString();
        }

        /// <summary>Finds the location of file that match the search pattern. By default, the search is done along the current directory and in the paths specified by the PATH environment variable.</summary>
        /// <param name="filename">The filename to search for</param>
        /// <exception cref="FileNotFoundException">The search yield no result</exception>
        public static string Where(string filename) => Where(filename, true);

        /// <summary>Finds the location of file that match the search pattern. By default, the search is done along the current directory and in the paths specified by the PATH environment variable.</summary>
        /// <param name="filename">The filename to search for</param>
        /// <param name="throwIfNotFound">Throw <seealso cref="FileNotFoundException"/> if no result</param>
        /// <remarks>Too lazy, that's why this method is still here.</remarks>
        public static string Where(string filename, bool throwIfNotFound)
        {
            var paths = EnvironmentPath.Get();
            paths.SafeSearchMode = false;
            if (throwIfNotFound)
            {
                return paths.SearchForExact(filename, true);
            }
            else
            {
                try
                {
                    return paths.SearchForExact(filename, true);
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
            }
        }

        /// <summary>Finds the location of file that match the search pattern. By default, the search is done along the current directory and in the paths specified by the PATH environment variable.</summary>
        /// <param name="filename">The filename to search for</param>
        /// <exception cref="FileNotFoundException">The search yield no result</exception>
        /// <remarks>Too lazy, that's why this method is still here.</remarks>
        public static Task<string> WhereAsync(string filename) => Task.Run(() =>
        {
            var paths = EnvironmentPath.Get();
            paths.SafeSearchMode = false;
            return paths.SearchForExact(filename);
        });
    }
}
