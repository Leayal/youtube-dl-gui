using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;
using System.Collections.ObjectModel;

namespace Lamie.Environment
{
    /// <summary>Represents a collection of paths (case-insensitive). Not thread-safe.</summary>
    public sealed class EnvironmentPath : IList<string>, ICollection<string>, IEnumerable<string>, IEnumerable, IReadOnlyList<string>, IReadOnlyCollection<string>
    {
        const string EnvironmentName = "PATH";

        private static readonly string CurrentExecutingAssemblyDirectory = (new Func<string>(() => 
        {
            var _assembly = Assembly.GetEntryAssembly().Location;

            if (!string.IsNullOrWhiteSpace(_assembly))
            {
                if (Uri.TryCreate(_assembly, UriKind.Absolute, out var theUri))
                {
                    if (theUri.IsFile)
                    {
                        return Path.GetDirectoryName(theUri.LocalPath);
                    }
                }
                else
                {
                    return Path.GetDirectoryName(_assembly);
                }
            }
            return null;
        }))();

        /// <summary>Initializes a new instance of the <seealso cref="EnvironmentPath"/> class that contains all paths from current process.</summary>
        /// <returns>The <seealso cref="EnvironmentPath"/> instance that contains all paths from current process</returns>
        public static EnvironmentPath Get() => Get(EnvironmentVariableTarget.Process);

        /// <summary>Initializes a new instance of the <seealso cref="EnvironmentPath"/> class that contains all paths from specific environment target.</summary>
        /// <param name="target">The environment target to copy.</param>
        /// <returns>The <seealso cref="EnvironmentPath"/> instance that contains all paths from specific environment target.</returns>
        public static EnvironmentPath Get(EnvironmentVariableTarget target) => Parse(System.Environment.GetEnvironmentVariable(EnvironmentName, target));

        /// <summary>Initializes a new instance of the <seealso cref="EnvironmentPath"/> class that contains all paths from specific <seealso cref="ProcessStartInfo"/>.</summary>
        /// <param name="startInfo">The <seealso cref="ProcessStartInfo"/> to copy %PATH% from.</param>
        /// <returns>The <seealso cref="EnvironmentPath"/> instance that contains all paths from specific <seealso cref="ProcessStartInfo"/> target.</returns>
        public static EnvironmentPath Get(ProcessStartInfo startInfo)
        {
            if (startInfo.EnvironmentVariables.ContainsKey(EnvironmentName))
            {
                return new EnvironmentPath(Parse(startInfo.EnvironmentVariables[EnvironmentName]));
            }
            return null;
        }

        /// <summary>Sets %PATH% value of current process according to the <seealso cref="EnvironmentPath"/> instance.</summary>
        /// <param name="environmentPath">The instance to set PATH</param>
        public static void Set(EnvironmentPath environmentPath) => Set(environmentPath, EnvironmentVariableTarget.Process);

        /// <summary>Sets %PATH% value to specific environment target according to the <seealso cref="EnvironmentPath"/> instance.</summary>
        /// <param name="environmentPath">The instance to set PATH</param>
        /// <param name="target">Environment target to set</param>
        public static void Set(EnvironmentPath environmentPath, EnvironmentVariableTarget target) => System.Environment.SetEnvironmentVariable(EnvironmentName, environmentPath.ToString(), target);

        /// <summary>Sets %PATH% value to a <seealso cref="ProcessStartInfo"/>'s environment variable according to the <seealso cref="EnvironmentPath"/> instance.</summary>
        /// <param name="environmentPath">The instance to set PATH</param>
        /// <param name="startInfo"><seealso cref="ProcessStartInfo"/> to set</param>
        public static void Set(EnvironmentPath environmentPath, ProcessStartInfo startInfo)
        {
            if (startInfo.EnvironmentVariables.ContainsKey(EnvironmentName))
            {
                string paths = startInfo.EnvironmentVariables[EnvironmentName];
                if (!string.IsNullOrWhiteSpace(paths))
                {
                    environmentPath.Merge(Parse(paths));
                }
            }
            startInfo.EnvironmentVariables[EnvironmentName] = environmentPath.ToString();
        }

        /// <summary>Initializes a new instance of the <seealso cref="EnvironmentPath"/> class that contains all paths which are parsed from the a string.</summary>
        /// <param name="data">The string to parse</param>
        /// <returns>The <seealso cref="EnvironmentPath"/> instance that contains all paths from given <paramref name="data"/>.</returns>
        public static EnvironmentPath Parse(string data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            int count = data.Length;
            if (count == 0)
            {
                return new EnvironmentPath();
            }
            else if (data.IndexOf(';') == -1)
            {
                return new EnvironmentPath(new string[]
                {
                    data
                });
            }

            EnvironmentPath result = new EnvironmentPath();
            char double_quote = '"',
                splitter = ';',
                currentChar;

            bool isInString = false;
            int starting = 0,
                ending = -1;

            
            unsafe
            {
                fixed (char* c = data)
                {
                    if (c[count - 1] == splitter)
                    {
                        count--;
                    }

                    for (int i = 0; i < count; i++)
                    {
                        currentChar = c[i];
                        if (currentChar == double_quote)
                        {
                            isInString = !isInString;
                            if (isInString)
                            {
                                starting = i + 1;
                            }
                            else
                            {
                                ending = i;
                            }
                        }
                        else if (currentChar == splitter)
                        {
                            if (!isInString)
                            {
                                int length;
                                if (ending == -1)
                                {
                                    length = i - starting;
                                }
                                else
                                {
                                    length = ending - starting;
                                    ending = -1;
                                }
                                if (length != 0)
                                {
                                    string path = new string(c, starting, length); //sb.ToString();

                                    if (!string.IsNullOrWhiteSpace(path))
                                    {
                                        result.Add(path);
                                    }
                                }
                                starting = i + 1;
                            }
                        }
                    }

                    if (starting < (count - 1))
                    {
                        int length;
                        if (c[starting] == double_quote)
                        {
                            starting++;
                            length = (count - 2) - starting;
                        }
                        else
                        {
                            length = (count - 1) - starting;
                        }

                        if (length != 0)
                        {
                            string path = new string(c, starting, length);
                            if (!string.IsNullOrWhiteSpace(path))
                            {
                                result.Add(path);
                            }
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>Searchs for a file that match search pattern.</summary>
        /// <param name="pattern">Pattern to search for. Can contains * or ?.</param>
        /// <returns>The full path to the file(s) which is found.</returns>
        /// <remarks>Short-hand method for <seealso cref="SearchFor(string)"/>.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="pattern"/> is invalid.</exception>
        public static IEnumerable<string> Where(string pattern) => Get().SearchFor(pattern);

        /// <summary>Searchs for file that match a search filename in a specified path.</summary>
        /// <param name="filename">The search string to match against the names of files in path.</param>
        /// <param name="searchInParallel">Determines if the search uses parallel query or not.</param>
        /// <returns>The full path to the file which is found.</returns>
        /// <remarks>Short-hand method for <seealso cref="SearchForExact(string, bool)"/>.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null or empty string.</exception>
        /// <exception cref="ArgumentException">Pattern should only contain valid filename only.</exception>
        /// <exception cref="FileNotFoundException">The search found no results.</exception>
        public static string WhereExact(string filename, bool searchInParallel = true) => Get().SearchForExact(filename, searchInParallel);

        /// <summary>Determines whether a path is in the <seealso cref="EnvironmentPath"/>.</summary>
        /// <param name="item">The path to locate in the <seealso cref="EnvironmentPath"/>.</param>
        /// <returns>true if <paramref name="item"/> is found in the <seealso cref="EnvironmentPath"/>; otherwise, false.</returns>
        public bool Contains(string item)
        {
            return this.dupChecker.Contains(item);
        }

        /// <summary>Copies all elements to a compatible one-dimensional array, starting at the specified index of the target array.</summary>
        /// <param name="array">The one-dimensional <seealso cref="Array"/> that is the destination of the elements copied from <seealso cref="EnvironmentPath"/>. The <seealso cref="Array"/> must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        /// <exception cref="ArgumentNullException">array is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">arrayIndex is less than 0.</exception>
        /// <exception cref="ArgumentException">The number of elements in the source <seealso cref="EnvironmentPath"/> is greater than the available space from arrayIndex to the end of the destination array.</exception>
        public void CopyTo(string[] array, int arrayIndex) => this.paths.CopyTo(array, arrayIndex);

        /// <summary>Copies a range of elements to a compatible one-dimensional array, starting at the specified index of the target array.</summary>
        /// <param name="index">The zero-based index in the <seealso cref="EnvironmentPath"/> at which copying begins.</param>
        /// <param name="array">The one-dimensional <seealso cref="Array"/> that is the destination of the elements copied. The <seealso cref="Array"/> must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        /// <param name="count">The number of elements to copy.</param>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0.-or- <paramref name="arrayIndex"/> is less than 0.-or- <paramref name="count"/> is less than 0.</exception>
        /// <exception cref="ArgumentException"><paramref name="index"/> is equal to or greater than the <seealso cref="Count"/>.-or-The number of elements from <paramref name="index"/> to the end of the <seealso cref="EnvironmentPath"/> is greater than the available space from <paramref name="arrayIndex"/> to the end of the destination array.</exception>
        public void CopyTo(int index, string[] array, int arrayIndex, int count) => this.paths.CopyTo(index, array, arrayIndex, count);


        /// <summary>Copies the entire collection to a compatible one-dimensional array, starting at the beginning of the target array.</summary>
        /// <param name="array">The one-dimensional <seealso cref="Array"/> that is the destination of the elements copied from <seealso cref="EnvironmentPath"/>. The <seealso cref="Array"/> must have zero-based indexing.</param>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is null.</exception>
        /// <exception cref="ArgumentException">The number of elements in the source <seealso cref="EnvironmentPath"/> is greater than the number of elements that the destination array can contain.</exception>
        public void CopyTo(string[] array) => this.paths.CopyTo(array);

        /// <summary>Returns an enumerator that iterates through the <seealso cref="EnvironmentPath"/>.</summary>
        public IEnumerator<string> GetEnumerator() => this.paths.GetEnumerator();

        /// <summary>Returns an enumerator that iterates through the <seealso cref="EnvironmentPath"/>.</summary>
        IEnumerator IEnumerable.GetEnumerator() => this.paths.GetEnumerator();

        private readonly List<string> paths;
        private readonly HashSet<string> dupChecker;

        /// <summary>Gets the number of paths contained in the <seealso cref="EnvironmentPath"/>.</summary>
        public int Count => this.paths.Count;

        /// <summary>Determines if the current instance can be modified. Always false, though.</summary>
        public bool IsReadOnly => false;

        /// <summary>Gets or sets a boolean determine if current instance should use SafeSearch. STRONGLY RECOMMENDED "true".</summary>
        /// <remarks>https://docs.microsoft.com/en-us/windows/desktop/dlls/dynamic-link-library-search-order</remarks>
        public bool SafeSearchMode { get; set; }

        /// <summary>Gets or sets path at the given index.</summary>
        /// <param name="index">The index.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the range of valid indexes.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null or empty string or whitespace string.</exception>
        public string this[int index]
        {
            get => this.paths[index];
            set => this.Insert(index, value);
        }

        /// <summary>Initializes a new instance of the <seealso cref="EnvironmentPath"/> class that has no paths.</summary>
        public EnvironmentPath() : this(GetSafeDllSearchMode()) { }

        /// <summary>Initializes a new instance of the <seealso cref="EnvironmentPath"/> class that has no paths.</summary>
        /// <param name="safeSearch">Determines whether the instance uses SafeDllSearchMode.</param>
        /// <remarks>https://docs.microsoft.com/en-us/windows/desktop/dlls/dynamic-link-library-search-order</remarks>
        public EnvironmentPath(bool safeSearch)
        {
            this.dupChecker = new HashSet<string>(PathComparer.Default);
            this.paths = new List<string>();

            this.SafeSearchMode = safeSearch;
        }

        private static bool GetSafeDllSearchMode()
        {
            RegistryKey key = null;
            try
            {
                key = Registry.LocalMachine.OpenSubKey(Path.Combine("System", "CurrentControlSet", "Control", "Session Manager"), false);
                if (key != null)
                {
                    var keyValue = key.GetValue("SafeDllSearchMode");
                    if (keyValue == null)
                    {
                        return true;
                    }
                    else
                    {
                        if (keyValue is int theInt)
                        {
                            return (theInt != 0);
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    return true;
                }
            }
            catch
            {
                return true;
            }
            finally
            {
                if (key != null)
                {
                    key.Dispose();
                }
            }
        }

        /// <summary>Initializes a new instance of the <seealso cref="EnvironmentPath"/> class that contains elements copied from the specified collection and has sufficient capacity to accommodate the number of elements copied.</summary>
        /// <param name="paths">The collection whose elements are copied to the new list.</param>
        /// <exception cref="ArgumentNullException"><paramref name="paths"/> is null.</exception>
        public EnvironmentPath(IEnumerable<string> paths) : this()
        {
            if (paths is ICollection<string> collection)
            {
                this.paths.Capacity = collection.Count + 1;
            }
            else if (paths is ICollection collection2)
            {
                this.paths.Capacity = collection2.Count + 1;
            }

            foreach (string path in paths)
            {
                this.Add(path);
            }
        }

        /// <summary>Initializes a new instance of the <seealso cref="EnvironmentPath"/> class that contains elements copied from the specified collection and has sufficient capacity to accommodate the number of elements copied.</summary>
        /// <param name="safeSearch">Determines whether the instance uses SafeDllSearchMode.</param>
        /// <param name="paths">The collection whose elements are copied to the new list.</param>
        /// <exception cref="ArgumentNullException"><paramref name="paths"/> is null.</exception>
        /// <remarks>https://docs.microsoft.com/en-us/windows/desktop/dlls/dynamic-link-library-search-order</remarks>
        public EnvironmentPath(bool safeSearch, IEnumerable<string> paths) : this(safeSearch)
        {
            if (paths is ICollection<string> collection)
            {
                this.paths.Capacity = collection.Count + 1;
            }
            else if (paths is ICollection collection2)
            {
                this.paths.Capacity = collection2.Count + 1;
            }

            foreach (string path in paths)
            {
                this.Add(path);
            }
        }

        /// <summary>Adds the specified path.</summary>
        /// <param name="path">The path to add to the set.</param>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is null or empty string or whitespace string.</exception>
        public void Add(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException("File path cannot be null.", nameof(path));
            }
            if (!this.dupChecker.Contains(path))
            {
                this.dupChecker.Add(path);
                this.paths.Add(path);
            }
        }

        /// <summary>Modifies the current <seealso cref="EnvironmentPath"/> object to contain all paths that are present in itself, the specified collection, or both.</summary>
        /// <param name="paths">The collection to compare to the current <seealso cref="EnvironmentPath"/> object.</param>
        /// <exception cref="ArgumentNullException"><paramref name="paths"/> is null.</exception>
        public void Merge(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                this.Add(path);
            }
        }

        /// <summary>Modifies the current <seealso cref="EnvironmentPath"/> object to contain all paths that are present in itself, the specified <paramref name="environmentPath"/>, or both.</summary>
        /// <param name="environmentPath">The <seealso cref="EnvironmentPath"/> to compare to the current one.</param>
        /// <exception cref="ArgumentNullException"><paramref name="environmentPath"/> is null.</exception>
        public void Merge(EnvironmentPath environmentPath)
        {
            foreach (string path in environmentPath.paths)
            {
                this.Add(path);
            }
        }

        /// <summary>Removes all paths.</summary>
        public void Clear()
        {
            this.dupChecker.Clear();
            this.paths.Clear();
        }

        /// <summary>Removes the specified path.</summary>
        /// <param name="path">The path to remove.</param>
        /// <returns>true if the path is successfully found and removed; otherwise, false.</returns>
        public bool Remove(string path)
        {
            this.paths.Remove(path);
            return this.dupChecker.Remove(path);
        }

        /// <summary>Removes all paths that match the conditions defined by the specified predicate.</summary>
        /// <param name="match"><seealso cref="Predicate{string}"/> delegate that defines the conditions of the paths to remove.</param>
        /// <returns>The number of elements that were removed.</returns>
        public int RemoveWhere(Predicate<string> match)
        {
            this.dupChecker.RemoveWhere(match);
            return this.paths.RemoveAll(match);
        }

        /// <summary>Returns a string that represents all the paths.</summary>
        public override string ToString()
        {
            int count = this.Count;
            if (count == 0)
            {
                return string.Empty;
            }
            else if (count == 1)
            {
                return this.paths.First();
            }
            else
            {
                int totalLength = 0;

                for (int i = 0; i < count; i++)
                {
                    if (this.paths[i].IndexOf(';') != -1)
                        totalLength += (this.paths[i].Length + 3);
                    else
                        totalLength += (this.paths[i].Length + 1);
                }

                // StringBuilder sb = new StringBuilder(totalLength);
                string result = new string(char.MinValue, totalLength);
                int currentIndex = 0,
                    sizeOfChar = sizeof(char);

                unsafe
                {
                    fixed (char* dst = result)
                    {
                        foreach (string currentString in this.paths)
                        {
                            if (currentString.IndexOf(';') == -1)
                            {
                                int length = currentString.Length * sizeOfChar;
                                fixed (char* src = currentString)
                                {
                                    Buffer.MemoryCopy(src, dst + currentIndex, length, length);
                                }
                                currentIndex += currentString.Length;
                            }
                            else
                            {
                                dst[currentIndex] = '"';
                                currentIndex++;
                                int length = currentString.Length * sizeOfChar;
                                fixed (char* src = currentString)
                                {
                                    Buffer.MemoryCopy(src, dst + currentIndex, length, length);
                                }
                                currentIndex += currentString.Length;
                                dst[currentIndex] = '"';
                                currentIndex++;
                            }
                            dst[currentIndex] = ';';
                            currentIndex++;
                        }
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Searches for the specified path and returns the zero-based index of the first occurrence within the collection that starts at the specified index and contains the specified number of paths.
        /// </summary>
        /// <param name="item">The path to locate in the collection.</param>
        /// <param name="index">The zero-based starting index of the search.</param>
        /// <param name="count">The number of paths in the section to search.</param>
        /// <returns>The zero-based index of the first occurrence of <paramref name="item"/> within the range of paths in the collection that starts at <paramref name="index"/> and contains <paramref name="count"/> number of paths, if found; otherwise, –1.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the range of valid indexes.-or- <paramref name="count"/> is less than 0.-or- <paramref name="index"/> and <paramref name="count"/> do not specify a valid section in the collection.</exception>
        public int IndexOf(string item, int index, int count) => this.paths.IndexOf(item, index, count);

        /// <summary>
        /// Searches for the specified path and returns the zero-based index of the first occurrence within the range of paths in the collection that extends from the specified <paramref name="index"/> to the last element.
        /// </summary>
        /// <param name="item">The path to locate in the collection.</param>
        /// <param name="index">The zero-based starting index of the search.</param>
        /// <returns>The zero-based index of the first occurrence of <paramref name="item"/> within the range of paths in the collection that extends from <paramref name="index"/> to the last element, if found; otherwise, –1.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the range of valid indexes.</exception>
        public int IndexOf(string item, int index) => this.paths.IndexOf(item, index);

        /// <summary>
        /// Searches for the specified path and returns the zero-based index of the first occurrence within the entire collection.
        /// </summary>
        /// <param name="item">The path to locate in the collection.</param>
        /// <returns>The zero-based index of the first occurrence of <paramref name="item"/> within the entire collection, if found; otherwise, –1.</returns>
        public int IndexOf(string item) => this.paths.IndexOf(item);

        /// <summary>Inserts a path at the specified index.</summary>
        /// <param name="index">The zero-based index at which item should be inserted.</param>
        /// <param name="path">The path to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0.-or- <paramref name="index"/> is greater than <seealso cref="Count"/>.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is null or empty string or whitespace string.</exception>
        public void Insert(int index, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException("File path cannot be null.", nameof(path));
            }
            if (!this.dupChecker.Contains(path))
            {
                this.paths.Insert(index, path);
                this.dupChecker.Add(path);
            }
            else
            {
                this.paths.Remove(path);
                this.paths.Insert(index, path);
            }
        }

        /// <summary>Removes the path at the specified index of the <seealso cref="EnvironmentPath"/>.</summary>
        /// <param name="index">The zero-based index of the element to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0.-or- <paramref name="index"/> is greater than <seealso cref="Count"/>.</exception>
        public void RemoveAt(int index)
        {
            string item = this.paths[index];
            this.paths.RemoveAt(index);
            this.dupChecker.Remove(item);
        }

        /// <summary>Sorts the paths in the entire collection using the default comparer.</summary>
        public void Sort() => this.paths.Sort(StringComparer.OrdinalIgnoreCase);

        /// <summary>Sorts the paths in the entire collection using the specified <seealso cref="Comparison{String}"/>.</summary>
        /// <param name="comparison">The <seealso cref="Comparison{String}"/> to use when comparing paths.</param>
        /// <exception cref="ArgumentNullException"><paramref name="comparison"/> is null.</exception>
        /// <exception cref="ArgumentException">The implementation of <paramref name="comparison"/> caused an error during the sort. For example, comparison might not return 0 when comparing an item with itself.</exception>
        public void Sort(Comparison<string> comparison)
        {
            if (comparison == null)
            {
                this.paths.Sort(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                this.paths.Sort(comparison);
            }
        }

        /// <summary>Sorts the paths in the entire collection using the specified comparer.</summary>
        /// <param name="comparer">The <seealso cref="IComparer{String}"/> implementation to use when comparing strings, or null to use the default comparer <seealso cref="IComparer"/>.</param>
        /// <exception cref="ArgumentException">The implementation of <paramref name="comparer"/> caused an error during the sort. For example, comparer might not return 0 when comparing an item with itself.</exception>
        public void Sort(IComparer<string> comparer) => this.paths.Sort(comparer);

        /// <summary>Searches for the specified path and returns the zero-based index of the last occurrence within the entire collection.</summary>
        /// <param name="path">The path to locate in the collection.</param>
        /// <returns>The zero-based index of the last occurrence of <paramref name="path"/> within the entire the collection, if found; otherwise, –1.</returns>
        public int LastIndexOf(string path) => this.paths.LastIndexOf(path);

        /// <summary>Searches for the specified path and returns the zero-based index of the last occurrence within the range of elements in the collection that extends from the first element to the specified index.</summary>
        /// <param name="path">The path to locate in the collection.</param>
        /// <param name="index">The zero-based starting index of the backward search.</param>
        /// <returns>The zero-based index of the last occurrence of item within the range of elements in the collection that extends from the first element to <paramref name="index"/>, if found; otherwise, –1.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the range of valid indexes for the collection.</exception>
        public int LastIndexOf(string path, int index) => this.paths.LastIndexOf(path, index);

        /// <summary>Searches for the specified object and returns the zero-based index of the last occurrence within the range of elements in the collection that contains the specified number of elements and ends at the specified index.</summary>
        /// <param name="path">The path to locate in the collection.</param>
        /// <param name="index">The zero-based starting index of the backward search.</param>
        /// <param name="count">The number of paths in the section to search.</param>
        /// <returns>The zero-based index of the last occurrence of item within the range of elements in the collection that contains count number of elements and ends at <paramref name="index"/>, if found; otherwise, –1.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the range of valid indexes for the collection.-or- <paramref name="count"/> is less than 0.-or- <paramref name="index"/> and <paramref name="count"/> do not specify a valid section in the collection.</exception>
        public int LastIndexOf(string path, int index, int count) => this.paths.LastIndexOf(path, index, count);

        /// <summary>Returns a read-only <seealso cref="ReadOnlyCollection{String}"/> wrapper for the current collection.</summary>
        /// <returns>An object that acts as a read-only wrapper around the current <seealso cref="EnvironmentPath"/>.</returns>
        public ReadOnlyCollection<string> AsReadOnly() => this.paths.AsReadOnly();
        #region "WHERE function"

        /// <summary>Searchs for a file that match search pattern.</summary>
        /// <param name="pattern">Pattern to search for. Can contains * or ?.</param>
        /// <returns>The full path to the file(s) which is found.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="pattern"/> is invalid.</exception>
        public IEnumerable<string> SearchFor(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                throw new ArgumentNullException(nameof(pattern));
            }
            if (pattern.IndexOf(Path.DirectorySeparatorChar) != -1 || pattern.IndexOf(Path.AltDirectorySeparatorChar) != -1)
            {
                throw new ArgumentException("Pattern should only contain valid filename only. Not a file path.", nameof(pattern));
            }

            bool isSafeSearch = this.SafeSearchMode;
            HashSet<string> searchedPlaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string currentPlace = CurrentExecutingAssemblyDirectory;

            if (currentPlace != null)
            {
                searchedPlaces.Add(currentPlace);
                foreach (string filename in TryOpenSearch(currentPlace, pattern))
                    yield return filename;
            }

            if (!isSafeSearch)
            {
                currentPlace = Directory.GetCurrentDirectory();
                searchedPlaces.Add(currentPlace);
                foreach (string filename in TryOpenSearch(currentPlace, pattern))
                    yield return filename;
            }

            currentPlace = System.Environment.SystemDirectory;
            searchedPlaces.Add(currentPlace);
            foreach (string filename in TryOpenSearch(currentPlace, pattern))
                yield return filename;

            // Skipped 16-bit system directory. Because I don't even know how to get its path.....

            currentPlace = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows, System.Environment.SpecialFolderOption.DoNotVerify);
            searchedPlaces.Add(currentPlace);
            foreach (string filename in TryOpenSearch(currentPlace, pattern))
                yield return filename;

            if (isSafeSearch)
            {
                currentPlace = Directory.GetCurrentDirectory();
                searchedPlaces.Add(currentPlace);
                foreach (string filename in TryOpenSearch(currentPlace, pattern))
                    yield return filename;
            }

            int count = this.Count;
            if (count != 0)
            {
                foreach (string path in this.paths)
                {
                    string fullpath = Path.GetFullPath(path);
                    if (!searchedPlaces.Contains(fullpath))
                    {
                        searchedPlaces.Add(fullpath);
                        foreach (string filename in TryOpenSearch(fullpath, pattern))
                            yield return filename;
                    }
                }
            }
        }

        private static IEnumerable<string> TryOpenSearch(string path, string pattern)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    return Directory.EnumerateFiles(path, pattern, SearchOption.TopDirectoryOnly);
                }
            }
            catch { }
            return Enumerable.Empty<string>();
        }

        /// <summary>Searchs for file that match a search filename in a specified path.</summary>
        /// <param name="filename">The search string to match against the names of files in path.</param>
        /// <param name="searchInParallel">Determines if the search uses parallel query or not.</param>
        /// <returns>The full path to the file which is found.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="filename"/> is null or empty string.</exception>
        /// <exception cref="ArgumentException">Pattern should only contain valid filename only.</exception>
        /// <exception cref="FileNotFoundException">The search found no results.</exception>
        public string SearchForExact(string filename, bool searchInParallel = true)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentNullException(nameof(filename));
            }
            if (filename.IndexOf(Path.DirectorySeparatorChar) != -1 || filename.IndexOf(Path.AltDirectorySeparatorChar) != -1 || filename.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
            {
                throw new ArgumentException("Pattern should only contain valid filename only. Not a file path.", nameof(filename));
            }

            bool isSafeSearch = this.SafeSearchMode;

            string tmpFilename;
            if (CurrentExecutingAssemblyDirectory != null)
            {
                tmpFilename = Path.Combine(CurrentExecutingAssemblyDirectory, filename);
                if (File.Exists(tmpFilename))
                    return tmpFilename;
            }

            if (!isSafeSearch)
            {
                tmpFilename = Path.Combine(Directory.GetCurrentDirectory(), filename);
                if (File.Exists(tmpFilename))
                    return tmpFilename;
            }

            tmpFilename = Path.Combine(System.Environment.SystemDirectory, filename);
            if (File.Exists(tmpFilename))
                return tmpFilename;

            tmpFilename = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows, System.Environment.SpecialFolderOption.DoNotVerify), filename);
            if (File.Exists(tmpFilename))
                return tmpFilename;

            if (isSafeSearch)
            {
                tmpFilename = Path.Combine(Directory.GetCurrentDirectory(), filename);
                if (File.Exists(tmpFilename))
                    return tmpFilename;
            }

            int count = this.Count;

            if (count != 0)
            {
                if (searchInParallel)
                {
                    ConcurrentDictionary<int, string> results = new ConcurrentDictionary<int, string>();
                    Parallel.For(0, this.paths.Count - 1, new ParallelOptions() { MaxDegreeOfParallelism = Math.Min(Math.Min(4, System.Environment.ProcessorCount), count) }, (index, loopstate) =>
                    {
                        string current = Path.Combine(this.paths[index], filename);
                        if (File.Exists(current))
                        {
                            results.TryAdd(index, Path.GetFullPath(current));
                            try
                            {
                                loopstate.Break();
                            }
                            catch (InvalidOperationException) { }
                        }
                    });

                    if (results.Count != 0)
                    {
                        if (results.Count == 1)
                        {
                            return results.Values.First();
                        }
                        else if (results.TryGetValue(results.Keys.Min(), out var result))
                        {
                            return result;
                        }
                    }
                }
                else
                {
                    string current;
                    foreach (string path in this.paths)
                    {
                        current = Path.Combine(path, filename);
                        if (File.Exists(current))
                        {
                            return Path.GetFullPath(current);
                        }
                    }
                }
            }

            throw new FileNotFoundException();
        }

        #endregion

        class PathComparer : StringComparer
        {
            public static readonly PathComparer Default = new PathComparer();

            private PathComparer() : base() { }

            public override int Compare(string x, string y)
            {
                int isAlreadyEqual = StringComparer.OrdinalIgnoreCase.Compare(x, y);
                if (isAlreadyEqual == 0)
                {
                    return StringComparer.OrdinalIgnoreCase.Compare(Path.GetFullPath(x), Path.GetFullPath(y));
                }
                else
                {
                    return isAlreadyEqual;
                }
            }

            public override bool Equals(string x, string y)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(x, y))
                {
                    return true;
                }
                else
                {
                    return StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(x), Path.GetFullPath(y));
                }
            }

            public override int GetHashCode(string obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
        }
    }
}
