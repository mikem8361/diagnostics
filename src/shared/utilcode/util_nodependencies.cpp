// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//*****************************************************************************
// Util_NoDependencies.cpp
//

//
// This contains a bunch of C++ utility classes needed also for UtilCode without dependencies
// (standalone version without CLR/clr.dll/mscoree.dll dependencies).
//
//*****************************************************************************

#include "stdafx.h"
#include "utilcode.h"
#include "ex.h"

void OutputDebugStringUtf8(LPCUTF8 utf8DebugMsg)
{
#ifdef TARGET_UNIX
    OutputDebugStringA(utf8DebugMsg);
#else
    if (utf8DebugMsg == NULL)
        utf8DebugMsg = "";

    MAKE_WIDEPTR_FROMUTF8_NOTHROW(wideDebugMsg, utf8DebugMsg);
    OutputDebugStringW(wideDebugMsg);
#endif // !TARGET_UNIX
}

BOOL ThreadWillCreateGuardPage(SIZE_T sizeReservedStack, SIZE_T sizeCommittedStack)
{
    // We need to make sure there will be a reserved but never committed page at the end
    // of the stack. We do here the check NT does when it creates the user stack to decide
    // if there is going to be a guard page. However, that is not enough, as if we only
    // have a guard page, we have nothing to protect us from going pass it. Well, in
    // fact, there is something that we will protect you, there are certain places
    // (RTLUnwind) in NT that will check that the current frame is within stack limits.
    // If we are not it will bomb out. We will also bomb out if we touch the hard guard
    // page.
    //
    // For situation B, teb->StackLimit is at the beginning of the user stack (ie
    // before updating StackLimit it checks if it was able to create a new guard page,
    // in this case, it can't), which makes the check fail in RtlUnwind.
    //
    //    Situation A  [ Hard guard page | Guard page | user stack]
    //
    //    Situation B  [ Guard page | User stack ]
    //
    //    Situation C  [ User stack ( no room for guard page) ]
    //
    //    Situation D (W9x) : Guard page or not, w9x has a 64k reserved region below
    //                        the stack, we don't need any checks at all
    //
    // We really want to be in situation A all the time, so we add one more page
    // to our requirements (we require guard page + hard guard)

    SYSTEM_INFO sysInfo;
    ::GetSystemInfo(&sysInfo);

    // OS rounds up sizes the following way to decide if it marks a guard page
    sizeReservedStack = ALIGN(sizeReservedStack, ((size_t)sysInfo.dwAllocationGranularity));   // Allocation granularity
    sizeCommittedStack = ALIGN(sizeCommittedStack, ((size_t)sysInfo.dwPageSize));  // Page Size

    // OS wont create guard page, we can't execute managed code safely.
    // We also have to make sure we have a 'hard' guard, thus we add another
    // page to the memory we would need comitted.
    // That is, the following code will check if sizeReservedStack is at least 2 pages
    // more than sizeCommittedStack.
    return (sizeReservedStack > sizeCommittedStack + ((size_t)sysInfo.dwPageSize));
} // ThreadWillCreateGuardPage
