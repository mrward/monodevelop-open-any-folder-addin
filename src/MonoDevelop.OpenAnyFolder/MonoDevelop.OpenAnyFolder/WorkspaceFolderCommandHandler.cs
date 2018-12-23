//
// WorkspaceFolderCommandHandler.cs
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

// Based on FolderCommandHandler
// MonoDevelop.Ide/MonoDevelop.Ide.Gui.Pads.ProjectPad/FolderNodeBuilder.cs

using System.IO;
using MonoDevelop.Components.Commands;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Commands;
using MonoDevelop.Ide.Gui.Components;
using MonoDevelop.Ide.Gui.Pads;

namespace MonoDevelop.OpenAnyFolder
{
	class WorkspaceFolderCommandHandler : NodeCommandHandler
	{
		[CommandHandler (ProjectCommands.AddNewFiles)]
		public void AddNewFileToProject ()
		{
			var folder = (WorkspaceFolder)CurrentNode.DataItem;

			if (!IdeApp.ProjectOperations.CreateProjectFile (null, folder.BaseDirectory))
				return;

			CurrentNode.Expanded = true;
			if (IdeApp.Workbench.ActiveDocument != null)
				IdeApp.Workbench.ActiveDocument.Window.SelectWindow ();
		}

		[CommandHandler (ProjectCommands.NewFolder)]
		void AddNewFolder ()
		{
			CurrentNode.Expanded = true;

			var folder = (WorkspaceFolder)CurrentNode.DataItem;
			string directoryName = CreateNewDirectory (folder.BaseDirectory);

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
	}
}
