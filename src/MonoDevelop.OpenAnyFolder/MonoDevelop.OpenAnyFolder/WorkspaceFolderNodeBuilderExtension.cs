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
			return typeof(IFolderItem).IsAssignableFrom (dataType);
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
					.Where (file => !new FilePath (file).FileName.StartsWith (".", StringComparison.OrdinalIgnoreCase))
					.Select (file => new SystemFile (file, null, false)));

				treeBuilder.AddChildren (Directory.EnumerateDirectories (path)
					.Select (folder => new ProjectFolder (folder, null)));
			}
		}
	}
}
