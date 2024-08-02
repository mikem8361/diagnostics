// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.Diagnostics.DebugServices;

namespace Microsoft.Diagnostics.ExtensionCommands
{
    [Command(Name = "targets", Help = "Lists targets or creates targets.")]
    [Command(Name = "settarget", DefaultOptions = "--set", Help = "Sets the current target.")]
    [Command(Name = "closetarget", DefaultOptions = "--close", Aliases = new string[] { "closedump" }, Help = "Closes a target.")]
    public class TargetsCommand : CommandBase
    {
        [ServiceImport]
        public IHost Host { get; set; }

        [ServiceImport]
        public IContextService ContextService { get; set; }

        [ServiceImport(Optional = true)]
        public IDumpTargetFactory DumpTargetFactory { get; set; }

        [ServiceImport(Optional = true)]
        public ILiveTargetFactory LiveTargetFactory { get; set; }

        [Argument(Help = "The target id.")]
        public int? TargetId { get; set; } = null;

        [Option(Name = "--set", Aliases = new string[] { "-s" }, Help = "Sets the current target")]
        public bool Set { get; set; }

        [Option(Name = "--close", Aliases = new string[] { "-c" }, Help = "Close/destroy target.")]
        public bool Close { get; set; }

        public override void Invoke()
        {
            if (Set && TargetId.HasValue)
            {
                ContextService.SetCurrentTarget(TargetId.Value);
            }
            else if (Close)
            {
                ITarget target;
                if (TargetId.HasValue)
                {
                    target = Host.EnumerateTargets().SingleOrDefault((target) => target.Id == TargetId.Value);
                    if (target is null)
                    {
                        throw new DiagnosticsException($"Invalid target id {TargetId.Value}");
                    }
                }
                else
                {
                    target = ContextService.GetCurrentTarget();
                    if (target is null)
                    {
                        throw new DiagnosticsException("No current target to close");
                    }
                }
                target.Destroy();
                WriteLine($"Closed target #{target.Id} {target}");
                return;
            }

            // Display the current target star ("*") only if there is more than one target
            bool displayStar = Host.EnumerateTargets().Count() > 1;
            ITarget currentTarget = ContextService.GetCurrentTarget();

            foreach (ITarget target in Host.EnumerateTargets())
            {
                string current = displayStar ? (target == currentTarget ? "*" : " ") : "";
                WriteLine($"{current}{target.Id} {target}");
            }
        }
    }

    [Command(Name = "opendump", Help = "Opens a new dump target.")]
    public class OpendumpCommand : TargetsCommand
    {
        [Argument(Help = "The dump file path.")]
        public string DumpFile { get; set; }

        public override void Invoke()
        {
            if (!string.IsNullOrEmpty(DumpFile))
            {
                if (DumpTargetFactory is null)
                {
                    throw new DiagnosticsException("Creating dump targets is not supported");
                }
                ITarget target = DumpTargetFactory.OpenDump(DumpFile);
                ContextService.SetCurrentTarget(target.Id);
                WriteLine($"Loaded core dump '{DumpFile}' target #{target.Id}");
            }
        }
    }

    [Command(Name = "attach", Help = "Non-invasive attach to process.")]
    public class AttachCommand : TargetsCommand
    {
        [Argument(Help = "Process id to attach.")]
        public int? ProcessId { get; set; } = null;

        public override void Invoke()
        {
            if (ProcessId.HasValue)
            {
                if (LiveTargetFactory is null)
                {
                    throw new DiagnosticsException("Attaching to live targets is not supported");
                }
                ITarget target = LiveTargetFactory.Attach(ProcessId.Value);
                ContextService.SetCurrentTarget(target.Id);
                WriteLine($"Attached to process {ProcessId.Value} target #{target.Id}");
            }
        }
    }
}
