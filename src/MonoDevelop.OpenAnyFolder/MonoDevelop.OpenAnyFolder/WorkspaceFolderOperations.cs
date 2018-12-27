//
// WorkspaceFolderOperations.cs
//
// Author:
//       Lluis Sanchez Gual
//       Matt Ward <matt.ward@microsoft.com>
//
// Copyright (C) 2005 Novell, Inc (http://www.novell.com)
// Copyright (c) 2018 Microsoft
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

// Parts based on ProjectOperations
// https://github.com/mono/monodevelop/blob/master/main/src/core/MonoDevelop.Ide/MonoDevelop.Ide/ProjectOperations.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonoDevelop.Core;
using MonoDevelop.Ide.Gui.Pads.ProjectPad;

namespace MonoDevelop.OpenAnyFolder
{
	static class WorkspaceFolderOperations
	{
		public static void TransferFiles (
			ProgressMonitor monitor,
			FilePath sourcePath,
			FilePath targetPath,
			bool removeFromSource)
		{
			// When transfering directories, targetPath is the directory where the source
			// directory will be transfered, including the destination directory or file name.
			// For example, if sourcePath is /a1/a2/a3 and targetPath is /b1/b2, the
			// new folder or file will be /b1/b2
			bool sourceIsFolder = Directory.Exists (sourcePath);

			bool movingFolder = removeFromSource && sourceIsFolder;

			// We need to remove all files + directories from the source project
			// but when dealing with the VCS addins we need to process only the
			// files so we do not create a 'file' in the VCS which corresponds
			// to a directory in the project and blow things up.
			var filesToRemove = new List<SystemFile> ();
			try {
				GetAllFilesRecursive (sourcePath, filesToRemove);
			} catch (Exception ex) {
				monitor.ReportError (GettextCatalog.GetString ("Could not get any file from '{0}'.", sourcePath), ex);
				return;
			}

			// Strip out all the directories to leave us with just the files.
			List<SystemFile> filesToMove = filesToRemove.Where (f => !Directory.Exists (f.Path)).ToList ();

			// Ensure that the destination folder is created, even if no files
			// are copied

			try {
				if (sourceIsFolder && !Directory.Exists (targetPath) && !movingFolder)
					FileService.CreateDirectory (targetPath);
			} catch (Exception ex) {
				monitor.ReportError (GettextCatalog.GetString ("Could not create directory '{0}'.", targetPath), ex);
				return;
			}

			// Transfer files
			// If moving a folder, do it all at once

			if (movingFolder) {
				try {
					FileService.MoveDirectory (sourcePath, targetPath);
				} catch (Exception ex) {
					monitor.ReportError (GettextCatalog.GetString ("Directory '{0}' could not be moved.", sourcePath), ex);
					return;
				}
			}

			if (removeFromSource)
				monitor.BeginTask (GettextCatalog.GetString ("Moving files..."), filesToMove.Count);
			else
				monitor.BeginTask (GettextCatalog.GetString ("Copying files..."), filesToMove.Count);

			foreach (SystemFile file in filesToMove) {
				var sourceFile = file.Path;
				FilePath newFile;
				if (sourceIsFolder)
					newFile = targetPath.Combine (sourceFile.ToRelative (sourcePath));
				else if (sourceFile == sourcePath)
					newFile = targetPath;
				else if (sourceFile.ParentDirectory != targetPath.ParentDirectory)
					newFile = targetPath.ParentDirectory.Combine (sourceFile.ToRelative (sourcePath.ParentDirectory));
				else
					newFile = GetTargetCopyName (sourceFile, false);

				if (!movingFolder) {
					try {
						FilePath fileDir = newFile.ParentDirectory;
						if (!Directory.Exists (fileDir))
							FileService.CreateDirectory (fileDir);
						if (removeFromSource) {
							// File.Move() does not have an overwrite argument and will fail if the destFile path exists, however, the user
							// has already chosen to overwrite the destination file.
							if (File.Exists (newFile))
								File.Delete (newFile);

							FileService.MoveFile (sourceFile, newFile);
						} else
							FileService.CopyFile (sourceFile, newFile);
					} catch (Exception ex) {
						if (removeFromSource)
							monitor.ReportError (GettextCatalog.GetString ("File '{0}' could not be moved.", sourceFile), ex);
						else
							monitor.ReportError (GettextCatalog.GetString ("File '{0}' could not be copied.", sourceFile), ex);
						monitor.Step (1);
						continue;
					}
				}

				monitor.Step (1);
			}

			monitor.EndTask ();
		}

		static void GetTargetCopyFileNameParts (FilePath path, out string nameWithoutExtension, out string extension)
		{
			// under normal circumstances this is what we would want, foo.cs -> foo and .cs
			// however, for cases like foo.xaml.cs, we want foo and .xaml.cs
			nameWithoutExtension = path.FileNameWithoutExtension;
			extension = path.Extension;
			var x = Path.GetFileNameWithoutExtension (nameWithoutExtension);
			while (x != nameWithoutExtension) {
				extension = Path.GetExtension (nameWithoutExtension) + extension;
				nameWithoutExtension = x;
			}
		}

		public static FilePath GetTargetCopyName (FilePath path, bool isFolder)
		{
			GetTargetCopyFileNameParts (path, out string nameWithoutExtension, out string extension);

			int n = 1;
			// First of all try to find an existing copy tag
			string fn = nameWithoutExtension;
			for (int i = 1; i < 100; i++) {
				string copyTag = GetCopyTag (i);
				if (fn.EndsWith (copyTag, StringComparison.OrdinalIgnoreCase)) {
					string newfn = fn.Substring (0, fn.Length - copyTag.Length);
					if (newfn.Trim ().Length > 0) {
						n = i + 1;
						path = path.ParentDirectory.Combine (newfn + path.Extension);
						break;
					}
				}
			}
			FilePath basePath = path;
			while ((!isFolder && File.Exists (path)) || (isFolder && Directory.Exists (path))) {
				string copyTag = GetCopyTag (n);
				path = basePath.ParentDirectory.Combine (nameWithoutExtension + copyTag + extension);
				n++;
			}
			return path;
		}

		static string GetCopyTag (int n)
		{
			string sc;
			switch (n) {
				case 1: sc = GettextCatalog.GetString ("copy"); break;
				case 2: sc = GettextCatalog.GetString ("another copy"); break;
				case 3: sc = GettextCatalog.GetString ("3rd copy"); break;
				case 4: sc = GettextCatalog.GetString ("4th copy"); break;
				case 5: sc = GettextCatalog.GetString ("5th copy"); break;
				case 6: sc = GettextCatalog.GetString ("6th copy"); break;
				case 7: sc = GettextCatalog.GetString ("7th copy"); break;
				case 8: sc = GettextCatalog.GetString ("8th copy"); break;
				case 9: sc = GettextCatalog.GetString ("9th copy"); break;
				default: sc = GettextCatalog.GetString ("copy {0}"); break;
			}
			return " (" + string.Format (sc, n) + ")";
		}

		static void GetAllFilesRecursive (string path, List<SystemFile> files)
		{
			if (File.Exists (path)) {
				files.Add (new SystemFile (path, null));
				return;
			}

			if (Directory.Exists (path)) {
				foreach (string file in Directory.GetFiles (path))
					files.Add (new SystemFile (file, null));

				foreach (string dir in Directory.GetDirectories (path))
					GetAllFilesRecursive (dir, files);
			}
		}
	}
}
