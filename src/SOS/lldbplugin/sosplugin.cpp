// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "sosplugin.h"

namespace lldb {
    bool PluginInitialize (lldb::SBDebugger debugger);
}

#if defined(HOST_ARM64)
// Flag to check if atomics feature is available on the machine
bool g_arm64_atomics_present = false;
#endif

LLDBServices* g_services = nullptr;

bool Uninitialize(void* baton, const char** argv)
{
    Extensions::Uninitialize();
    return false;
}

bool lldb::PluginInitialize(lldb::SBDebugger debugger)
{
    g_services = new LLDBServices(debugger);
    Extensions::Initialize(g_services, g_services);
    debugger.GetCommandInterpreter().SetCommandOverrideCallback("quit", Uninitialize, nullptr);
    sosCommandInitialize(g_services);
    setsostidCommandInitialize(g_services);
    sethostruntimeCommandInitialize(g_services);
    return true;
}
