﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using ZipFilePair = System.Tuple<string, System.IO.Compression.ZipArchiveEntry>;

namespace NuGet.ProjectManagement
{
    public static class FileSystemUtility
    {
        public static void MakeWritable(string fullPath)
        {
            if (File.Exists(fullPath))
            {
                var attributes = File.GetAttributes(fullPath);
                if (attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    File.SetAttributes(fullPath, attributes & ~FileAttributes.ReadOnly);
                }
            }
        }

        public static bool FileExists(string root, string path)
        {
            path = GetFullPath(root, path);
            return File.Exists(path);
        }

        public static string GetFullPath(string root, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return root;
            }
            return Path.Combine(root, path);
        }

        public static void AddFile(string root, string path, Stream stream, INuGetProjectContext nuGetProjectContext)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            AddFileCore(root, path, targetStream => stream.CopyTo(targetStream), nuGetProjectContext);
        }

        public static void AddFile(string root, string path, Action<Stream> writeToStream, INuGetProjectContext nuGetProjectContext)
        {
            if (writeToStream == null)
            {
                throw new ArgumentNullException("writeToStream");
            }

            AddFileCore(root, path, writeToStream, nuGetProjectContext);
        }

        private static void AddFileCore(string root, string path, Action<Stream> writeToStream, INuGetProjectContext nuGetProjectContext)
        {
            if (string.IsNullOrEmpty(path)
                || string.IsNullOrEmpty(Path.GetFileName(path)))
            {
                return;
            }

            Directory.CreateDirectory(GetFullPath(root, Path.GetDirectoryName(path)));

            var fullPath = GetFullPath(root, path);

            using (var outputStream = CreateFile(fullPath, nuGetProjectContext))
            {
                writeToStream(outputStream);
            }

            WriteAddedFileAndDirectory(path, nuGetProjectContext);
        }

        private static void WriteAddedFileAndDirectory(string path, INuGetProjectContext nuGetProjectContext)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var folderPath = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(folderPath))
            {
                nuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_AddedFileToFolder, Path.GetFileName(path), folderPath);
            }
            else
            {
                nuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_AddedFile, Path.GetFileName(path));
            }
        }

        public static Stream CreateFile(string root, string path, INuGetProjectContext nuGetProjectContext)
        {
            return CreateFile(GetFullPath(root, path), nuGetProjectContext);
        }

        public static Stream CreateFile(string fullPath, INuGetProjectContext nuGetProjectContext)
        {
            if (string.IsNullOrEmpty(fullPath)
                || string.IsNullOrEmpty(Path.GetFileName(fullPath)))
            {
                throw new ArgumentException(Strings.Argument_Cannot_Be_Null_Or_Empty, nameof(fullPath));
            }
            // MakeWriteable(fullPath); SourceControlManager will do that
            var sourceControlManager = SourceControlUtility.GetSourceControlManager(nuGetProjectContext);
            if (sourceControlManager != null)
            {
                return sourceControlManager.CreateFile(fullPath, nuGetProjectContext);
            }

            return CreateFile(fullPath);
        }

        public static Stream CreateFile(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)
                || string.IsNullOrEmpty(Path.GetFileName(fullPath)))
            {
                throw new ArgumentException(Strings.Argument_Cannot_Be_Null_Or_Empty, nameof(fullPath));
            }
            MakeWritable(fullPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            return File.Create(fullPath);
        }

        public static IEnumerable<string> GetFiles(string root, string path, string filter)
        {
            return GetFiles(root, path, filter, recursive: false);
        }

        public static IEnumerable<string> GetFiles(string root, string path, string filter, bool recursive)
        {
            path = PathUtility.EnsureTrailingSlash(Path.Combine(root, path));
            if (string.IsNullOrEmpty(filter))
            {
                filter = "*.*";
            }
            try
            {
                if (!Directory.Exists(path))
                {
                    return Enumerable.Empty<string>();
                }

                var filePaths = Directory.EnumerateFiles(path, filter, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                var trimmedPaths = new List<string>();
                foreach (var filePath in filePaths)
                {
                    if (filePath.Length > root.Length)
                    {
                        trimmedPaths.Add(filePath.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar));
                    }
                    else
                    {
                        trimmedPaths.Add(filePath.TrimStart(Path.DirectorySeparatorChar));
                    }
                }
                return trimmedPaths;
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }

            return Enumerable.Empty<string>();
        }

        public static void DeleteFile(string fullPath, INuGetProjectContext nuGetProjectContext)
        {
            if (!File.Exists(fullPath))
            {
                return;
            }

            try
            {
                MakeWritable(fullPath);
                var sourceControlManager = SourceControlUtility.GetSourceControlManager(nuGetProjectContext);
                if (sourceControlManager != null)
                {
                    sourceControlManager.PendDeleteFiles(new List<string> { fullPath }, string.Empty, nuGetProjectContext);
                }

                File.Delete(fullPath);
                var folderPath = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    nuGetProjectContext.Log(
                        MessageLevel.Debug,
                        Strings.Debug_RemovedFileFromFolder,
                        Path.GetFileName(fullPath),
                        Path.GetFullPath(folderPath));
                }
                else
                {
                    nuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_RemovedFile, Path.GetFileName(fullPath));
                }
            }
            catch (FileNotFoundException)
            {
            }
        }

        public static void DeleteFiles(IEnumerable<ZipFilePair> packageFiles, string packagesDir, INuGetProjectContext nuGetProjectContext)
        {
            var filesToDelete = new List<string>();

            foreach (var packageFile in packageFiles)
            {
                if (packageFile != null
                    && packageFile.Item1 != null
                    && packageFile.Item2 != null
                    && File.Exists(packageFile.Item1))
                {
                    if (ContentEquals(packageFile.Item1, packageFile.Item2.Open))
                    {
                        MakeWritable(packageFile.Item1);
                        filesToDelete.Add(packageFile.Item1);
                    }
                    else
                    {
                        nuGetProjectContext.Log(MessageLevel.Warning, Strings.Warning_FileModified, packageFile.Item1);
                    }
                }
            }

            var sourceControlManager = SourceControlUtility.GetSourceControlManager(nuGetProjectContext);
            if (sourceControlManager != null)
            {
                sourceControlManager.PendDeleteFiles(filesToDelete, packagesDir, nuGetProjectContext);
                foreach (var fileToDelete in filesToDelete)
                {
                    File.Delete(fileToDelete);
                }
            }
            else
            {
                // When it is not SourceControl, it is a different scenario altogether
                // First get all directories that contain files
                var directoryLookup = filesToDelete.ToLookup(p => Path.GetDirectoryName(p));

                // Get all directories that this package may have added
                var directories = from grouping in directoryLookup
                                  from directory in GetDirectories(grouping.Key, altDirectorySeparator: false)
                                  orderby directory.Length descending
                                  select directory;

                // Remove files from every directory
                foreach (var directory in directories)
                {
                    var directoryFiles = directoryLookup.Contains(directory) ? directoryLookup[directory] : Enumerable.Empty<string>();
                    var dirPath = Path.Combine(packagesDir, directory);

                    if (!Directory.Exists(dirPath))
                    {
                        continue;
                    }

                    foreach (var file in directoryFiles)
                    {
                        var path = Path.Combine(packagesDir, file);
                        File.Delete(path);
                    }

                    // If the directory is empty then delete it
                    if (!GetFiles(packagesDir, dirPath, "*.*").Any()
                        &&
                        !GetDirectories(packagesDir, dirPath).Any())
                    {
                        DeleteDirectorySafe(Path.Combine(packagesDir, dirPath), recursive: false, nuGetProjectContext: nuGetProjectContext);
                    }
                }
            }
        }

        public static bool ContentEquals(string path, Func<Stream> streamFactory)
        {
            using (Stream stream = streamFactory(),
                fileStream = File.OpenRead(path))
            {
                return StreamUtility.ContentEquals(stream, fileStream);
            }
        }

        // HACK: TODO: This is kinda bad that there is a PendAddFiles method here while delete files performs necessary pending and deletion
        // Need to update package extraction in Packaging to use a filesystem abstraction or'
        // just return files to be added in a clean form for projectmanagement to do the addition
        public static void PendAddFiles(IEnumerable<string> addedPackageFiles, string packagesDir, INuGetProjectContext nuGetProjectContext)
        {
            var sourceControlManager = SourceControlUtility.GetSourceControlManager(nuGetProjectContext);
            if (sourceControlManager != null)
            {
                sourceControlManager.PendAddFiles(addedPackageFiles, packagesDir, nuGetProjectContext);
            }
        }

        public static bool DirectoryExists(string root, string path)
        {
            path = GetFullPath(root, path);
            return Directory.Exists(path);
        }

        public static void DeleteFileAndParentDirectoriesIfEmpty(string root, string filePath, INuGetProjectContext nuGetProjectContext)
        {
            var fullPath = GetFullPath(root, filePath);
            // first delete the file itself
            DeleteFileSafe(fullPath, nuGetProjectContext);

            if (!string.IsNullOrEmpty(filePath))
            {
                // now delete all parent directories if they are empty
                for (var path = Path.GetDirectoryName(filePath); !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
                {
                    if (GetFiles(root, path, "*.*").Any()
                        || GetDirectories(root, path).Any())
                    {
                        // if this directory is not empty, stop
                        break;
                    }
                    // otherwise, delete it, and move up to its parent
                    DeleteDirectorySafe(fullPath, false, nuGetProjectContext);
                }
            }
        }

        public static void DeleteDirectorySafe(string fullPath, bool recursive, INuGetProjectContext nuGetProjectContext)
        {
            DoSafeAction(() => DeleteDirectory(fullPath, recursive, nuGetProjectContext), nuGetProjectContext);
        }

        public static void DeleteDirectory(string fullPath, bool recursive, INuGetProjectContext nuGetProjectContext)
        {
            if (!Directory.Exists(fullPath))
            {
                return;
            }

            try
            {
                Directory.Delete(fullPath, recursive);

                // The directory is not guaranteed to be gone since there could be
                // other open handles. Wait, up to half a second, until the directory is gone.
                for (var i = 0; Directory.Exists(fullPath) && i < 5; ++i)
                {
                    Thread.Sleep(100);
                }

                nuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_RemovedFolder, fullPath);
            }
            catch (DirectoryNotFoundException)
            {
            }
        }

        public static IEnumerable<string> GetDirectories(string root, string path)
        {
            try
            {
                path = PathUtility.EnsureTrailingSlash(GetFullPath(root, path));
                if (!Directory.Exists(path))
                {
                    return Enumerable.Empty<string>();
                }
                return Directory.EnumerateDirectories(path)
                    .Select(x => MakeRelativePath(root, x));
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }

            return Enumerable.Empty<string>();
        }

        internal static IEnumerable<string> GetDirectories(string path, bool altDirectorySeparator)
        {
            foreach (var index in IndexOfAll(path, altDirectorySeparator ? Path.AltDirectorySeparatorChar : Path.DirectorySeparatorChar))
            {
                yield return path.Substring(0, index);
            }
            yield return path;
        }

        private static IEnumerable<int> IndexOfAll(string value, char ch)
        {
            var index = -1;
            do
            {
                index = value.IndexOf(ch, index + 1);
                if (index >= 0)
                {
                    yield return index;
                }
            }
            while (index >= 0);
        }

        public static string MakeRelativePath(string root, string fullPath)
        {
            if (fullPath.Length <= root.Length)
            {
                return fullPath.TrimStart(Path.DirectorySeparatorChar);
            }
            return fullPath.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar);
        }

        internal static void DeleteFileSafe(string fullPath, INuGetProjectContext nuGetProjectContext)
        {
            DoSafeAction(() => DeleteFile(fullPath, nuGetProjectContext), nuGetProjectContext);
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We want to log an exception as a warning and move on")]
        private static void DoSafeAction(Action action, INuGetProjectContext nuGetProjectContext)
        {
            try
            {
                Attempt(action);
            }
            catch (Exception e)
            {
                nuGetProjectContext.Log(MessageLevel.Warning, e.Message);
            }
        }

        private static void Attempt(Action action, int retries = 3, int delayBeforeRetry = 150)
        {
            while (retries > 0)
            {
                try
                {
                    action();
                    break;
                }
                catch
                {
                    retries--;
                    if (retries == 0)
                    {
                        throw;
                    }
                }
                Thread.Sleep(delayBeforeRetry);
            }
        }
    }
}