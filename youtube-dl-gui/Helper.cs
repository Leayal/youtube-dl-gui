﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace youtube_dl_gui
{
    static class Helper
    {
        public static string FirstLetterToUpper(this string str)
        {
            if (str == null)
                return null;

            if (str.Length > 1)
                return char.ToUpper(str[0]) + str.Substring(1);

            return str.ToUpper();
        }
        public static string GetArg(IEnumerable<string> args)
        {
            StringBuilder sb = new StringBuilder();
            bool firstAppend = true;
            foreach (string str in args)
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
            return sb.ToString();
        }

        /// <summary>Finds the location of file that match the search pattern. By default, the search is done along the current directory and in the paths specified by the PATH environment variable.</summary>
        /// <param name="filename">The filename to search for</param>
        /// <exception cref="FileNotFoundException">The search yield no result</exception>
        public static string Where(string filename) => Where(filename, true);

        /// <summary>Finds the location of file that match the search pattern. By default, the search is done along the current directory and in the paths specified by the PATH environment variable.</summary>
        /// <param name="filename">The filename to search for</param>
        /// <param name="throwIfNotFound">Throw <seealso cref="FileNotFoundException"/> if no result</param>
        public static string Where(string filename, bool throwIfNotFound)
        {
            using (Process proc = new Process()
            {
                StartInfo = new ProcessStartInfo("where.exe", filename)
                {
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            })
            {
                proc.Start();
                proc.WaitForExit();

                string textbuffer;

                switch (proc.ExitCode)
                {
                    case 2:
                        textbuffer = proc.StandardOutput.ReadToEnd();
                        if (string.IsNullOrEmpty(textbuffer))
                        {
                            textbuffer = proc.StandardError.ReadToEnd();
                        }
                        throw new System.Exception(textbuffer);
                    case 1:
                        if (throwIfNotFound)
                        {
                            throw new FileNotFoundException();
                        }
                        else
                        {
                            return null;
                        }
                    default:
                        textbuffer = proc.StandardOutput.ReadToEnd();
                        if (string.IsNullOrEmpty(textbuffer))
                        {
                            if (throwIfNotFound)
                            {
                                throw new FileNotFoundException();
                            }
                            else
                            {
                                return null;
                            }
                        }
                        else
                        {
                            if (textbuffer.EndsWith("\r\n", System.StringComparison.Ordinal))
                            {
                                return textbuffer.Remove(textbuffer.Length - 2);
                            }
                            else
                            {
                                return textbuffer;
                            }
                        }
                }
            }
        }

        /// <summary>Finds the location of file that match the search pattern. By default, the search is done along the current directory and in the paths specified by the PATH environment variable.</summary>
        /// <param name="filename">The filename to search for</param>
        /// <exception cref="FileNotFoundException">The search yield no result</exception>
        public static Task<string> WhereAsync(string filename)
        {
            TaskCompletionSource<string> tasksrc = new TaskCompletionSource<string>();
            Process proc = new Process()
            {
                StartInfo = new ProcessStartInfo("where.exe", filename)
                {
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };
            proc.Exited += (sender, e) =>
            {
                string textbuffer;

                switch (proc.ExitCode)
                {
                    case 2:
                        textbuffer = proc.StandardOutput.ReadToEnd();
                        if (string.IsNullOrEmpty(textbuffer))
                        {
                            textbuffer = proc.StandardError.ReadToEnd();
                        }
                        tasksrc.SetException(new System.Exception(textbuffer));
                        break;
                    case 1:
                        tasksrc.SetException(new FileNotFoundException());
                        break;
                    default:
                        textbuffer = proc.StandardOutput.ReadToEnd();
                        if (string.IsNullOrEmpty(textbuffer))
                        {
                            tasksrc.SetException(new FileNotFoundException());
                        }
                        else
                        {
                            if (textbuffer.EndsWith("\r\n", StringComparison.Ordinal))
                            {
                                tasksrc.SetResult(textbuffer.Remove(textbuffer.Length - 2));
                            }
                            else
                            {
                                tasksrc.SetResult(textbuffer);
                            }
                        }
                        break;
                }
                proc.Dispose();
            };
            proc.Start();
            return tasksrc.Task;
        }
    }
}
