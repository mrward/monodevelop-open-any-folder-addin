//
// NewWorkspaceFileHandler.cs
//
// Author:
//       Matt Ward <matt.ward@microsoft.com>
//
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

using System;
using System.Linq;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui.Components;
using MonoDevelop.Ide.Gui.Pads;
using MonoDevelop.Projects;

namespace MonoDevelop.OpenAnyFolder
{
	class NewWorkspaceFileHandler : CommandHandler
	{
		protected override void Update (CommandInfo info)
		{
			Workspace workspace = GetWorkspace ();
			info.Visible = (workspace?.IsFolder () == true);
		}

		protected override void Run (object dataItem)
		{
			SolutionPad solutionPad = GetSolutionPad ();
			Workspace workspace = GetWorkspace (solutionPad);
			if (workspace == null)
				return;

			if (!IdeApp.ProjectOperations.CreateProjectFile (null, workspace.BaseDirectory))
				return;

			ITreeNavigator navigator = solutionPad.TreeView.GetNodeAtObject (workspace);
			if (navigator != null)
				navigator.Expanded = true;

			if (IdeApp.Workbench.ActiveDocument != null)
				IdeApp.Workbench.ActiveDocument.Window.SelectWindow ();
		}

		static Workspace GetWorkspace ()
		{
			SolutionPad solutionPad = GetSolutionPad ();
			return GetWorkspace (solutionPad);
		}

		static SolutionPad GetSolutionPad ()
		{
			var pad = IdeApp.Workbench.Pads.FirstOrDefault (p => p.Id == "ProjectPad");
			return pad?.Content as SolutionPad;
		}

		static Workspace GetWorkspace (SolutionPad solutionPad)
		{
			if (solutionPad == null)
				return null;

			var selectedNode = solutionPad.TreeView.GetSelectedNode ();
			return selectedNode?.DataItem as Workspace;
		}
	}
}
