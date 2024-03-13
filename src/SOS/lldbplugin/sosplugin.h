// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma once

#include <lldb/API/LLDB.h>
#include <mstypes.h>
#define DEFINE_EXCEPTION_RECORD
#include <lldbservices.h>
#include <outputservice.h>
#include <extensions.h>
#include <dbgtargetcontext.h>
#include <specialdiaginfo.h>
#include <specialthreadinfo.h>
#include "services.h"

#define SOSInitialize "SOSInitializeByHost"

typedef HRESULT (*CommandFunc)(ILLDBServices* services, const char* args);
typedef HRESULT (*InitializeFunc)(IUnknown* punk, IDebuggerServices* debuggerServices, IOutputService* outputService);

extern char *g_coreclrDirectory;
extern LLDBServices* g_services;

bool 
sosCommandInitialize(LLDBServices* services);

bool
setsostidCommandInitialize(LLDBServices* services);

bool
sethostruntimeCommandInitialize(LLDBServices* services);
