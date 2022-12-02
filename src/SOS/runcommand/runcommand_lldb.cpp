#include <lldb/API/LLDB.h>
#include <string.h>
#include <string>

using namespace lldb;

#define END_OF_COMMAND_SUCCESS "<END_COMMAND_OUTPUT>\n"
#define END_OF_COMMAND_ERROR "<END_COMMAND_ERROR>\n"
#define COMMAND_PROMPT "<COMMAND_PROMPT>\n"

void Printf(lldb::SBDebugger debugger, const char* message)
{
    FILE* file = debugger.GetOutputFileHandle();
    fputs(message, file);
    fflush(file);
}

class runcommandCommand : public SBCommandPluginInterface
{
public:
    runcommandCommand()
    {
    }

    virtual bool DoExecute(SBDebugger debugger, char** arguments, SBCommandReturnObject &result)
    {
        result.SetStatus(eReturnStatusSuccessFinishResult);

        // If there are arguments passed, execute the command, otherwise just echo <END_COMMAND_OUTPUT>
        if (arguments != nullptr && arguments[0] != nullptr)
        {
            // Build all the possible arguments into a string
            std::string commandLine;
            for (const char* arg = *arguments; arg != nullptr; arg = *(++arguments))
            {
                commandLine.append(arg);
                commandLine.append(" ");
            }

            // Execute command
            SBCommandInterpreter interpreter = debugger.GetCommandInterpreter();
            ReturnStatus status = interpreter.HandleCommand(commandLine.c_str(), result);
            result.SetStatus(status);
        }

        if (result.Succeeded())
        {
            result.Printf(END_OF_COMMAND_SUCCESS);
        }
        else
        {
            result.Printf(END_OF_COMMAND_ERROR);
        }

        return result.Succeeded();
    }
};

namespace lldb {
    bool PluginInitialize(lldb::SBDebugger debugger);
}

bool lldb::PluginInitialize(lldb::SBDebugger debugger)
{
    lldb::SBCommandInterpreter interpreter = debugger.GetCommandInterpreter();
    lldb::SBCommand command = interpreter.AddCommand("runcommand", new runcommandCommand(), "Runs a command for the test harness");

    debugger.SetPrompt(COMMAND_PROMPT);

    Printf(debugger, END_OF_COMMAND_SUCCESS);
    Printf(debugger, COMMAND_PROMPT);
    return true;
}
