project(sosplugin)

set(CMAKE_INCLUDE_CURRENT_DIR ON)

if(CLR_CMAKE_HOST_ARCH_AMD64)
    add_definitions(-D_TARGET_AMD64_=1)
    add_definitions(-DDBG_TARGET_64BIT=1)
    add_definitions(-DDBG_TARGET_AMD64=1)
    add_definitions(-DDBG_TARGET_WIN64=1)
    add_definitions(-DBIT64)
elseif(CLR_CMAKE_HOST_ARCH_I386)
    add_definitions(-D_TARGET_X86_=1)
    add_definitions(-DDBG_TARGET_32BIT=1)
    add_definitions(-DDBG_TARGET_X86=1)
elseif(CLR_CMAKE_HOST_ARCH_ARM)
    add_definitions(-D_TARGET_ARM_=1)
    add_definitions(-DDBG_TARGET_32BIT=1)
    add_definitions(-DDBG_TARGET_ARM=1)
elseif(CLR_CMAKE_HOST_ARCH_ARM64)
    add_definitions(-D_TARGET_ARM64_=1)
    add_definitions(-DDBG_TARGET_64BIT=1)
    add_definitions(-DDBG_TARGET_ARM64=1)
    add_definitions(-DDBG_TARGET_WIN64=1)
    add_definitions(-DBIT64)
elseif(CLR_CMAKE_HOST_ARCH_MIPS64)
    add_definitions(-D_TARGET_MIPS64_=1)
    add_definitions(-DDBG_TARGET_64BIT=1)
    add_definitions(-DDBG_TARGET_MIPS64=1)
    add_definitions(-DDBG_TARGET_WIN64=1)
    add_definitions(-DBIT64)
    SET(REQUIRE_LLDBPLUGIN false)
elseif(CLR_CMAKE_HOST_ARCH_RISCV64)
    add_definitions(-D_TARGET_RISCV64_=1)
    add_definitions(-DDBG_TARGET_64BIT=1)
    add_definitions(-DDBG_TARGET_RISCV64=1)
    add_definitions(-DDBG_TARGET_WIN64=1)
    add_definitions(-DBIT64)
    SET(REQUIRE_LLDBPLUGIN false)
endif()

include(../lldb.cmake)

if(NOT ENABLE_LLDBPLUGIN)
    return()
endif()

add_compile_options(-Wno-delete-non-virtual-dtor)

include_directories(${ROOT_DIR}/src/SOS/inc)
include_directories(${ROOT_DIR}/src/SOS/extensions)
include_directories("${LLDB_H}")

set(SOURCES
    sosplugin.cpp
    soscommand.cpp
    sethostruntimecommand.cpp
    setsostidcommand.cpp
    services.cpp
)

set(LIBRARIES
    extensions
)

if(NOT ${LLDB_LIB} STREQUAL "")
    list(APPEND LIBRARIES ${LLDB_LIB})
endif()

add_library_clr(sosplugin SHARED ${SOURCES})

target_link_libraries(sosplugin ${LIBRARIES})

# add the install targets
install_clr(TARGETS sosplugin DESTINATIONS .)
