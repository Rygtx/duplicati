﻿// Copyright (C) 2025, The Duplicati Team
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
using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Duplicati.Library.Interface;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using Duplicati.Library.Utility;

namespace Duplicati.UnitTest
{
    public class BorderTests : BasicSetupHelper
    {
        [Test]
        [Category("Border")]
        public void Run10kNoProgress()
        {
            RunCommands(1024 * 10, modifyOptions: opts =>
            {
                opts["disable-file-scanner"] = "true";
            });
        }

        [Test]
        [Category("Border")]
        public void Run10k()
        {
            RunCommands(1024 * 10);
        }

        [Test]
        [Category("Border")]
        public void Run10mb()
        {
            RunCommands(1024 * 10, modifyOptions: opts =>
            {
                opts["blocksize"] = "10mb";
                opts["dblock-size"] = "25mb";
            });
        }

        [Test]
        [Category("Border")]
        public void Run100k()
        {
            RunCommands(1024 * 100);
        }

        [Test]
        [Category("Border")]
        public void Run12345_1()
        {
            RunCommands(12345);
        }

        [Test]
        [Category("Border")]
        public void Run12345_2()
        {
            RunCommands(12345, 1024 * 1024 * 10);
        }

        [Test]
        [Category("Border")]
        public void RunNoMetadata()
        {
            RunCommands(1024 * 10, modifyOptions: opts =>
            {
                opts["skip-metadata"] = "true";
            });
        }


        [Test]
        [Category("Border")]
        public void RunMD5()
        {
            RunCommands(1024 * 10, modifyOptions: opts =>
            {
                opts["block-hash-algorithm"] = "MD5";
                opts["file-hash-algorithm"] = "MD5";
            });
        }

        [Test]
        [Category("Border")]
        public void RunSHA384()
        {
            RunCommands(1024 * 10, modifyOptions: opts =>
            {
                opts["block-hash-algorithm"] = "SHA384";
                opts["file-hash-algorithm"] = "SHA384";
            });
        }

        [Test]
        [Category("Border")]
        public void RunMixedBlockFile_1()
        {
            RunCommands(1024 * 10, modifyOptions: opts =>
            {
                opts["block-hash-algorithm"] = "MD5";
                opts["file-hash-algorithm"] = "SHA1";
            });
        }

        [Test]
        [Category("Border")]
        public void RunMixedBlockFile_2()
        {
            RunCommands(1024 * 10, modifyOptions: opts =>
            {
                opts["block-hash-algorithm"] = "MD5";
                opts["file-hash-algorithm"] = "SHA256";
            });
        }

        [Test]
        [Category("Border")]
        public void RunNoIndexFiles()
        {
            RunCommands(1024 * 10, modifyOptions: opts =>
            {
                opts["index-file-policy"] = "None";
            });
        }

        [Test]
        [Category("Border")]
        public void RunSlimIndexFiles()
        {
            RunCommands(1024 * 10, modifyOptions: opts =>
            {
                opts["index-file-policy"] = "Lookup";
            });
        }

        [Test]
        [Category("Border")]
        public void RunQuickTimestamps()
        {
            RunCommands(1024 * 10, modifyOptions: opts =>
            {
                opts["check-filetime-only"] = "true";
            });
        }

        [Test]
        [Category("Border")]
        public void RunFullScan()
        {
            RunCommands(1024 * 10, modifyOptions: opts =>
            {
                opts["disable-filetime-check"] = "true";
            });
        }

        [Test]
        [Category("Border")]
        public void Run10kBackupRead()
        {
            if (!PermissionHelper.HasSeBackupPrivilege())
                return;

            try
            {
                using var _ = Duplicati.Library.Snapshots.Windows.WindowsShimLoader.NewSeBackupPrivilegeScope();
            }
            catch (Exception)
            {
                return;
            }

            RunCommands(1024 * 10, modifyOptions: opts =>
            {
                opts["backupread-policy"] = "required";
            });
        }

        public static Dictionary<string, int> WriteTestFilesToFolder(string targetfolder, int blocksize, int basedatasize = 0)
        {
            if (basedatasize <= 0)
                basedatasize = blocksize * 1024;

            var filenames = new Dictionary<string, int>();
            filenames[""] = basedatasize;
            filenames["-0"] = 0;
            filenames["-1"] = 1;

            filenames["-p1"] = basedatasize + 1;
            filenames["-p2"] = basedatasize + 2;
            filenames["-p500"] = basedatasize + 500;
            filenames["-m1"] = basedatasize - 1;
            filenames["-m2"] = basedatasize - 2;
            filenames["-m500"] = basedatasize - 500;

            filenames["-s1"] = blocksize / 4 + 6;
            filenames["-s2"] = blocksize / 10 + 6;
            filenames["-l1"] = blocksize * 4 + 6;
            filenames["-l2"] = blocksize * 10 + 6;

            filenames["-bm1"] = blocksize - 1;
            filenames["-b"] = blocksize;
            filenames["-bp1"] = blocksize + 1;

            var data = new byte[filenames.Select(x => x.Value).Max()];

            foreach (var k in filenames)
                File.WriteAllBytes(Path.Combine(targetfolder, "a" + k.Key), data.Take(k.Value).ToArray());

            return filenames;
        }

        private void RunCommands(int blocksize, int basedatasize = 0, Action<Dictionary<string, string>> modifyOptions = null)
        {
            var testopts = TestOptions;
            testopts["blocksize"] = blocksize.ToString() + "b";
            modifyOptions?.Invoke(testopts);

            var filenames = WriteTestFilesToFolder(DATAFOLDER, blocksize, basedatasize);

            using (var c = new Library.Main.Controller("file://" + TARGETFOLDER, testopts, null))
            {
                IBackupResults backupResults = c.Backup(new string[] { DATAFOLDER });
                Assert.AreEqual(0, backupResults.Errors.Count());

                // TODO: This sometimes results in a "No block hash found for file: C:\projects\duplicati\testdata\backup-data\a-0" warning.
                // Because of this, we don't check for warnings here.
            }

            // After the first backup we remove the --blocksize argument as that should be auto-set
            testopts.Remove("blocksize");
            testopts.Remove("block-hash-algorithm");
            testopts.Remove("file-hash-algorithm");

            using (var c = new Library.Main.Controller("file://" + TARGETFOLDER, testopts.Expand(new { version = 0 }), null))
                TestUtils.AssertResults(c.List("*"));

            // Do a "touch" on files to trigger a re-scan, which should do nothing
            //foreach (var k in filenames)
            //if (File.Exists(Path.Combine(DATAFOLDER, "a" + k.Key)))
            //File.SetLastWriteTime(Path.Combine(DATAFOLDER, "a" + k.Key), DateTime.Now.AddSeconds(5));

            var data = new byte[filenames.Select(x => x.Value).Max()];
            new Random().NextBytes(data);
            foreach (var k in filenames)
                File.WriteAllBytes(Path.Combine(DATAFOLDER, "b" + k.Key), data.Take(k.Value).ToArray());

            using (var c = new Library.Main.Controller("file://" + TARGETFOLDER, testopts, null))
            {
                var r = c.Backup(new string[] { DATAFOLDER });
                Assert.AreEqual(0, r.Errors.Count());
                Assert.AreEqual(0, r.Warnings.Count());

                if (!Library.Utility.Utility.ParseBoolOption(testopts, "disable-filetime-check"))
                {
                    if (r.OpenedFiles != filenames.Count)
                        throw new Exception($"Opened {r.OpenedFiles}, but should open {filenames.Count}");
                    if (r.ExaminedFiles != filenames.Count * 2)
                        throw new Exception($"Examined {r.ExaminedFiles}, but should examine open {filenames.Count * 2}");
                }
            }

            var rn = new Random();
            foreach (var k in filenames)
            {
                rn.NextBytes(data);
                File.WriteAllBytes(Path.Combine(DATAFOLDER, "c" + k.Key), data.Take(k.Value).ToArray());
            }

            System.Threading.Tasks.Task.Delay(1000).Wait();

            using (var c = new Library.Main.Controller("file://" + TARGETFOLDER, testopts, null))
                TestUtils.AssertResults(c.Backup(new string[] { DATAFOLDER }));
            using (var c = new Library.Main.Controller("file://" + TARGETFOLDER, testopts.Expand(new { version = 0 }), null))
            {
                var r = c.List("*");
                TestUtils.AssertResults(r);
                //ProgressWriteLine("Newest before deleting:");
                //ProgressWriteLine(string.Join(Environment.NewLine, r.Files.Select(x => x.Path)));
                Assert.AreEqual((filenames.Count * 3) + 1, r.Files.Count());
            }

            using (var c = new Library.Main.Controller("file://" + TARGETFOLDER, testopts.Expand(new { version = 0, no_local_db = true }), null))
            {
                var r = c.List("*");
                TestUtils.AssertResults(r);
                //ProgressWriteLine("Newest without db:");
                //ProgressWriteLine(string.Join(Environment.NewLine, r.Files.Select(x => x.Path)));
                Assert.AreEqual((filenames.Count * 3) + 1, r.Files.Count());
            }

            using (var c = new Library.Main.Controller("file://" + TARGETFOLDER, testopts.Expand(new { full_remote_verification = true }), null))
                TestUtils.AssertResults(c.Test(long.MaxValue));

            var recreatedDatabaseFile = Path.Combine(BASEFOLDER, "recreated-database.sqlite");
            if (File.Exists(recreatedDatabaseFile))
                File.Delete(recreatedDatabaseFile);

            testopts["dbpath"] = recreatedDatabaseFile;

            using (var c = new Library.Main.Controller("file://" + TARGETFOLDER, testopts, null))
                TestUtils.AssertResults(c.Repair());

            using (var c = new Library.Main.Controller("file://" + TARGETFOLDER, testopts, null))
            {
                IListResults listResults = c.List();
                TestUtils.AssertResults(listResults);
                Assert.AreEqual(3, listResults.Filesets.Count());
            }

            using (var c = new Library.Main.Controller("file://" + TARGETFOLDER, testopts.Expand(new { version = 2 }), null))
            {
                var r = c.List("*");
                TestUtils.AssertResults(r);
                //ProgressWriteLine("V2 after delete:");
                //ProgressWriteLine(string.Join(Environment.NewLine, r.Files.Select(x => x.Path)));
                Assert.AreEqual((filenames.Count * 1) + 1, r.Files.Count());
            }

            using (var c = new Library.Main.Controller("file://" + TARGETFOLDER, testopts.Expand(new { version = 1 }), null))
            {
                var r = c.List("*");
                TestUtils.AssertResults(r);
                //ProgressWriteLine("V1 after delete:");
                //ProgressWriteLine(string.Join(Environment.NewLine, r.Files.Select(x => x.Path)));
                Assert.AreEqual((filenames.Count * 2) + 1, r.Files.Count());
            }

            using (var c = new Library.Main.Controller("file://" + TARGETFOLDER, testopts.Expand(new { version = 0 }), null))
            {
                var r = c.List("*");
                TestUtils.AssertResults(r);
                //ProgressWriteLine("Newest after delete:");
                //ProgressWriteLine(string.Join(Environment.NewLine, r.Files.Select(x => x.Path)));
                Assert.AreEqual((filenames.Count * 3) + 1, r.Files.Count());
            }

            using (var c = new Library.Main.Controller("file://" + TARGETFOLDER, testopts.Expand(new { full_remote_verification = true }), null))
            {
                var r = c.Test(long.MaxValue);
                Assert.AreEqual(0, r.Errors.Count());
                Assert.AreEqual(0, r.Warnings.Count());
                Assert.IsFalse(r.Verifications.Any(p => p.Value.Any()));
            }

            using (var c = new Library.Main.Controller("file://" + TARGETFOLDER, testopts.Expand(new { restore_path = RESTOREFOLDER }), null))
            {
                var r = c.Restore(null);
                TestUtils.AssertResults(r);
                Assert.AreEqual(filenames.Count * 3, r.RestoredFiles);
            }

            TestUtils.AssertDirectoryTreesAreEquivalent(DATAFOLDER, RESTOREFOLDER, !Library.Utility.Utility.ParseBoolOption(testopts, "skip-metadata"), "Restore");

            using (var tf = new Library.Utility.TempFolder())
            {
                using (var c = new Library.Main.Controller("file://" + TARGETFOLDER, testopts.Expand(new { restore_path = (string)tf }), null))
                {
                    var r = c.Restore(new string[] { Path.Combine(DATAFOLDER, "a") + "*" });
                    TestUtils.AssertResults(r);
                    Assert.AreEqual(filenames.Count, r.RestoredFiles);
                }
            }
        }
    }
}

