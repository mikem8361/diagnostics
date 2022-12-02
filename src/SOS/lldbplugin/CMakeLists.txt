project(sosplugin)

set(CMAKE_INCLUDE_CURRENT_DIR ON)

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
