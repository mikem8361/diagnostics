// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.DebugServices;
using System;
using System.Diagnostics;
using System.IO;
using System.Security;

namespace Microsoft.Diagnostics.ExtensionCommands
{
    [Command(Name = "logging", Help = "Enable/disable internal logging", Platform = CommandPlatform.Global)]
    public class LoggingCommand : CommandBase
    {
        [Option(Name = "enable", Help = "Enable internal logging.")]
        public bool Enable { get; set; }

        [Option(Name = "logfile", Help = "Log file name.")]
        public string LogFile { get; set; }

        [Option(Name = "disable", Help = "Disable internal logging.")]
        public bool Disable { get; set; }

        private const string ListenerName = "Analyze.LoggingListener";

        public override void Invoke()
        {
            if (Enable || LogFile is not null) {
                EnableLogging(LogFile);
            }
            else if (Disable) {
                DisableLogging();
            }
            TraceListener listener = Trace.Listeners[ListenerName];
            WriteLine("Logging is {0}", listener != null ? "enabled" : "disabled");
            if (listener is LoggingListener loggingListener && loggingListener.LogFile != null)
            {
                WriteLine(loggingListener.LogFile);
            }
        }

        public static void Initialize(string logfile = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(logfile))
                {
                    logfile = Environment.GetEnvironmentVariable("DOTNET_ENABLED_SOS_LOGGING");
                }
                if (!string.IsNullOrWhiteSpace(logfile))
                {
                    if (logfile == "1")
                    {
                        EnableLogging();
                    }
                    else
                    {
                        EnableLogging(logfile);
                    }
                }
            }
            catch (Exception ex) when ( ex is IOException || ex is NotSupportedException || ex is SecurityException || ex is UnauthorizedAccessException)
            {
            }
        }

        public static void EnableLogging(string logfile = null)
        {
            if (Trace.Listeners[ListenerName] is null)
            {
                Trace.Listeners.Add(new LoggingListener(logfile));
                Trace.AutoFlush = true;
            }
        }

        public static void DisableLogging()
        {
            Trace.Listeners[ListenerName]?.Close();
            Trace.Listeners.Remove(ListenerName);
        }

        class LoggingListener : TraceListener
        {
            private readonly StreamWriter _writer;

            internal readonly string LogFile;

            internal LoggingListener(string logfile)
                : base(ListenerName)
            {
                LogFile = logfile;

                Stream stream = null;
                try
                {
                    if (logfile is not null)
                    {
                        stream = new FileStream(logfile, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                    }
                }
                finally
                {
                    if (stream is null)
                    {
                        stream = System.Console.OpenStandardOutput();
                    }
                    _writer = new StreamWriter(stream) {
                        AutoFlush = true
                    };
                }
            }

            public override void Close()
            {
                _writer.Flush();
                _writer.BaseStream.Flush();
                _writer.BaseStream.Close();
                base.Close();
            }

            public override void Write(string message)
            {
                _writer.Write(message);
            }

            public override void WriteLine(string message)
            {
                _writer.WriteLine(message);
            }
        }
    }
}
