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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

		public override DragOperation CanDragNode ()
		{
			return DragOperation.Copy | DragOperation.Move;
		}

		public override bool CanDropNode (object dataObject, DragOperation operation)
		{
			var folder = (IFolderItem)CurrentNode.DataItem;
			if (dataObject is SystemFile systemFile) {
				if (operation == DragOperation.Copy) {
					return true;
				}
				return systemFile.Path.ParentDirectory != folder.BaseDirectory;
			} else if (dataObject is WorkspaceFolder otherFolder) {
				if (operation == DragOperation.Copy) {
					return true;
				}
				return otherFolder.BaseDirectory != folder.BaseDirectory;
			}
			return false;
		}

		public async override void OnNodeDrop (object dataObjects, DragOperation operation)
		{
			FilePath source = null;
			FilePath target = null;
			WorkspaceFolder sourceFolder = null;

			var targetFolder = (IFolderItem)CurrentNode.DataItem;

			if (dataObjects is WorkspaceFolder) {
				sourceFolder = (WorkspaceFolder)dataObjects;
				source = sourceFolder.BaseDirectory;
			} else if (dataObjects is SystemFile systemFile) {
				source = systemFile.Path;
				target = targetFolder.BaseDirectory.Combine (source.FileName);
			} else {
				return;
			}

			target = targetFolder.BaseDirectory.Combine (source.FileName);
			// If copying to the same directory, make a copy with a different name
			if (target == source) {
				target = WorkspaceFolderOperations.GetTargetCopyName (target, sourceFolder != null);
			}

			if (dataObjects is WorkspaceFolder) {
				string q;
				if (operation == DragOperation.Move) {
					q = GettextCatalog.GetString ("Do you really want to move the folder '{0}' to the folder '{1}'?", source.FileName, targetFolder.BaseDirectory);
					if (!MessageService.Confirm (q, AlertButton.Move))
						return;
				} else {
						q = GettextCatalog.GetString ("Do you really want to copy the folder '{0}' to the folder '{1}'?", source.FileName, targetFolder.BaseDirectory);
					if (!MessageService.Confirm (q, AlertButton.Copy))
						return;
				}
			} else if (dataObjects is SystemFile) {
				var items = Enumerable.Repeat (target, 1);

				foreach (var file in items) {
					if (File.Exists (file))
						if (!MessageService.Confirm (GettextCatalog.GetString ("The file '{0}' already exists. Do you want to overwrite it?", file.FileName), AlertButton.OverwriteFile))
							return;
				}
			}

			var filesToSave = new List<Document> ();
			foreach (Document doc in IdeApp.Workbench.Documents) {
				if (doc.IsDirty && doc.IsFile) {
					if (doc.Name == source || doc.Name.StartsWith (source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
						filesToSave.Add (doc);
					}
				}
			}

			if (filesToSave.Count > 0) {
				var sb = new StringBuilder ();
				foreach (Document doc in filesToSave) {
					if (sb.Length > 0) sb.Append (",\n");
					sb.Append (Path.GetFileName (doc.Name));
				}

				string question;

				if (operation == DragOperation.Move) {
					if (filesToSave.Count == 1)
						question = GettextCatalog.GetString ("Do you want to save the file '{0}' before the move operation?", sb.ToString ());
					else
						question = GettextCatalog.GetString ("Do you want to save the following files before the move operation?\n\n{0}", sb.ToString ());
				} else {
					if (filesToSave.Count == 1)
						question = GettextCatalog.GetString ("Do you want to save the file '{0}' before the copy operation?", sb.ToString ());
					else
						question = GettextCatalog.GetString ("Do you want to save the following files before the copy operation?\n\n{0}", sb.ToString ());
				}
				AlertButton noSave = new AlertButton (GettextCatalog.GetString ("Don't Save"));
				AlertButton res = MessageService.AskQuestion (question, AlertButton.Cancel, noSave, AlertButton.Save);
				if (res == AlertButton.Cancel)
					return;
				if (res == AlertButton.Save) {
					try {
						foreach (Document doc in filesToSave) {
							await doc.Save ();
						}
					} catch (Exception ex) {
						MessageService.ShowError (GettextCatalog.GetString ("Save operation failed."), ex);
						return;
					}
				}
			}

			bool move = operation == DragOperation.Move;
			string opText = move ? GettextCatalog.GetString ("Moving files...") : GettextCatalog.GetString ("Copying files...");

			using (var monitor = IdeApp.Workbench.ProgressMonitors.GetStatusProgressMonitor (opText, Stock.StatusSolutionOperation, true)) {
				WorkspaceFolderOperations.TransferFiles (monitor, source, target, move);
			}
		}
	}
}
