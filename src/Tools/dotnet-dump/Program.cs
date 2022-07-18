// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Internal.Common.Commands;
using Microsoft.Tools.Common;
using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Tools.Dump
{
    class Program
    {
        public static Task<int> Main(string[] args)
        {
            var parser = new CommandLineBuilder()
                .AddCommand(CollectCommand())
                .AddCommand(AnalyzeCommand())
                .AddCommand(ProcessStatusCommandHandler.ProcessStatusCommand("Lists the dotnet processes that dumps can be collected from."))
                .UseDefaults()
                .Build();

            return parser.InvokeAsync(args);
        }

        private static Command CollectCommand() =>
            new Command( name: "collect", description: "Capture dumps from a process")
            {
                // Handler
                //CommandHandler.Create<IConsole, int, string, bool, bool, Dumper.DumpTypeOption, string>(new Dumper().Collect),
                // Options
                ProcessIdOption(), OutputOption(), DiagnosticLoggingOption(), CrashReportOption(), TypeOption(), ProcessNameOption()
            };

        private static Option ProcessIdOption() =>
            new Option<int>(
                aliases: new[] { "-p", "--process-id" },
                description: "The process id to collect a memory dump.")
            {
                ArgumentHelpName = "pid"
            };

        private static Option ProcessNameOption() =>
            new Option<string>(
                aliases: new[] { "-n", "--name" },
                description: "The name of the process to collect a memory dump.")
            {
                ArgumentHelpName = "name"
            };

        private static Option OutputOption() =>
            new Option<string>( 
                aliases: new[] { "-o", "--output" },
                description: @"The path where collected dumps should be written. Defaults to '.\dump_YYYYMMDD_HHMMSS.dmp' on Windows and './core_YYYYMMDD_HHMMSS' 
on Linux where YYYYMMDD is Year/Month/Day and HHMMSS is Hour/Minute/Second. Otherwise, it is the full path and file name of the dump.") 
            {
                ArgumentHelpName = "output_dump_path"
            };

        private static Option DiagnosticLoggingOption() =>
            new Option<bool>(
                name: "--diag", 
                description: "Enable dump collection diagnostic logging.") 
            {
                ArgumentHelpName = "diag"
            };

        private static Option CrashReportOption() =>
            new Option<bool>(
                name: "--crashreport", 
                description: "Enable crash report generation.") 
            {
                ArgumentHelpName = "crashreport"
            };

        private static Option TypeOption() =>
            new Option<Dumper.DumpTypeOption>(
                name: "--type",
                getDefaultValue: () => Dumper.DumpTypeOption.Full,
                description: @"The dump type determines the kinds of information that are collected from the process. There are several types: Full - The largest dump containing all memory including the module images. Heap - A large and relatively comprehensive dump containing module lists, thread lists, all stacks, exception information, handle information, and all memory except for mapped images. Mini - A small dump containing module lists, thread lists, exception information and all stacks. Triage - A small dump containing module lists, thread lists, exception information, all stacks and PII removed.")
            {
                ArgumentHelpName = "dump_type"
            };

        private static Command AnalyzeCommand() =>
            new Command(
                name: "analyze", 
                description: "Starts an interactive shell with debugging commands to explore a dump")
            {
                // Handler
                //SetHandler<FileInfo, string[]>(new Analyzer().Analyze) 
                // Arguments and Options
                DumpPath(),
                RunCommand() 
            }; 

        private static Argument DumpPath() =>
            new Argument<FileInfo>(
                name: "dump_path")
            {
                Description = "Name of the dump file to analyze."
            }.ExistingOnly();

        private static Option RunCommand() =>
            new Option<string[]>(
                aliases: new[] { "-c", "--command" }, 
                getDefaultValue: () => Array.Empty<string>(),
                description: "Runs the command on start. Multiple instances of this parameter can be used in an invocation to chain commands. Commands will get run in the order that they are provided on the command line. If you want dotnet dump to exit after the commands, your last command should be 'exit'.") 
            {
                ArgumentHelpName = "command",
                Arity = ArgumentArity.ZeroOrMore,
            };
    }
}
