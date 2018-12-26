//
// RemoveWorkspaceFolderHandler.cs
//
// Author:
//       Lluis Sanchez Gual
//       Matt Ward <matt.ward@microsoft.com>
//
// Copyright (C) 2005 Novell, Inc (http://www.novell.com)//
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

// Based on ProjectFolderCommandHandler
// https://github.com/mono/monodevelop/blob/master/main/src/core/MonoDevelop.Ide/MonoDevelop.Ide.Gui.Pads.ProjectPad/ProjectFolderNodeBuilder.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui.Components;

namespace MonoDevelop.OpenAnyFolder
{
	static class RemoveWorkspaceFolderHandler
	{
		public static void DeleteMultipleItems (ITreeNavigator[] currentNodes)
		{
			var folders = new List<WorkspaceFolder> ();
			foreach (ITreeNavigator node in currentNodes) {
				if (node.DataItem is WorkspaceFolder folder) {
					folders.Add (folder);
				}
			}

			if (!folders.Any ())
				return;

			var deleteOnlyQuestion = new QuestionMessage () {
				AllowApplyToAll = folders.Count > 1,
				SecondaryText = GettextCatalog.GetString ("The directory and any files it contains will be permanently removed from your hard disk. ")
			};
			deleteOnlyQuestion.Buttons.Add (AlertButton.Delete);
			deleteOnlyQuestion.Buttons.Add (AlertButton.Cancel);

			foreach (WorkspaceFolder folder in folders) {
				deleteOnlyQuestion.Text = GettextCatalog.GetString ("Are you sure you want to remove directory {0}?", folder.Name);
				AlertButton result = MessageService.AskQuestion (deleteOnlyQuestion);
				if (result == AlertButton.Delete) {
					DeleteFolder (folder);
				} else {
					return;
				}
			}
		}

		static void DeleteFolder (WorkspaceFolder folder)
		{
			try {
				if (Directory.Exists (folder.BaseDirectory)) {
					FileService.DeleteDirectory (folder.BaseDirectory);
				}
			} catch (Exception ex) {
				MessageService.ShowError (GettextCatalog.GetString ("The folder {0} could not be deleted from disk: {1}", folder.BaseDirectory, ex.Message));
			}
		}
	}
}
