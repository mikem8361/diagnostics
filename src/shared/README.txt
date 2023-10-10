The "shared" directory contains the common code between the runtime and diagnostics repros.

It is also shared by the dbgshim and SOS components.

Updated last on 10/12/2023 from the runtime repo commit hash: 5b7ab0416fe04b23b026534cef36731172c7675c.

runtime/src/coreclr/inc -> diagnostics/src/shared/inc
runtime/src/coreclr/debug/dbgutil -> diagnostics/src/shared/debug/dbgutil
runtime/src/coreclr/debug/inc/dump/dumpcommon.h -> diagnostics/src/shared/debug/inc/dump/dumpcommon.h
runtime/src/coreclr/debug/inc/dbgutil.h -> diagnostics/src/shared/debug/inc/dbgutil.h
runtime/src/coreclr/debug/inc/dbgtargetcontext.h -> diagnostics/src/shared/debug/inc/dbgtargetcontext.h
runtime/src/coreclr/debug/inc/runtimeinfo.h -> diagnostics/src/shared/inc/debug/runtimeinfo.h
runtime/src/coreclr/gcdump -> diagnostics/src/shared/gcdump
runtime/src/coreclr/gcinfo/gcinfodumper.cpp -> diagnostics/src/shared/gcinfo/gcinfodumper.cpp
runtime/src/coreclr/vm/gcinfodecoder.cpp -> diagnostics/src/shared/vm/gcinfodecoder.cpp
runtime/src/coreclr/gc/gcdesc.h -> diagnostics/src/shared/gc/gcdesc.h
runtime/src/coreclr/dlls/mscorrc/resource.h -> diagnostics/src/shared/inc/resource.h
runtime/src/coreclr/hosts/inc/coreclrhost.h -> diagnostics/src/shared/hosts/inc/coreclrhost.h
runtime/src/native/minipal -> diagnostics/src/shared/native/minipal
runtime/src/coreclr/minipal -> diagnostics/src/shared/minipal
runtime/src/coreclr/pal -> diagnostics/src/shared/pal
runtime/src/coreclr/palrt -> diagnostics/src/shared/palrt
runtime/src/coreclr/utilcode -> diagnostics/src/shared/utilcode

Needed by SOS or dbgshim but not in runtime anymore:

diagnostics/src/shared/pal/inc/rt/intsafe.h
diagnostics/src/shared/pal/inc/rt/psapi.h
diagnostics/src/shared/pal/inc/rt/tchar.h
diagnostics/src/shared/pal/inc/rt/tlhelp32.h
diagnostics/src/shared/pal/inc/rt/winapifamily.h
diagnostics/src/shared/pal/inc/rt/winternl.h
diagnostics/src/shared/pal/inc/rt/winver.h

For swprintf/vswprintf support needed by SOS:

diagnostics/src/shared/pal/src/safecrt/output.inl
diagnostics/src/shared/pal/src/safecrt/safecrt_woutput_s.cpp
diagnostics/src/shared/pal/src/safecrt/swprintf.cpp
diagnostics/src/shared/pal/src/safecrt/vswprint.cpp

diagnostics/src/shared/palrt/bstr.cpp

There are a lot of include and source files that need to be carefully merged from the runtime because 
there is functions that the diagnostics repo doesn't need (i.e. Mutexes) and functions that were removed
from the runtime PAL that the diagnostics repo needs (i.e. RemoveDirectoryA).
