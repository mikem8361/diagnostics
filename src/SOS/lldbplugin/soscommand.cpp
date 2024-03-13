// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "sosplugin.h"
#include <dlfcn.h>
#include <string.h>
#include <string>

void *g_sosHandle = nullptr;

class sosCommand : public lldb::SBCommandPluginInterface
{
    const char *m_command;
    const char *m_arguments;

public:
    sosCommand(const char* command, const char* arguments = nullptr)
    {
        m_command = command;
        m_arguments = arguments;
    }

    ~sosCommand()
    {
        g_services->Output(IOutputService::OutputType::Error, "~sosCommand %s\n", m_command);
    }

    virtual bool
    DoExecute (lldb::SBDebugger debugger,
               char** arguments,
               lldb::SBCommandReturnObject &result)
    {
        result.SetStatus(lldb::eReturnStatusSuccessFinishResult);

        const char* sosCommand = m_command;
        if (sosCommand == nullptr)
        {
            if (arguments == nullptr || *arguments == nullptr)
            {
                sosCommand = "Help";
            }
            else
            {
                sosCommand = *arguments++;
                if (g_services->ExecuteCommand(sosCommand, arguments, result))
                {
                    return result.Succeeded();
                }
            }
        }

        LoadSos();

        if (g_sosHandle != nullptr)
        {
            CommandFunc commandFunc = (CommandFunc)dlsym(g_sosHandle, sosCommand);
            if (commandFunc)
            {
                std::string str;
                if (m_arguments)
                {
                    str.append(m_arguments);
                    str.append(" ");
                }
                if (arguments != nullptr)
                {
                    for (const char* arg = *arguments; arg; arg = *(++arguments))
                    {
                        str.append(arg);
                        str.append(" ");
                    }
                }
                g_services->FlushCheck();
                const char* sosArgs = str.c_str();
                HRESULT hr = commandFunc(g_services, sosArgs);
                if (hr != S_OK)
                {
                    result.SetStatus(lldb::eReturnStatusFailed);
                    g_services->Output(IOutputService::OutputType::Error, "%s %s failed\n", sosCommand, sosArgs);
                }
            }
            else
            {
                result.SetStatus(lldb::eReturnStatusFailed);
                g_services->Output(IOutputService::OutputType::Error, "SOS command '%s' not found %s\n", sosCommand, dlerror());
            }
        }

        return result.Succeeded();
    }

    void
    LoadSos()
    {
        if (g_sosHandle == nullptr)
        {
            const char *loadDirectory = g_services->GetPluginModuleDirectory();
            if (loadDirectory != nullptr)
            {
                g_sosHandle = LoadModule(loadDirectory, MAKEDLLNAME_A("sos"));
                if (g_sosHandle != nullptr)
                {
                    InitializeFunc initializeFunc = (InitializeFunc)dlsym(g_sosHandle, SOSInitialize);
                    if (initializeFunc)
                    {
                        HRESULT hr = initializeFunc(GetHost(), GetDebuggerServices(), GetOutputService());
                        if (hr != S_OK)
                        {
                            g_services->Output(IOutputService::OutputType::Error, SOSInitialize " failed %08x\n", hr);
                        }
                    }
                }
            }
        }
    }

    void *
    LoadModule(const char *loadDirectory, const char *moduleName)
    {
        std::string modulePath(loadDirectory);
        modulePath.append(moduleName);

        void *moduleHandle = dlopen(modulePath.c_str(), RTLD_NOW);
        if (moduleHandle == nullptr)
        {
            g_services->Output(IOutputService::OutputType::Error, "Could not load '%s' - %s\n", modulePath.c_str(), dlerror());
        }

        return moduleHandle;
    }
};

bool
sosCommandInitialize(LLDBServices* services)
{
    services->AddCommand("sos", new sosCommand(nullptr), "Executes various coreclr debugging commands. Use the syntax 'sos <command - name> <args>'. For more information, see 'soshelp'.");
    services->AddCommand("ext", new sosCommand(nullptr), "Executes various coreclr debugging commands. Use the syntax 'sos <command - name> <args>'. For more information, see 'soshelp'.");
    services->AddManagedCommand("analyzeoom", "Provides a stack trace of managed code only.");
    services->AddCommand("bpmd", new sosCommand("bpmd"), "Creates a breakpoint at the specified managed method in the specified module.");
    services->AddManagedCommand("assemblies", "Lists the managed modules in the process.");
    services->AddManagedCommand("clrmodules", "Lists the managed modules in the process.");
    services->AddCommand("clrstack", new sosCommand("ClrStack"), "Provides a stack trace of managed code only.");
    services->AddCommand("clrthreads", new sosCommand("Threads"), "Lists the managed threads running.");
    services->AddCommand("clru", new sosCommand("u"), "Displays an annotated disassembly of a managed method.");
    services->AddManagedCommand("crashinfo", "Displays the Native AOT crash info.");
    services->AddCommand("dbgout", new sosCommand("dbgout"), "Enables/disables (-off) internal SOS logging.");
    services->AddCommand("dumpalc", new sosCommand("DumpALC"), "Displays details about a collectible AssemblyLoadContext to which the specified object is loaded.");
    services->AddCommand("dumparray", new sosCommand("DumpArray"), "Displays details about a managed array.");
    services->AddManagedCommand("dumpasync", "Displays information about async \"stacks\" on the garbage-collected heap.");
    services->AddCommand("dumpassembly", new sosCommand("DumpAssembly"), "Displays details about an assembly.");
    services->AddCommand("dumpclass", new sosCommand("DumpClass"), "Displays information about a EE class structure at the specified address.");
    services->AddCommand("dumpdelegate", new sosCommand("DumpDelegate"), "Displays information about a delegate.");
    services->AddCommand("dumpdomain", new sosCommand("DumpDomain"), "Displays information about the all assemblies within all the AppDomains or the specified one.");
    services->AddCommand("dumpgcdata", new sosCommand("DumpGCData"), "Displays information about the GC data.");
    services->AddManagedCommand("dumpheap", "Displays info about the garbage-collected heap and collection statistics about objects.");
    services->AddManagedCommand("dumphttp", "Displays information about HTTP requests.");
    services->AddManagedCommand("dumprequests", "Displays all currently active incoming HTTP requests.");
    services->AddCommand("dumpil", new sosCommand("DumpIL"), "Displays the Microsoft intermediate language (MSIL) that's associated with a managed method.");
    services->AddCommand("dumplog", new sosCommand("DumpLog"), "Writes the contents of an in-memory stress log to the specified file.");
    services->AddCommand("dumpmd", new sosCommand("DumpMD"), "Displays information about a MethodDesc structure at the specified address.");
    services->AddCommand("dumpmodule", new sosCommand("DumpModule"), "Displays information about a EE module structure at the specified address.");
    services->AddCommand("dumpmt", new sosCommand("DumpMT"), "Displays information about a method table at the specified address.");
    services->AddCommand("dumpobj", new sosCommand("DumpObj"), "Displays info about an object at the specified address.");
    services->AddManagedCommand("dumpruntimetypes", "Finds all System.RuntimeType objects in the GC heap and prints the type name and MethodTable they refer too.");
    services->AddCommand("dumpsig", new sosCommand("DumpSig"), "Dumps the signature of a method or field specified by '<sigaddr> <moduleaddr>'.");
    services->AddCommand("dumpsigelem", new sosCommand("DumpSigElem"), "Dumps a single element of a signature object.");
    services->AddCommand("dumpstack", new sosCommand("DumpStack"), "Displays a native and managed stack trace.");
    services->AddManagedCommand("dumpstackobjects", "Displays all managed objects found within the bounds of the current stack.");
    services->AddManagedCommand("dso", "Displays all managed objects found within the bounds of the current stack.");
    services->AddCommand("dumpvc", new sosCommand("DumpVC"), "Displays info about the fields of a value class.");
    services->AddManagedCommand("eeheap", "Displays info about process memory consumed by internal runtime data structures.");
    services->AddCommand("eestack", new sosCommand("EEStack"), "Runs dumpstack on all threads in the process.");
    services->AddCommand("eeversion", new sosCommand("EEVersion"), "Displays information about the runtime and SOS versions.");
    services->AddCommand("ehinfo", new sosCommand("EHInfo"), "Displays the exception handling blocks in a JIT-ed method.");
    services->AddManagedCommand("finalizequeue", "Displays all objects registered for finalization.");
    services->AddCommand("findappdomain", new sosCommand("FindAppDomain"), "Attempts to resolve the AppDomain of a GC object.");
    services->AddCommand("findroots", new sosCommand("FindRoots"), "Finds and displays object roots across GC collections.");
    services->AddCommand("gchandles", new sosCommand("GCHandles"), "Displays statistics about garbage collector handles in the process.");
    services->AddManagedCommand("gcheapstat", "Displays statistics about garbage collector.");
    services->AddCommand("gcinfo", new sosCommand("GCInfo"), "Displays info JIT GC encoding for a method.");
    services->AddManagedCommand("gcroot", "Displays info about references (or roots) to an object at the specified address.");
    services->AddManagedCommand("gcwhere", "Displays the location in the GC heap of the specified address.");
    services->AddCommand("histclear", new sosCommand("HistClear"), "Releases any resources used by the family of Hist commands.");
    services->AddCommand("histinit", new sosCommand("HistInit"), "Initializes the SOS structures from the stress log saved in the debuggee.");
    services->AddCommand("histobj", new sosCommand("HistObj"), "Examines all stress log relocation records and displays the chain of garbage collection relocations that may have led to the address passed in as an argument.");
    services->AddCommand("histobjfind", new sosCommand("HistObjFind"), "Displays all the log entries that reference an object at the specified address.");
    services->AddCommand("histroot", new sosCommand("HistRoot"), "Displays information related to both promotions and relocations of the specified root.");
    services->AddCommand("histstats", new sosCommand("HistStats"), "Displays stress log stats.");
    services->AddCommand("ip2md", new sosCommand("IP2MD"), "Displays the MethodDesc structure at the specified address in code that has been JIT-compiled.");
    services->AddManagedCommand("listnearobj", "Displays the object preceding and succeeding the specified address.");
    services->AddManagedCommand("loadsymbols", "Loads the .NET Core native module symbols.");
    services->AddManagedCommand("logging", "Enables/disables internal SOS logging.");
    services->AddCommand("name2ee", new sosCommand("Name2EE"), "Displays the MethodTable structure and EEClass structure for the specified type or method in the specified module.");
    services->AddManagedCommand("objsize", "Displays the size of the specified object.");
    services->AddManagedCommand("pathto", "Displays the GC path from <root> to <target>.");
    services->AddCommand("pe", new sosCommand("PrintException"), "Displays and formats fields of any object derived from the Exception class at the specified address.");
    services->AddCommand("printexception", new sosCommand("PrintException"), "Displays and formats fields of any object derived from the Exception class at the specified address.");
    services->AddCommand("runtimes", new sosCommand("runtimes"), "Lists the runtimes in the target or change the default runtime.");
    services->AddCommand("stoponcatch", new sosCommand("StopOnCatch"), "Target process will break the next time a managed exception is caught during execution.");
    services->AddCommand("setclrpath", new sosCommand("SetClrPath"), "Sets the path to load the runtime DAC/DBI files.");
    services->AddManagedCommand("setsymbolserver", "Enables the symbol server support ");
    services->AddCommand("soshelp", new sosCommand("Help"), "Displays all available commands when no parameter is specified, or displays detailed help information about the specified command: 'soshelp <command>'.");
    services->AddCommand("sosstatus", new sosCommand("SOSStatus"), "Displays the global SOS status.");
    services->AddCommand("sosflush", new sosCommand("SOSFlush"), "Resets the internal cached state.");
    services->AddCommand("syncblk", new sosCommand("SyncBlk"), "Displays the SyncBlock holder info.");
    services->AddManagedCommand("threadpool", "Displays info about the runtime thread pool.");
    services->AddCommand("threadstate", new sosCommand("ThreadState"), "Pretty prints the meaning of a threads state.");
    services->AddCommand("token2ee", new sosCommand("token2ee"), "Displays the MethodTable structure and MethodDesc structure for the specified token and module.");
    services->AddManagedCommand("verifyheap", "Checks the GC heap for signs of corruption.");
    services->AddManagedCommand("verifyobj", "Checks the object that is passed as an argument for signs of corruption.");
    services->AddManagedCommand("traverseheap", "Writes out heap information to a file in a format understood by the CLR Profiler.");
    return true;
}
