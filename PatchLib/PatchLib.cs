﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Ionic.Zip;

// using DotNetZip library
// http://dotnetzip.codeplex.com/
// http://cheeso.members.winisp.net/DotNetZipHelp/html/d4648875-d41a-783b-d5f4-638df39ee413.htm
//
// TODO
// 1: look at ExtractExistingFileAction OverwriteSilently
//  http://cheeso.members.winisp.net/DotNetZipHelp/html/5443c4c0-6f74-9ae1-37fd-9a4ae936832d.htm
// 2: 

// NOTES
// 1: Extractor will have to maintain the list of registry keys for each application that is
//    patched.  But Archiver will need to pass a hint to let Extractor know which registry key to
//    query.

namespace PatchTool
{
    public class Archiver
    {
        private string _sourceDir;
        public string SourceDir
        {
            get { return _sourceDir; }
            set { _sourceDir = value; }
        }

        private string _patchId = "GenericArchive.exe";
        public string PatchID
        {
            get { return _patchId; }
            set { _patchId = value; }
        }

        private string _productVersion = "1.0.0.0";
        public string ProductVersion
        {
            get { return _productVersion; }
            set { _productVersion = value; }
        }

        private string _extractDir = ".";
        public string ExtractDir
        {
            get { return _extractDir; }
            set { _extractDir = value; }
        }

        public void run()
        {
            using (ZipFile zip = new ZipFile())
            {
                if (Directory.Exists(SourceDir))
                {
                    zip.AddDirectory(SourceDir, Path.GetFileName(SourceDir));
                }
                else
                {
                    Console.WriteLine("{0} is not a valid directory", SourceDir);
                    // NOT A BIG FAN of these types of side-effects
                    System.Environment.Exit(1);
                }

                // TC: well this is ugly.  I've added a reference to Extractor.exe into the
                // Archiver project, but it seems like this belongs in Archiver as well, since
                // that's what's looking for the file.
                zip.AddFile("Clyde.exe");
                zip.AddFile("PatchLib.dll");

                zip.Comment = "Where will this show up?";

                SelfExtractorSaveOptions options = new SelfExtractorSaveOptions();
                options.Flavor = SelfExtractorFlavor.ConsoleApplication;
                // TC: I'd like to also use options.FileVersion here.  Maybe sort it out later.
                options.ProductVersion = ProductVersion;
                options.DefaultExtractDirectory = ExtractDir;
                // TC: debug
                //Console.WriteLine("options.DefaultExtractDirectory: {0}", options.DefaultExtractDirectory);

                options.Copyright = "Copyright 2010 Envision Telephony";
                options.PostExtractCommandLine = "Clyde.exe";
                // TC: false for dev, true (maybe) for production
                options.RemoveUnpackedFilesAfterExecute = false;

                // TC: delete other patches before reusing file name!
                zip.SaveSelfExtractor(PatchID, options);
            }
        }

        // TC: given name of ExistingZipFile (param), list the file's contents.  This
        //  method will be mostly useful, if a little self-referential (it could be called
        //  on itself! :)), in Extractor.
        //public void List()
        //{
        //    using (ZipFile zip = ZipFile.Read(ExistingZipFile))
        //    {
        //        foreach (ZipEntry e in zip)
        //        {
        //            if (header)
        //            {
        //                System.Console.WriteLine("Zipfile: {0}", zip.Name);
        //                if ((zip.Comment != null) && (zip.Comment != ""))
        //                    System.Console.WriteLine("Comment: {0}", zip.Comment);
        //                System.Console.WriteLine("\n{1,-22} {2,8}  {3,5}   {4,8}  {5,3} {0}",
        //                                         "Filename", "Modified", "Size", "Ratio", "Packed", "pw?");
        //                System.Console.WriteLine(new System.String('-', 72));
        //                header = false;
        //            }
        //            System.Console.WriteLine("{1,-22} {2,8} {3,5:F0}%   {4,8}  {5,3} {0}",
        //                                     e.FileName,
        //                                     e.LastModified.ToString("yyyy-MM-dd HH:mm:ss"),
        //                                     e.UncompressedSize,
        //                                     e.CompressionRatio,
        //                                     e.CompressedSize,
        //                                     (e.UsesEncryption) ? "Y" : "N");
        //        }
        //    }
        //}
    }

    // The Extractor needs a few paths:
    //  - ExtractDir is where the files are extracted when the user double clicks the SFX.  This path should be logged.
    //  - APPDIR/patches/<version>/old is where the files to replace are stored for posterity.  This info should be logged.
    //  - APPDIR/patches/<version>/new is where the new files are stored for posterity.  This info should be logged.
    //  - APPDIR is where the patch files are eventually delivered to, if all goes well.

    public class Extractor
    {
        private static log4net.ILog log = log4net.LogManager.GetLogger("patch.log");
        // TC: not yet
        //string logmsg;

        private void init()
        {
            Console.SetWindowSize(140, 50);
        }

        public Extractor()
        {
            init();
        }

        public Extractor(string _appDir)
        {
            init();
            AppDir = _appDir;
            //logmsg = System.String.Format("APPDIR: {0}", AppDir);
            //log.Info(logmsg);
            log.Info(System.String.Format("APPDIR: {0}", AppDir));
        }

        private string _appDir;
        public string AppDir
        {
            get { return _appDir; }
            set { _appDir = value; }
        }

        // This should be equivalent to ExtractDir in Archiver.  I should probably find a better
        // solution.
        private string _extractDir = Directory.GetCurrentDirectory();
        public string ExtractDir
        {
            get { return _extractDir; }
        }

        // Are all files in srcDir also present in dstDir (extractDir and appDir, respectively)?
        // Should it return void or bool?
        //
        // NB: may need "C:\patches\d7699dbd-8214-458e-adb0-8317dfbfaab1>runas /env /user:administrator Clyde.exe"
        public void run(string _srcDir, string _dstDir)
        {
            // patch directory and local target
            DirectoryInfo srcDir = new DirectoryInfo(_srcDir);
            DirectoryInfo dstDir = new DirectoryInfo(_dstDir);

            // create backup folders: Clyde needs the patchID; fake it for now
            DirectoryInfo backupDirNew = new DirectoryInfo(Path.Combine(dstDir.ToString(), "patches", @"1.2.3.4", "new"));
            if (!Directory.Exists(backupDirNew.ToString()))
            {
                try
                {
                    Directory.CreateDirectory(backupDirNew.ToString());
                }
                catch (System.UnauthorizedAccessException)
                {
                    MessageBox.Show("PatchTool must be run as Administrator on this system", "sorry Charlie");
                    throw;
                }
            }
            //
            DirectoryInfo backupDirOld = new DirectoryInfo(Path.Combine(dstDir.ToString(), "patches", @"1.2.3.4", "old"));
            if (!Directory.Exists(backupDirOld.ToString()))
            {
                try
                {
                Directory.CreateDirectory(backupDirOld.ToString());
                }
                catch (System.UnauthorizedAccessException)
                {
                    MessageBox.Show("PatchTool must be run as Administrator on this system", "sorry Charlie");
                    throw;
                }
            }

            FileInfo[] srcFiles = srcDir.GetFiles("*", SearchOption.AllDirectories);

            // each file in the patch, with relative directories; base paths are the heads
            string tail;
            // each file to patch, full path
            string fileToPatch;
            // each file bound for the old/ directory
            string bakFileOld;

            // TC: three steps
            // 1: copy srcDir to backupDirNew
            //    (e.g. C:/patches/GUID/ -> APPDIR/patches/10.1.0001.0/new/)
            // 2: copy the same files from dstDir to backupDirOld;
            //    (e.g., APPDIR/ -> APPDIR/patches/10.1.0001.0/old/)
            // 3: apply the patch.

            // 1: copy srcDir to backupDirNew
            CopyFolder(srcDir.ToString(), backupDirNew.ToString());
            Console.WriteLine("INFO: Did everything unzip okay?  The files in the new backup location [1]");
            Console.WriteLine("      should match the files in the extract dir [2]:");
            Console.WriteLine("\t[1] {0}", ExtractDir);
            Console.WriteLine("\t[2] {0}", backupDirNew);
            foreach (FileInfo f in srcFiles)
            {
                tail = RelativePath(srcDir.ToString(), f.FullName);
                string orig = Path.Combine(srcDir.ToString(), Path.GetDirectoryName(tail), f.ToString());
                string copied = Path.Combine(backupDirNew.ToString(), Path.GetDirectoryName(tail), f.ToString());
                FileCompare(orig, copied, tail);
            }
            Console.WriteLine();

            //
            // 2: copy the same files from dstDir to backupDirOld
            foreach (FileInfo f in srcFiles)
            {
                tail = RelativePath(srcDir.ToString(), f.FullName);
                bakFileOld = Path.GetFullPath(Path.Combine(backupDirOld.ToString(), tail));

                // get and check original location; eventually this will be a milestone: if the
                // file is missing, user may want to cancel
                fileToPatch = Path.GetFullPath(Path.Combine(dstDir.ToString(), tail));
                FileStat(fileToPatch);

                // Create any nested subdirectories included in the patch.  Note, this will loop
                // over the same location multiple times; it's a little big ugly
                DirectoryInfo backupSubdirOld = new DirectoryInfo(Path.GetDirectoryName(bakFileOld.ToString()));
                if (!Directory.Exists(backupSubdirOld.ToString()))
                {
                    Directory.CreateDirectory(backupSubdirOld.ToString());
                }

                File.Copy(fileToPatch, bakFileOld, true);
                // TC: explain this
                FileStat(bakFileOld);
                Console.WriteLine();
            }

            Console.WriteLine("INFO: Did the backup succeed?  The files to replace in APPDIR [1]");
            Console.WriteLine("      should match the files in the old backup location [2]:");
            Console.WriteLine("\t[1] {0}", dstDir);
            Console.WriteLine("\t[2] {0}", backupDirOld);
            foreach (FileInfo f in srcFiles)
            {
                tail = RelativePath(srcDir.ToString(), f.FullName);
                bakFileOld = Path.GetFullPath(Path.Combine(backupDirOld.ToString(), tail));
                fileToPatch = Path.GetFullPath(Path.Combine(dstDir.ToString(), tail));

                // Compare each file in old/ with the original in APPDIR.
                string orig = fileToPatch;
                string copied = bakFileOld;
                // TC: explain this
                FileCompare(orig, copied, tail);
            }
            Console.WriteLine();

            //
            // 3: apply the patch.
            Console.WriteLine("INFO: patching {0}", dstDir.ToString());
            CopyFolder(srcDir.ToString(), dstDir.ToString());

            Console.WriteLine("INFO: Did the patch succeed?  The files in the new backup location [1]");
            Console.WriteLine("      should match the files in APPDIR [2]:");
            Console.WriteLine("\t[1] {0}", dstDir);
            Console.WriteLine("\t[2] {0}", backupDirNew);
            foreach (FileInfo f in srcFiles)
            {
                tail = RelativePath(srcDir.ToString(), f.FullName);
                string orig = Path.Combine(srcDir.ToString(), Path.GetDirectoryName(tail), f.ToString());
                string copied = Path.Combine(dstDir.ToString(), Path.GetDirectoryName(tail), f.ToString());
                // TC: explain this
                FileCompare(orig, copied, tail);
            }
            Console.WriteLine();
        }

        // TC: probably want to return bool and not write to STDOUT
        private void FileCompare(string fileName1, string fileName2, string fileName3)
        {
            if (FileEquals(fileName1, fileName2))
            {
                Console.Write("{0, -130}", fileName3, Console.WindowWidth, Console.WindowHeight);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(String.Format("{0, 9}", "[matches]"), Console.WindowWidth, Console.WindowHeight);
                Console.ResetColor();
            }
            else
            {
                Console.Write("{0, -130}", fileName3, Console.WindowWidth, Console.WindowHeight);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(String.Format("{0, 9}", "[nomatch]"), Console.WindowWidth, Console.WindowHeight);
                Console.ResetColor();
            }
        }

        private void FileStat(string fileName)
        {
            FileInfo fileInfo = new FileInfo(fileName);
            if (fileInfo.Exists)
            {
                Console.Write("{0, -130}", fileInfo, Console.WindowWidth, Console.WindowHeight);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(String.Format("{0, 9}", "[present]"), Console.WindowWidth, Console.WindowHeight);
                Console.ResetColor();
            }
            else
            {
                Console.Write("{0, -130}", fileInfo, Console.WindowWidth, Console.WindowHeight);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(String.Format("{0, 9}", "[missing]"), Console.WindowWidth, Console.WindowHeight);
                Console.ResetColor();
            }
        }

        // http://stackoverflow.com/questions/968935/c-binary-file-compare
        static bool FileEquals(string fileName1, string fileName2)
        {
            // Check the file size and CRC equality here.. if they are equal...    
            using (var file1 = new FileStream(fileName1, FileMode.Open))
            using (var file2 = new FileStream(fileName2, FileMode.Open))
                return StreamsContentsAreEqual(file1, file2);
        }

        private static bool StreamsContentsAreEqual(Stream stream1, Stream stream2)
        {
            const int bufferSize = 2048 * 2;
            var buffer1 = new byte[bufferSize];
            var buffer2 = new byte[bufferSize];

            while (true)
            {
                int count1 = stream1.Read(buffer1, 0, bufferSize);
                int count2 = stream2.Read(buffer2, 0, bufferSize);

                if (count1 != count2)
                {
                    return false;
                }

                if (count1 == 0)
                {
                    return true;
                }

                int iterations = (int)Math.Ceiling((double)count1 / sizeof(Int64));
                for (int i = 0; i < iterations; i++)
                {
                    if (BitConverter.ToInt64(buffer1, i * sizeof(Int64)) != BitConverter.ToInt64(buffer2, i * sizeof(Int64)))
                    {
                        return false;
                    }
                }
            }
        }

        // http://stackoverflow.com/questions/968935/c-binary-file-compare
        //static bool FileEquals(string fileName1, string fileName2)
        //{
        //    // Check the file size and CRC equality here.. if they are equal...    
        //    using (var file1 = new FileStream(fileName1, FileMode.Open))
        //    using (var file2 = new FileStream(fileName2, FileMode.Open))
        //        return StreamEquals(file1, file2);
        //}

        //static bool StreamEquals(Stream stream1, Stream stream2)
        //{
        //    const int bufferSize = 2048;
        //    byte[] buffer1 = new byte[bufferSize]; //buffer size
        //    byte[] buffer2 = new byte[bufferSize];
        //    while (true)
        //    {
        //        int count1 = stream1.Read(buffer1, 0, bufferSize);
        //        int count2 = stream2.Read(buffer2, 0, bufferSize);

        //        if (count1 != count2)
        //            return false;

        //        if (count1 == 0)
        //            return true;

        //        // You might replace the following with an efficient "memcmp"
        //        if (!buffer1.Take(count1).SequenceEqual(buffer2.Take(count2)))
        //            return false;
        //    }
        //}

        // http://www.csharp411.com/c-copy-folder-recursively/
        public static void CopyFolder(string sourceFolder, string destFolder)
        {
            if (!Directory.Exists(destFolder))
            {
                Directory.CreateDirectory(destFolder);
            }
            string[] files = Directory.GetFiles(sourceFolder);
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                string dest = Path.Combine(destFolder, name);
                File.Copy(file, dest, true);
            }
            string[] folders = Directory.GetDirectories(sourceFolder);
            foreach (string folder in folders)
            {
                string name = Path.GetFileName(folder);
                string dest = Path.Combine(destFolder, name);
                CopyFolder(folder, dest);
            }
        }

        // http://mrpmorris.blogspot.com/2007/05/convert-absolute-path-to-relative-path.html
        private string RelativePath(string absolutePath, string relativeTo)
        {
            string[] absoluteDirectories = absolutePath.Split('\\');
            string[] relativeDirectories = relativeTo.Split('\\');

            //Get the shortest of the two paths
            int length = absoluteDirectories.Length < relativeDirectories.Length ? absoluteDirectories.Length : relativeDirectories.Length;

            //Use to determine where in the loop we exited
            int lastCommonRoot = -1;
            int index;

            //Find common root
            for (index = 0; index < length; index++)
                if (absoluteDirectories[index] == relativeDirectories[index])
                    lastCommonRoot = index;
                else
                    break;

            //If we didn't find a common prefix then throw
            if (lastCommonRoot == -1)
                throw new ArgumentException("Paths do not have a common base");

            //Build up the relative path
            StringBuilder relativePath = new StringBuilder();

            //Add on the ..
            for (index = lastCommonRoot + 1; index < absoluteDirectories.Length; index++)
                if (absoluteDirectories[index].Length > 0)
                    relativePath.Append("..\\");

            //Add on the folders
            for (index = lastCommonRoot + 1; index < relativeDirectories.Length - 1; index++)
                relativePath.Append(relativeDirectories[index] + "\\");
            relativePath.Append(relativeDirectories[relativeDirectories.Length - 1]);

            return relativePath.ToString();
        }
    }
}
