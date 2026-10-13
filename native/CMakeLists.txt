# UpSTEPer native facade: a narrow, C-ABI shared library over Open CASCADE (OCCT).
# See docs/adr/0002-native-interop-c-abi.md for the rationale.
cmake_minimum_required(VERSION 3.24)

project(upsteper_native VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_CXX_VISIBILITY_PRESET hidden)
set(CMAKE_VISIBILITY_INLINES_HIDDEN ON)

find_package(OpenCASCADE CONFIG REQUIRED)

add_library(upsteper_native SHARED
    src/error.cpp
    src/document.cpp
    src/mesh.cpp
    src/check.cpp
    src/repair.cpp
    src/progress.cpp
    src/appearance.cpp
    src/step_export.cpp
    src/messages.cpp
    src/drawing.cpp
    src/edges.cpp
    src/tree.cpp
    src/instances.cpp
    src/version.cpp
)

target_include_directories(upsteper_native
    PUBLIC  ${CMAKE_CURRENT_SOURCE_DIR}/include
    PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/src
            ${OpenCASCADE_INCLUDE_DIR})

target_compile_definitions(upsteper_native PRIVATE
    UPS_BUILDING_LIBRARY
    UPS_VERSION="${PROJECT_VERSION}")

# Link every OCCT toolkit exported by the package config. Listing toolkits by
# hand breaks across OCCT versions (7.8 merged the STEP toolkits into TKDESTEP).
target_link_libraries(upsteper_native PRIVATE ${OpenCASCADE_LIBRARIES})

if(MSVC)
    target_compile_options(upsteper_native PRIVATE /W4 /permissive- /utf-8 /EHsc)
else()
    target_compile_options(upsteper_native PRIVATE -Wall -Wextra)
endif()

# Developer tools (not installed, not built by default): test data generators.
option(UPS_BUILD_TOOLS "Build developer tools such as the test data generators" OFF)
if(UPS_BUILD_TOOLS)
    add_executable(upsteper_make_test_assembly tools/make_test_assembly.cpp)
    target_include_directories(upsteper_make_test_assembly PRIVATE ${OpenCASCADE_INCLUDE_DIR})
    target_link_libraries(upsteper_make_test_assembly PRIVATE ${OpenCASCADE_LIBRARIES})
    add_executable(upsteper_make_damaged_parts tools/make_damaged_parts.cpp)
    target_include_directories(upsteper_make_damaged_parts PRIVATE ${OpenCASCADE_INCLUDE_DIR})
    target_link_libraries(upsteper_make_damaged_parts PRIVATE ${OpenCASCADE_LIBRARIES})
endif()

# Locate OCCT and its dependencies next to our library at runtime.
if(APPLE)
    set_target_properties(upsteper_native PROPERTIES INSTALL_RPATH "@loader_path")
elseif(UNIX)
    set_target_properties(upsteper_native PROPERTIES INSTALL_RPATH "$ORIGIN")
endif()

# `cmake --install` produces a self-contained folder (our library + OCCT and its
# transitive shared libraries) that the .NET projects copy to their output.
set(UPS_DEPENDENCY_DIRS "")
if(DEFINED VCPKG_INSTALLED_DIR AND DEFINED VCPKG_TARGET_TRIPLET)
    list(APPEND UPS_DEPENDENCY_DIRS
        "${VCPKG_INSTALLED_DIR}/${VCPKG_TARGET_TRIPLET}/bin"
        "${VCPKG_INSTALLED_DIR}/${VCPKG_TARGET_TRIPLET}/lib")
endif()

# Normalise paths before matching the exclude patterns (CMake >= 4.x).
if(POLICY CMP0207)
    cmake_policy(SET CMP0207 NEW)
endif()

# The MSVC runtime (vcruntime/msvcp) is not bundled: it comes from the VC++ Redistributable,
# which the installer will require (see docs/roadmap.md, packaging).
install(TARGETS upsteper_native
    RUNTIME_DEPENDENCIES
        PRE_EXCLUDE_REGEXES "api-ms-.*" "ext-ms-.*"
            "[Vv][Cc][Rr][Uu][Nn][Tt][Ii][Mm][Ee].*" "[Mm][Ss][Vv][Cc][Pp].*" "[Cc][Oo][Nn][Cc][Rr][Tt].*"
        POST_EXCLUDE_REGEXES
            ".*[Ss]ystem32/.*"
            "^/lib/.*" "^/lib64/.*" "^/usr/lib/.*" "^/usr/lib64/.*"
            "^/System/Library/.*" "^/usr/lib/system/.*"
        DIRECTORIES ${UPS_DEPENDENCY_DIRS}
    RUNTIME DESTINATION .
    LIBRARY DESTINATION .
    FRAMEWORK DESTINATION .   # required on macOS when RUNTIME_DEPENDENCIES is used
    ARCHIVE DESTINATION lib)
