using System;
using System.Diagnostics;
using System.IO.Pipes;

class Simple
{
    public static int Main(string[] args)
    {
        string pipeServerName = args[0];
        int pid = Process.GetCurrentProcess().Id;
        Console.WriteLine("{0} SimpleDebuggee: pipe server: {1}", pid, pipeServerName);

        if (pipeServerName != null)
        {
            var pipeStream = new NamedPipeClientStream(pipeServerName);
            Console.WriteLine("{0} SimpleDebuggee: connecting to pipe", pid);
            pipeStream.Connect();

            // Wait for server to send something
            int input = pipeStream.ReadByte();

            Console.WriteLine("{0} SimpleDebuggee: waking up {1}", pid, input);
        }
        return 0;
    }
}
