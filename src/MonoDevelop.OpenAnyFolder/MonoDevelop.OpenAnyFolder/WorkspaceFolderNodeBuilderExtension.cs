//
// WorkspaceFolderNodeBuilderExtension.cs
//
// Author:
//       Lluis Sanchez Gual
//       Matt Ward <matt.ward@xamarin.com>
//
// Copyright (C) 2005 Novell, Inc (http://www.novell.com)
// Copyright (c) 2017 Xamarin Inc. (http://xamarin.com)
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

// Based on the ShowAllFilesBuilderExtension.

using System;
using System.IO;
using System.Linq;
using MonoDevelop.Core;
using MonoDevelop.Ide.Gui.Components;
using MonoDevelop.Ide.Gui.Pads.ProjectPad;
using MonoDevelop.Projects;

namespace MonoDevelop.OpenAnyFolder
{
	class WorkspaceFolderNodeBuilderExtension : NodeBuilderExtension
	{
		public override bool CanBuildNode (Type dataType)
		{
			return typeof (Workspace).IsAssignableFrom (dataType) ||
				typeof (WorkspaceFolder).IsAssignableFrom (dataType);
		}

		protected override void Initialize ()
		{
			FileService.FileCreated += OnFileCreated;
			FileService.FileRemoved += OnFileRemoved;
		}

		public override void Dispose ()
		{
			FileService.FileCreated -= OnFileCreated;
			FileService.FileRemoved -= OnFileRemoved;
		}

		public override bool HasChildNodes (ITreeBuilder builder, object dataObject)
		{
			string path = GetFolderPath (dataObject);
			return Directory.Exists (path) && (Directory.EnumerateFileSystemEntries (path).Any ());
		}

		static string GetFolderPath (object dataObject)
		{
			return ((IFolderItem)dataObject).BaseDirectory;
		}

		public override void BuildChildNodes (ITreeBuilder treeBuilder, object dataObject)
		{
			string path = GetFolderPath (dataObject);
			if (Directory.Exists (path)) {
				treeBuilder.AddChildren (Directory.EnumerateFiles (path)
					.Where (file => !IsHidden (file))
					.Select (file => new SystemFile (file, null, false)));

				treeBuilder.AddChildren (Directory.EnumerateDirectories (path)
					.Where (file => !IsHidden (file))
					.Select (folder => new WorkspaceFolder (folder)));
			}
		}

		static bool IsHidden (string file)
		{
			return new FilePath (file).FileName.StartsWith (".", StringComparison.OrdinalIgnoreCase);
		}

		void OnFileCreated (object sender, FileEventArgs args)
		{
			foreach (FileEventInfo e in args) {
				if (Directory.Exists (e.FileName)) {
					EnsureReachable (e.FileName + "/", isDirectory: true);
				} else {
					AddFile (e.FileName);
				}
			}
		}

		void EnsureReachable (string path, bool isDirectory = false)
		{
			string childPath;
			ITreeBuilder builder = FindParentFolderNode (path, out childPath);
			if (builder != null) {
				if (childPath != path) {
					builder.AddChild (new WorkspaceFolder (childPath));
				}
				return;
			}

			if (!isDirectory)
				return;

			// Check to see if the path is inside a workspace.
			builder = FindParentWorkspaceNode (path);
			if (builder != null) {
				builder.AddChild (new WorkspaceFolder (path));
			}
		}

		ITreeBuilder FindParentFolderNode (string path, out string lastChildPath)
		{
			lastChildPath = path;
			string basePath = Path.GetDirectoryName (path);

			if (string.IsNullOrEmpty (basePath))
				return null;

			ITreeBuilder builder = Context.GetTreeBuilder (new WorkspaceFolder (basePath));
			if (builder != null)
				return builder;

			return FindParentFolderNode (basePath, out lastChildPath);
		}

		void AddFile (string fileName)
		{
			ITreeBuilder builder = Context.GetTreeBuilder ();
			string filePath = Path.GetDirectoryName (fileName);

			var file = new SystemFile (fileName, null, false);

			// Already there?
			if (builder.MoveToObject (file))
				return;

			var folder = new WorkspaceFolder (filePath);
			if (builder.MoveToObject (folder) || MoveToWorkspace (builder, filePath)) {
				if (builder.Filled)
					builder.AddChild (file);
			} else {
				// Make sure there is a path to that folder
				EnsureReachable (fileName);
			}
		}

		ITreeBuilder FindParentWorkspaceNode (string path)
		{
			ITreeBuilder builder = Context.GetTreeBuilder ();

			if (MoveToWorkspace (builder, path))
				return builder;

			return null;
		}

		bool MoveToWorkspace (ITreeBuilder builder, FilePath directoryPath)
		{
			Workspace workspace = builder.GetParentDataItem<Workspace> (true);
			if (workspace == null)
				return false;

			if (!builder.MoveToObject (workspace))
				return false;

			return workspace.BaseDirectory == directoryPath.CanonicalPath.ParentDirectory;
		}

		void OnFileRemoved (object sender, FileEventArgs e)
		{
			foreach (FileEventInfo info in e) {
				ITreeBuilder treeBuilder = Context.GetTreeBuilder ();
				if (treeBuilder.MoveToObject (new WorkspaceFolder (info.FileName))) {
					treeBuilder.Remove ();
				}
			}
		}
	}
}
