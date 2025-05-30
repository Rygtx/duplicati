// Copyright (C) 2025, The Duplicati Team
// https://duplicati.com, hello@duplicati.com
// 
// Permission is hereby granted, free of charge, to any person obtaining a 
// copy of this software and associated documentation files (the "Software"), 
// to deal in the Software without restriction, including without limitation 
// the rights to use, copy, modify, merge, publish, distribute, sublicense, 
// and/or sell copies of the Software, and to permit persons to whom the 
// Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in 
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS 
// OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE.

using System;
using System.IO;
using System.Collections.Generic;
using Duplicati.Library.Utility;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Duplicati.Library.Common.IO;
using NUnit.Framework;
using Duplicati.Library.Interface;
using System.Data;
using System.Text;
using Duplicati.Library.Main.Database;

namespace Duplicati.UnitTest
{
    public static class TestUtils
    {
        public static Dictionary<string, string> DefaultOptions
        {
            get
            {
                var opts = new Dictionary<string, string>();

                string auth_password = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "unittest_authpassword.txt");
                if (System.IO.File.Exists(auth_password))
                    opts["auth-password"] = File.ReadAllText(auth_password).Trim();

                return opts;
            }
        }

        public static async Task GrowingFile(string testFile, CancellationToken token)
        {
            try
            {
                var str = new string('*', 50);
                while (true)
                {
                    if (token.IsCancellationRequested)
                    {
                        continue;
                    }
                    File.AppendAllText(testFile, str);
                    await Task.Delay(18, token).ConfigureAwait(false);
                }
            }
            finally
            {
                if (File.Exists(testFile))
                {
                    File.Delete(testFile);
                }
            }
        }

        public static string GetDefaultTarget(string other = null)
        {
            string alttarget = System.IO.Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "unittest_target.txt");

            if (File.Exists(alttarget))
                return File.ReadAllText(alttarget).Trim();
            else if (other != null)
                return other;
            else
                using (var tf = new Library.Utility.TempFolder())
                {
                    tf.Protected = true;
                    return "file://" + tf;
                }
        }

        /// <summary>
        /// Recursively copy a directory to another location.
        /// </summary>
        /// <param name="sourcefolder">Source directory path</param>
        /// <param name="targetfolder">Destination directory path</param>
        public static void CopyDirectoryRecursive(string sourcefolder, string targetfolder)
        {
            sourcefolder = Util.AppendDirSeparator(sourcefolder);

            var work = new Queue<string>();
            work.Enqueue(sourcefolder);

            var timestampfailures = 0;

            while (work.Count > 0)
            {
                var c = work.Dequeue();

                var t = Path.Combine(targetfolder, c.Substring(sourcefolder.Length));

                if (!Directory.Exists(t))
                    Directory.CreateDirectory(t);

                try { Directory.SetCreationTimeUtc(t, Directory.GetCreationTimeUtc(c)); }
                catch (Exception ex)
                {
                    if (timestampfailures++ < 20)
                        Console.WriteLine("Failed to set creation time on dir {0}: {1}", t, ex.Message);
                }

                try { Directory.SetLastWriteTimeUtc(t, Directory.GetLastWriteTimeUtc(c)); }
                catch (Exception ex)
                {
                    if (timestampfailures++ < 20)
                        Console.WriteLine("Failed to set write time on dir {0}: {1}", t, ex.Message);
                }


                foreach (var n in Directory.EnumerateFiles(c))
                {
                    var tf = Path.Combine(t, Path.GetFileName(n));
                    File.Copy(n, tf, true);
                    try { File.SetCreationTimeUtc(tf, System.IO.File.GetCreationTimeUtc(n)); }
                    catch (Exception ex)
                    {
                        if (timestampfailures++ < 20)
                            Console.WriteLine("Failed to set creation time on file {0}: {1}", n, ex.Message);
                    }
                    try { File.SetLastWriteTimeUtc(tf, System.IO.File.GetLastWriteTimeUtc(n)); }
                    catch (Exception ex)
                    {
                        if (timestampfailures++ < 20)
                            Console.WriteLine("Failed to set write time on file {0}: {1}", n, ex.Message);
                    }
                }

                foreach (var n in Directory.EnumerateDirectories(c))
                    work.Enqueue(n);
            }

            if (timestampfailures > 20)
                Console.WriteLine("Encountered additional {0} timestamp errors!", timestampfailures);
        }

        /// <summary>
        /// Asserts that the two directory trees are equivalent; i.e.,
        /// that they they contain the same directories and files, recursively.
        /// </summary>
        /// <param name="expectedDir">The expected directory tree.</param>
        /// <param name="actualDir">The actual directory tree.</param>
        /// <param name="verifymetadata">True to also compare file metadata.</param>
        /// <param name="contextMessage">Context information to include in an assert message.</param>
        public static void AssertDirectoryTreesAreEquivalent(string expectedDir, string actualDir, bool verifymetadata, string contextMessage)
        {
            var localMessage = $"{contextMessage}, in directories {expectedDir} and {actualDir}";
            // Assert that expectedDir and actualDir contain the same directories
            var expectedSubdirs = SystemIO.IO_OS.EnumerateDirectories(expectedDir).OrderBy(SystemIO.IO_OS.PathGetFileName);
            var actualSubdirs = SystemIO.IO_OS.EnumerateDirectories(actualDir).OrderBy(SystemIO.IO_OS.PathGetFileName);
            Assert.That(expectedSubdirs.Select(SystemIO.IO_OS.PathGetFileName), Is.EquivalentTo(actualSubdirs.Select(SystemIO.IO_OS.PathGetFileName)), localMessage);
            // Recursively compare the contained directories
            var expectedSubdirsEnumerator = expectedSubdirs.GetEnumerator();
            var actualSubdirsEnumerator = actualSubdirs.GetEnumerator();
            while (expectedSubdirsEnumerator.MoveNext() && actualSubdirsEnumerator.MoveNext())
            {
                AssertDirectoryTreesAreEquivalent(expectedSubdirsEnumerator.Current, actualSubdirsEnumerator.Current, verifymetadata, contextMessage);
            }
            // Assert that expectedDir and actualDir contain the same files
            var expectedFiles = SystemIO.IO_OS.EnumerateFiles(expectedDir).OrderBy(SystemIO.IO_OS.PathGetFileName);
            var actualFiles = SystemIO.IO_OS.EnumerateFiles(actualDir).OrderBy(SystemIO.IO_OS.PathGetFileName);
            Assert.That(expectedFiles.Select(SystemIO.IO_OS.PathGetFileName), Is.EquivalentTo(actualFiles.Select(SystemIO.IO_OS.PathGetFileName)), localMessage);
            // Assert that the files are equal
            var expectedFilesEnumerator = expectedFiles.GetEnumerator();
            var actualFilesEnumerator = actualFiles.GetEnumerator();
            while (expectedFilesEnumerator.MoveNext() && actualFilesEnumerator.MoveNext())
            {
                AssertFilesAreEqual(expectedFilesEnumerator.Current, actualFilesEnumerator.Current, verifymetadata, contextMessage);
            }
        }

        /// <summary>
        /// Asserts that two files are equal by comparing their length, contents, and, optionally, their metadata.
        /// </summary>
        /// <param name="expectedFile">The expected file.</param>
        /// <param name="actualFile">The actual file.</param>
        /// <param name="verifymetadata">True to also compare file metadata.</param>
        /// <param name="contextMessage">Context information to include in an assert message.</param>
        public static void AssertFilesAreEqual(string expectedFile, string actualFile, bool verifymetadata, string contextMessage)
        {
            using (var expectedFileStream = SystemIO.IO_OS.FileOpenRead(expectedFile))
            using (var actualFileStream = SystemIO.IO_OS.FileOpenRead(actualFile))
            {
                // Compare file lengths
                var expectedFileStreamLength = expectedFileStream.Length;
                var actualFileStreamLength = actualFileStream.Length;
                Assert.That(actualFileStreamLength, Is.EqualTo(expectedFileStreamLength), $"{contextMessage}, file size mismatch for {expectedFile} and {actualFile}");
                // Compare file contents
                // The byte-by-byte compare is dog-slow, so we use a fast(-er) check, and then report the first byte diff if required
                if (!Utility.CompareStreams(expectedFileStream, actualFileStream, true))
                {
                    // Reset stream positions
                    expectedFileStream.Position = 0;
                    actualFileStream.Position = 0;
                    for (long i = 0; i < expectedFileStreamLength; i++)
                    {
                        var expectedByte = expectedFileStream.ReadByte();
                        var actualByte = actualFileStream.ReadByte();
                        // For performance reasons, only exercise Assert mechanism and generate message if byte comparison fails
                        if (expectedByte != actualByte)
                        {
                            var message = $"{contextMessage}, file contents mismatch at position {i} for {expectedFile} and {actualFile}";
                            Assert.That(actualByte, Is.EqualTo(expectedByte), message);
                        }
                    }
                }
            }
            // Compare file metadata
            if (verifymetadata)
            {
                // macOS seem to like to actually set the time to some value different than what you set by hundreds of milliseconds.
                // Reading the time right after it is set gives the expected value but when read later it is slightly different.
                // Maybe a bug in .net?
                int granularity = OperatingSystem.IsMacOS() ? 2999 : 1;
                Assert.That(
                    SystemIO.IO_OS.GetLastWriteTimeUtc(actualFile),
                    Is.EqualTo(SystemIO.IO_OS.GetLastWriteTimeUtc(expectedFile)).Within(granularity).Milliseconds,
                    $"{contextMessage}, last write time mismatch for {expectedFile} and {actualFile}");
                Assert.That(
                    SystemIO.IO_OS.GetCreationTimeUtc(actualFile),
                    Is.EqualTo(SystemIO.IO_OS.GetCreationTimeUtc(expectedFile)).Within(granularity).Milliseconds,
                    $"{contextMessage}, creation time mismatch for {expectedFile} and {actualFile}");
            }
        }

        public static Dictionary<string, string> Expand(this Dictionary<string, string> self, object extra)
        {
            var res = new Dictionary<string, string>(self);
            foreach (var n in extra.GetType().GetFields())
            {
                var name = n.Name.Replace('_', '-');
                var value = n.GetValue(extra);
                res[name] = value == null ? "" : value.ToString();
            }

            foreach (var n in extra.GetType().GetProperties())
            {
                var name = n.Name.Replace('_', '-');
                var value = n.GetValue(extra);
                res[name] = value == null ? "" : value.ToString();
            }

            return res;
        }

        /// <summary>
        /// Write file <paramref name="path"/> with <paramref name="contents"/>.
        /// </summary>
        public static void WriteFile(string path, byte[] contents)
        {
            using (FileStream fileStream = SystemIO.IO_OS.FileOpenWrite(path))
            {
                Utility.CopyStream(new MemoryStream(contents), fileStream);
            }
        }

        public static void WriteTestFile(string path, long size)
        {
            var data = new byte[size];
            new Random(path.GetHashCode()).NextBytes(data);
            File.WriteAllBytes(path, data);
        }

        public class TestVerificationException(string message) : Exception(message)
        {
        }

        public static void AssertResults(IBasicResults results)
        {
            string operation = "Result";
            // Use dynamic property access for MainOperation, because it is only exposed in internal classes
            PropertyInfo operationProperty = results.GetType().GetProperty("MainOperation", typeof(Library.Main.OperationMode));
            if (operationProperty != null)
            {
                operation = ((Library.Main.OperationMode)operationProperty.GetValue(results)).ToString();
            }

            if (results is ITestResults testResults)
            {
                if (testResults.Verifications.Any(p => p.Value.Any()))
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"Verification errors - {operation}:");
                    foreach (var p in testResults.Verifications)
                    {
                        sb.Append(p.Key.ToString());
                        if (p.Value.Count() == 0)
                        {
                            sb.AppendLine(" (no errors)");
                            continue;
                        }

                        sb.AppendLine(" (errors):");
                        foreach (var v in p.Value)
                            sb.AppendLine(v.ToString());
                    }

                    throw new TestVerificationException(sb.ToString());
                }
            }

            if (results.Errors.Count() != 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Errors - {operation}:");
                foreach (var e in results.Errors)
                    sb.AppendLine(e.ToString());
                throw new TestVerificationException(sb.ToString());
            }

            if (results.Warnings.Count() != 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Warnings - {operation}:");
                foreach (var w in results.Warnings)
                    sb.AppendLine(w.ToString());
                throw new TestVerificationException(sb.ToString());
            }
        }

        public static string DumpTable(IDbCommand cmd, string table, string where = null)
        {
            var sb = new StringBuilder();
            cmd.CommandText = $"SELECT * FROM {table}";
            var rowix = 0;

            sb.AppendLine($"{table} table:");
            foreach (var row in cmd.ExecuteReaderEnumerable())
            {
                if (rowix == 0)
                {
                    for (var col = 0; col < row.FieldCount; col++)
                        sb.Append($"{row.GetName(col)}\t");

                    sb.AppendLine();
                }
                rowix++;

                for (var col = 0; col < row.FieldCount; col++)
                    sb.Append($"{row.GetValue(col)}\t");
                sb.AppendLine();
            }

            sb.AppendLine();
            return sb.ToString();
        }
    }
}

