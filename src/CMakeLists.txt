add_subdirectory(shared)
add_subdirectory(SOS)

if (CLR_CMAKE_HOST_UNIX)
  # Include the dummy c++ include files
  include_directories(${CLR_SHARED_DIR}/pal/inc/rt/cpp)

  # This prevents inclusion of standard C compiler headers
  add_compile_options(-nostdinc)
endif(CLR_CMAKE_HOST_UNIX)

add_subdirectory(dbgshim)
