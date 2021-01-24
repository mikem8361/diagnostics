// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.DebugServices;
using System.Reflection;
using System.IO;

namespace Microsoft.Diagnostics.ExtensionCommands
{
    [Command(Name = "load", Help = "Load command extension assembly.")]
    public class LoadCommand : CommandBase
    {
        public ICommandService CommandService { get; set; }

        [Argument(Name = "path", Help = "Extension assembly path.")]
        public string ExtensionPath { get; set; }

        public override void Invoke()
        {
            string path = Path.GetFullPath(ExtensionPath);
            WriteLine("Loading {0}", path);

            Assembly assembly = Assembly.LoadFrom(path);
            CommandService.AddCommands(assembly);

            WriteLine("Load successful");
        }
    }
}
