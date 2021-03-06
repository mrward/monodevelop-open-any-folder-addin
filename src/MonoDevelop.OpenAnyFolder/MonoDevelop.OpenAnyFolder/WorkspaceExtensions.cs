﻿//
// WorkspaceExtensions.cs
//
// Author:
//       Matt Ward <matt.ward@xamarin.com>
//
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

using MonoDevelop.Projects;
using MonoDevelop.Ide;

namespace MonoDevelop.OpenAnyFolder
{
	static class WorkspaceExtensions
	{
		static WorkspaceExtensions ()
		{
			if (!IdeApp.IsInitialized)
				return;

			IdeApp.Workspace.WorkspaceItemOpened += WorkspaceItemOpened;
		}

		static void WorkspaceItemOpened (object sender, WorkspaceItemEventArgs e)
		{
			var workspace = e.Item as Workspace;
			if (workspace != null && workspace.IsFolder ()) {
				DesktopService.RecentFiles.UpdateDisplayNameForFolder (workspace.FileName);
			}
		}

		public static void MarkAsFolder (this WorkspaceObject workspace)
		{
			workspace.ExtendedProperties.Add ("Workspace.IsFolder", "true");
		}

		public static bool IsFolder (this WorkspaceObject workspace)
		{
			return workspace.ExtendedProperties.Contains ("Workspace.IsFolder");
		}
	}
}
