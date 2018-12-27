//
// FolderCommandHandler.cs
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

// Based on FolderCommandHandler and ProjectFolderCommandHandler
// MonoDevelop.Ide/MonoDevelop.Ide.Gui.Pads.ProjectPad/FolderNodeBuilder.cs
// MonoDevelop.Ide/MonoDevelop.Ide.Gui.Pads.ProjectPad/ProjectFolderNodeBuilder.cs

using System;
using System.IO;
using System.Linq;
using MonoDevelop.Components.Commands;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Commands;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Components;
using MonoDevelop.Ide.Gui.Pads;
using MonoDevelop.Ide.Gui.Pads.ProjectPad;
using MonoDevelop.Projects;

namespace MonoDevelop.OpenAnyFolder
{
	abstract class FolderCommandHandler : NodeCommandHandler
	{
		public override void ActivateItem ()
		{
			CurrentNode.Expanded = !CurrentNode.Expanded;
		}

		[CommandHandler (ProjectCommands.AddNewFiles)]
		void AddNewFileToFolder ()
		{
			var folderItem = (IFolderItem)CurrentNode.DataItem;

			if (!IdeApp.ProjectOperations.CreateProjectFile (null, folderItem.BaseDirectory))
				return;

			CurrentNode.Expanded = true;

			if (IdeApp.Workbench.ActiveDocument != null)
				IdeApp.Workbench.ActiveDocument.Window.SelectWindow ();
		}

		[CommandHandler (ProjectCommands.NewFolder)]
		void AddNewFolder ()
		{
			CurrentNode.Expanded = true;

			var folderItem = (IFolderItem)CurrentNode.DataItem;
			string directoryName = CreateNewDirectory (folderItem.BaseDirectory);

			Tree.AddNodeInsertCallback (new WorkspaceFolder (directoryName), new TreeNodeCallback (OnFolderNodeInserted));
		}

		string CreateNewDirectory (FilePath baseDirectory)
		{
			string directoryName = Path.Combine (baseDirectory, GettextCatalog.GetString ("New Folder"));
			int index = -1;

			if (Directory.Exists (directoryName)) {
				bool exists = true;
				while (exists) {
					++index;
					string newDirectoryName = directoryName + (index + 1);
					exists = Directory.Exists (newDirectoryName);
				}
			}

			if (index >= 0) {
				directoryName += index + 1;
			}

			Directory.CreateDirectory (directoryName);

			return directoryName;
		}

		void OnFolderNodeInserted (ITreeNavigator nav)
		{
			nav.Selected = true;
			Tree.StartLabelEdit ();
		}

		[CommandUpdateHandler (EditCommands.Delete)]
		void UpdateRemoveItem (CommandInfo info)
		{
			info.Enabled = CanDeleteMultipleItems ();
			info.Text = GettextCatalog.GetString ("Remove");
		}

		public override void DeleteMultipleItems ()
		{
			RemoveWorkspaceFolderHandler.DeleteMultipleItems (CurrentNodes);
		}

		public override void RenameItem (string newName)
		{
			var folder = (WorkspaceFolder)CurrentNode.DataItem;

			FilePath oldFolderName = folder.BaseDirectory;
			FilePath newFolderName = oldFolderName.ParentDirectory.Combine (newName);

			if (oldFolderName == newFolderName) {
				return;
			}

			try {
				if (!FileService.IsValidPath (newFolderName) || ContainsDirectorySeparator (newName)) {
					MessageService.ShowWarning (GettextCatalog.GetString ("The name you have chosen contains illegal characters. Please choose a different name."));
					return;
				}
				if (File.Exists (newFolderName)) {
					MessageService.ShowWarning (GettextCatalog.GetString ("File or directory name is already in use. Please choose a different one."));
					return;
				}
				// Don't use Directory.Exists because we want to check for the exact case in case-insensitive file systems
				string directory = Directory.EnumerateDirectories (Path.GetDirectoryName (newFolderName), Path.GetFileName (newFolderName)).FirstOrDefault ();
				if (directory != null) {
					MessageService.ShowWarning (GettextCatalog.GetString ("File or directory name is already in use. Please choose a different one."));
					return;
				}

				FileService.RenameDirectory (oldFolderName, newName);
			} catch (ArgumentException) { // new file name with wildcard (*, ?) characters in it
				MessageService.ShowWarning (GettextCatalog.GetString ("The name you have chosen contains illegal characters. Please choose a different name."));
			} catch (IOException ex) {
				MessageService.ShowError (GettextCatalog.GetString ("There was an error renaming the directory."), ex.Message, ex);
			}
		}

		static bool ContainsDirectorySeparator (string name)
		{
			return name.Contains (Path.DirectorySeparatorChar) || name.Contains (Path.AltDirectorySeparatorChar);
		}

		public override bool CanDropNode (object dataObject, DragOperation operation)
		{
			var folder = (IFolderItem)CurrentNode.DataItem;
			if (dataObject is SystemFile systemFile) {
				if (operation == DragOperation.Copy) {
					return true;
				}
				return systemFile.Path.ParentDirectory != folder.BaseDirectory;
			}
			return false;
		}

		public override void OnNodeDrop (object dataObjects, DragOperation operation)
		{
			var folder = (IFolderItem)CurrentNode.DataItem;
			if (dataObjects is SystemFile systemFile) {
				FilePath source = systemFile.Path;
				FilePath target = folder.BaseDirectory.Combine (source.FileName);
				if (target == source) {
					target = WorkspaceFolderOperations.GetTargetCopyName (target, false);
				}

				using (ProgressMonitor monitor = IdeApp.Workbench.ProgressMonitors.GetStatusProgressMonitor (GettextCatalog.GetString ("Copying files..."), Stock.StatusWorking, true)) {
					bool move = operation == DragOperation.Move;
					WorkspaceFolderOperations.TransferFiles (monitor, source, target, move);
				}
			}
		}
	}
}
