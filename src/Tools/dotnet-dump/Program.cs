// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Internal.Common.Commands;
using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Tools.Dump
{
    class Program
    {
        public static Task<int> Main(string[] args)
        {
            var builder = new CommandLineBuilder().UseDefaults();
            builder.Command.AddCommand(CollectCommand());
            builder.Command.AddCommand(AnalyzeCommand());
            builder.Command.AddCommand(ProcessStatusCommandHandler.ProcessStatusCommand("Lists the dotnet processes that dumps can be collected from."));
            return builder.Build().InvokeAsync(args);
        }

        private static Command CollectCommand()
        {
            Command command = new(name: "collect", description: "Capture dumps from a process")
            {
                // Options
                ProcessIdOption(), OutputOption(), DiagnosticLoggingOption(), CrashReportOption(), TypeOption(), ProcessNameOption()
            };
            command.SetHandler(new Dumper().Collect, ProcessIdOption(), OutputOption(), DiagnosticLoggingOption(), CrashReportOption(), TypeOption(), ProcessNameOption());
            return command;
        }

        private static Option<int> ProcessIdOption() =>
            new Option<int>(
                aliases: new[] { "-p", "--process-id" },
                description: "The process id to collect a memory dump.")
            {
                ArgumentHelpName = "pid"
            };

        private static Option<string> ProcessNameOption() =>
            new Option<string>(
                aliases: new[] { "-n", "--name" },
                description: "The name of the process to collect a memory dump.")
            {
                ArgumentHelpName = "name"
            };

        private static Option<string> OutputOption() =>
            new Option<string>( 
                aliases: new[] { "-o", "--output" },
                description: @"The path where collected dumps should be written. Defaults to '.\dump_YYYYMMDD_HHMMSS.dmp' on Windows and './core_YYYYMMDD_HHMMSS' 
on Linux where YYYYMMDD is Year/Month/Day and HHMMSS is Hour/Minute/Second. Otherwise, it is the full path and file name of the dump.") 
            {
                ArgumentHelpName = "output_dump_path"
            };

        private static Option<bool> DiagnosticLoggingOption() =>
            new Option<bool>(
                name: "--diag", 
                description: "Enable dump collection diagnostic logging.") 
            {
                ArgumentHelpName = "diag"
            };

        private static Option<bool> CrashReportOption() =>
            new Option<bool>(
                name: "--crashreport", 
                description: "Enable crash report generation.") 
            {
                ArgumentHelpName = "crashreport"
            };

        private static Option<Dumper.DumpTypeOption> TypeOption() =>
            new Option<Dumper.DumpTypeOption>(
                name: "--type",
                getDefaultValue: () => Dumper.DumpTypeOption.Full,
                description: @"The dump type determines the kinds of information that are collected from the process. There are several types: Full - The largest dump containing all memory including the module images. Heap - A large and relatively comprehensive dump containing module lists, thread lists, all stacks, exception information, handle information, and all memory except for mapped images. Mini - A small dump containing module lists, thread lists, exception information and all stacks. Triage - A small dump containing module lists, thread lists, exception information, all stacks and PII removed.")
            {
                ArgumentHelpName = "dump_type"
            };

        private static Command AnalyzeCommand()
        {
            var command = new Command(
                name: "analyze",
                description: "Starts an interactive shell with debugging commands to explore a dump")
            {
                // Arguments and Options
                DumpPath(), RunCommand()
            };
            command.SetHandler(new Analyzer().Analyze, DumpPath(), RunCommand()); 
            return command;
        }

        private static Argument<FileInfo> DumpPath() =>
            new Argument<FileInfo>(
                name: "dump_path")
            {
                Description = "Name of the dump file to analyze."
            }.ExistingOnly();

        private static Option<string[]> RunCommand() =>
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
