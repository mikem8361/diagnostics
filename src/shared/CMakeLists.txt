include_directories(${CLR_SRC_NATIVE_DIR})

if (CLR_CMAKE_HOST_UNIX)
  add_subdirectory(pal)
endif(CLR_CMAKE_HOST_UNIX)

include(shared.cmake)

add_subdirectory(inc)
add_subdirectory(debug)
add_subdirectory(minipal)

if (CLR_CMAKE_HOST_UNIX)
  # Include the dummy c++ include files
  include_directories(${CLR_SHARED_DIR}/pal/inc/rt/cpp)

  # This prevents inclusion of standard C compiler headers
  add_compile_options(-nostdinc)

  add_subdirectory(palrt)
endif(CLR_CMAKE_HOST_UNIX)

add_subdirectory(utilcode)
