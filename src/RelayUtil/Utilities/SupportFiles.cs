// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace RelayUtil.Utilities
{
    using System;
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// Approach from: https://github.com/microsoft/perfview/blob/master/src/PerfView/Utilities/SupportFiles.cs
    /// 
    /// SupportFiles is a class that manages the unpacking of DLLs and other resources.  
    /// This allows you to make your EXE the 'only' file in the distribution, and all other
    /// files are unpacked from that EXE.   
    /// 
    /// To add a file to the EXE as a resource you need to add the following lines to the .csproj
    /// In the example below we added the TraceEvent.dll file (relative to the project directory).
    /// LogicalName must start with a .\ and is the relative path from the SupportFiles directory
    /// where the file will be placed.  Adding the Link tag makes it show up in a pretty way in
    /// solution explorer.  
    /// 
    /// <ItemGroup>
    ///  <EmbeddedResource Include="..\TraceEvent\$(OutDir)Microsoft.Diagnostics.Tracing.TraceEvent.dll">
    ///   <Type>Non-Resx</Type>
    ///   <WithCulture>false</WithCulture>
    ///   <LogicalName>.\TraceEvent.dll</LogicalName>
    ///  </EmbeddedResource>
    /// </ItemGroup>
    /// 
    /// By default SupportFiles registers an Assembly resolve helper so that if you reference
    /// any DLLs in the project the .NET runtime will look for them in the support files directory. 
    /// 
    /// You just need to be careful to call 'UnpackResourcesIfNeeded' in your Main method and 
    /// don't use any of your support DLLs in the main method itself (you can use it in any method
    /// called from main).   If necessary, put everything in a 'MainWorker' method except the 
    /// call to UnpackResourcesIfNeeded.
    /// 
    /// Everything you deploy goes in its own version directory where the version is the timestamp
    /// of the EXE.   Thus newer version of your EXE can run with an older version and they don't
    /// cobber each other.    Newer version WILL delete older version by only if the directory
    /// is unlocked (no-one is using it).   Thus there tends to only be one version. 
    /// 
    /// While UnpackResourcesIfNeeded will keep only one version it will not clean it up to
    /// zero.  You have to write your own '/uninstall' or 'Cleanup' that deletes SupportFileDir
    /// if you want this.  
    /// </summary>
    internal static class SupportFiles
    {
        /// <summary>
        /// Unpacks any resource that beginning with a .\ (so it looks like a relative path name)
        /// Such resources are unpacked into their relative position in SupportFileDir. 
        /// 'force' will force an update even if the files were unpacked already (usually not needed)
        /// The function returns true if files were unpacked.  
        /// </summary>
        public static bool UnpackResourcesIfNeeded(bool force = false)
        {
            var unpacked = false;
            if (Directory.Exists(SupportFileDir))
            {
                if (force)
                {
                    Directory.Delete(SupportFileDir);
                    UnpackResources();
                    unpacked = true;
                }
            }
            else
            {
                UnpackResources();
                unpacked = true;
            }

            // Register a Assembly resolve event handler so that we find our support dlls in the support dir.
            AppDomain.CurrentDomain.AssemblyResolve += delegate (object sender, ResolveEventArgs args)
            {
                var simpleName = args.Name;
                var commaIdx = simpleName.IndexOf(',');
                if (0 <= commaIdx)
                {
                    simpleName = simpleName.Substring(0, commaIdx);
                }

                string fileName = Path.Combine(SupportFileDir, simpleName + ".dll");
                if (File.Exists(fileName))
                {
                    return System.Reflection.Assembly.LoadFrom(fileName);
                }

                return null;
            };

            // Do we need to cleanup old files?
            // Note we do this AFTER setting up the Assemble Resolve event because we use FileUtiltities that
            // may not be in the EXE itself.  
            if (unpacked || File.Exists(Path.Combine(SupportFileDirBase, "CleanupNeeded")))
            {
                Cleanup();
            }

            return unpacked;
        }
        /// <summary>
        /// SupportFileDir is a directory that is reserved for CURRENT VERSION of the software (if a later version is installed)
        /// It gets its own directory).   This is the directory where files in the EXE get unpacked to.  
        /// </summary>
        public static string SupportFileDir
        {
            get
            {
                {
                    var exeLastWriteTime = File.GetLastWriteTime(MainAssemblyPath);
                    var version = exeLastWriteTime.ToString("VER.yyyy'-'MM'-'dd'.'HH'.'mm'.'ss.fff");
                    s_supportFileDir = Path.Combine(SupportFileDirBase, version);
                }
                return s_supportFileDir;
            }
        }
        /// <summary>
        /// You must have write access to this directory.  It does not need to exist, but 
        /// if not, users have to have permission to create it.   This directory should only
        /// be used for this app only (not shared with other things).    By default we choose
        /// %APPDATA%\APPNAME where APPNAME is the name of the application (EXE file name 
        /// without the extension). 
        /// </summary>
        public static string SupportFileDirBase
        {
            get
            {
                if (s_supportFileDirBase == null)
                {
                    string appName = Path.GetFileNameWithoutExtension(MainAssemblyPath);

                    string appData = Environment.GetEnvironmentVariable(appName + "_APPDATA");
                    if (appData == null)
                    {
                        appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        if (appData == null)
                        {
                            appData = Path.GetFileName(MainAssemblyPath);
                        }
                    }
                    s_supportFileDirBase = Path.Combine(appData, appName);
                }
                return s_supportFileDirBase;
            }
            set { s_supportFileDirBase = value; }
        }
        /// <summary>
        /// The path to the assembly containing <see cref="SupportFiles"/>. You should not be writing here! that is what
        /// <see cref="SupportFileDir"/> is for.
        /// </summary>
        public static string MainAssemblyPath
        {
            get
            {
                if (s_mainAssemblyPath == null)
                {
                    var mainAssembly = Assembly.GetExecutingAssembly();
                    s_mainAssemblyPath = mainAssembly.ManifestModule.FullyQualifiedName;
                }

                return s_mainAssemblyPath;
            }
        }
        /// <summary>
        /// The path to the entry executable.
        /// </summary>
        public static string ExePath
        {
            get
            {
                if (s_exePath == null)
                {
                    s_exePath = Assembly.GetEntryAssembly().ManifestModule.FullyQualifiedName;
                }

                return s_exePath;
            }
        }

        private static void UnpackResources()
        {
            // We don't unpack into the final directory so we can be transactional (all or nothing).  
            string prepDir = SupportFileDir + ".new";
            Directory.CreateDirectory(prepDir);

            // Unpack the files.  
            // We used to used GetEntryAssembly, but that makes using PerfView as a component of a larger EXE
            // problematic.   Instead use GetExecutingAssembly, which means that you have to put SupportFiles.cs
            // in your main program 
            var resourceAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            foreach (var resourceName in resourceAssembly.GetManifestResourceNames())
            {
                if (resourceName.StartsWith(@".\") || resourceName.StartsWith(@"./"))
                {
                    // Unpack everything, inefficient, but insures ldr64 works.  
                    string targetPath = Path.Combine(prepDir, resourceName);
                    if (!ResourceUtilities.UnpackResourceAsFile(resourceName, targetPath, resourceAssembly))
                    {
                        throw new ApplicationException("Could not unpack support file " + resourceName);
                    }
                }
            }

            // Commit the unpack, we try several times since antiviruses often lock the directory
            for (int retries = 0; ; retries++)
            {
                try
                {
                    Directory.Move(prepDir, SupportFileDir);
                    break;
                }
                catch (Exception)
                {
                    if (retries > 5)
                    {
                        throw;
                    }
                }
                System.Threading.Thread.Sleep(100);
            }
        }

        private static void Cleanup()
        {
            string cleanupMarkerFile = Path.Combine(SupportFileDirBase, "CleanupNeeded");
            var dirs = Directory.GetDirectories(SupportFileDirBase, "VER.*");
            if (dirs.Length > 1)
            {
                // We will assume we should come and check again on our next launch.  
                File.WriteAllText(cleanupMarkerFile, "");
                foreach (string dir in Directory.GetDirectories(s_supportFileDirBase))
                {
                    // Don't clean up myself
                    if (string.Compare(dir, s_supportFileDir, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        continue;
                    }

                    // We first try to move the directory and only delete it if that succeeds.  
                    // That way directories that are in use don't get cleaned up.    
                    try
                    {
                        var deletingName = dir + ".deleting";
                        if (dir.EndsWith(".deleting"))
                        {
                            deletingName = dir;
                        }
                        else
                        {
                            Directory.Move(dir, deletingName);
                        }

                        DirectoryUtilities.Clean(deletingName);
                    }
                    catch (Exception) { }
                }
            }
            else
            {
                // No cleanup needed, mark that fact
                FileUtilities.ForceDelete(cleanupMarkerFile);
            }
        }

        private static string s_supportFileDir;
        private static string s_supportFileDirBase;
        private static string s_mainAssemblyPath;
        private static string s_exePath;

        /// <summary>
        /// From: https://github.com/microsoft/perfview/blob/master/src/PerfView/Utilities/ResourceUtilities.cs
        /// </summary>
        public class ResourceUtilities
        {
            public static bool UnpackResourceAsFile(string resourceName, string targetFileName, Assembly sourceAssembly)
            {
                Stream sourceStream = sourceAssembly.GetManifestResourceStream(resourceName);
                if (sourceStream == null)
                {
                    return false;
                }

                var dir = Path.GetDirectoryName(targetFileName);
                Directory.CreateDirectory(dir);     // Create directory if needed.  
                FileUtilities.ForceDelete(targetFileName);
                FileStream targetStream = File.Open(targetFileName, FileMode.Create);
                sourceStream.CopyTo(targetStream);
                targetStream.Close();
                return true;
            }
        }

        /// <summary>
        /// From https://github.com/microsoft/perfview/blob/master/src/Utilities/DirectoryUtilities.cs
        /// </summary>
        static class DirectoryUtilities
        {
            /// <summary>
            /// Clean is sort of a 'safe' recursive delete of a directory.  It either deletes the
            /// files or moves them to '*.deleting' names.  It deletes directories that are completely
            /// empty.  Thus it will do a recursive delete when that is possible.  There will only 
            /// be *.deleting files after this returns.  It returns the number of files and directories
            /// that could not be deleted.  
            /// </summary>
            public static int Clean(string directory)
            {
                if (!Directory.Exists(directory))
                {
                    return 0;
                }

                int ret = 0;
                foreach (string file in Directory.GetFiles(directory))
                {
                    if (!FileUtilities.ForceDelete(file))
                    {
                        ret++;
                    }
                }

                foreach (string subDir in Directory.GetDirectories(directory))
                {
                    ret += Clean(subDir);
                }

                if (ret == 0)
                {
                    try
                    {
                        Directory.Delete(directory, true);
                    }
                    catch
                    {
                        ret++;
                    }
                }
                else
                {
                    ret++;
                }

                return ret;
            }
        }

        /// <summary>
        /// From https://github.com/microsoft/perfview/blob/master/src/Utilities/FileUtilities.cs
        /// </summary>
        static class FileUtilities
        {
            /// <summary>
            /// Delete works much like File.Delete, except that it will succeed if the
            /// archiveFile does not exist, and will rename the archiveFile so that even if the archiveFile 
            /// is locked the original archiveFile variable will be made available.  
            /// 
            /// It renames the  archiveFile with a '[num].deleting'.  These files might be left 
            /// behind.  
            /// 
            /// It returns true if it was completely successful.  If there is a *.deleting
            /// archiveFile left behind, it returns false. 
            /// </summary>
            /// <param variable="fileName">The variable of the archiveFile to delete</param>
            public static bool ForceDelete(string fileName)
            {
                if (Directory.Exists(fileName))
                {
                    return DirectoryUtilities.Clean(fileName) != 0;
                }

                if (!File.Exists(fileName))
                {
                    return true;
                }

                // First move the archiveFile out of the way, so that even if it is locked
                // The original archiveFile is still gone.  
                string fileToDelete = fileName;
                bool tryToDeleteOtherFiles = true;
                if (!fileToDelete.EndsWith(".deleting", StringComparison.OrdinalIgnoreCase))
                {
                    tryToDeleteOtherFiles = false;
                    int i = 0;
                    for (i = 0; ; i++)
                    {
                        fileToDelete = fileName + "." + i.ToString() + ".deleting";
                        if (!File.Exists(fileToDelete))
                        {
                            break;
                        }

                        tryToDeleteOtherFiles = true;
                    }
                    try
                    {
                        File.Move(fileName, fileToDelete);
                    }
                    catch (Exception)
                    {
                        fileToDelete = fileName;
                    }
                }

                bool ret = false;
                try
                {
                    ret = TryDelete(fileToDelete);
                    if (tryToDeleteOtherFiles)
                    {
                        // delete any old *.deleting files that may have been left around 
                        string deletePattern = Path.GetFileName(fileName) + @".*.deleting";
                        foreach (string deleteingFile in Directory.GetFiles(Path.GetDirectoryName(fileName), deletePattern))
                        {
                            TryDelete(deleteingFile);
                        }
                    }
                }
                catch { };
                return ret;
            }

            /// <summary>
            /// Try to delete 'fileName' catching any exception.  Returns true if successful.   It will delete read-only files.  
            /// </summary>  
            public static bool TryDelete(string fileName)
            {
                bool ret = false;
                if (!File.Exists(fileName))
                {
                    return true;
                }

                try
                {
                    FileAttributes attribs = File.GetAttributes(fileName);
                    if ((attribs & FileAttributes.ReadOnly) != 0)
                    {
                        attribs &= ~FileAttributes.ReadOnly;
                        File.SetAttributes(fileName, attribs);
                    }
                    File.Delete(fileName);
                    ret = true;
                }
                catch (Exception) { }
                return ret;
            }
        }
    }
}
