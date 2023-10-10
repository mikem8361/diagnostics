include_directories(${CLR_SRC_NATIVE_DIR})
include_directories(${CLR_SHARED_DIR}/pal/prebuilt/inc)

#--------------------------------
# Definition directives
#  - all clr specific compile definitions should be included in this file
#  - all clr specific feature variable should also be added in this file
#----------------------------------
include(${CLR_SHARED_DIR}/clrdefinitions.cmake)

if (CLR_CMAKE_HOST_UNIX)
  include_directories("${CLR_SHARED_DIR}/pal/inc")
  include_directories("${CLR_SHARED_DIR}/pal/inc/rt")
  include_directories("${CLR_SHARED_DIR}/pal/src/safecrt")
endif (CLR_CMAKE_HOST_UNIX)

include_directories(${CLR_SHARED_DIR}/minipal)
include_directories(${CLR_SHARED_DIR}/debug/inc)
include_directories(${CLR_SHARED_DIR}/debug/inc/dump)
include_directories(${CLR_SHARED_DIR}/hosts/inc)
include_directories(${CLR_SHARED_DIR}/inc)
include_directories(${CLR_SHARED_DIR}/gc)
