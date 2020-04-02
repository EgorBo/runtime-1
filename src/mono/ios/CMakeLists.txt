cmake_minimum_required(VERSION 3.14.5)

project(XunitTestRunner)

file(GLOB TEST_DLLS ${MONO_APPDIR}/*.dll)
file(GLOB TEST_DLLS_AOT ${MONO_APPDIR}/*.dll.o)

set(APP_RESOURCES
    ${TEST_DLLS}
)

add_executable(
    XunitTestRunner
    main.m
    runtime.h
    runtime.m
    modules.m
    ${APP_RESOURCES}
)

if (MONO_ARCH STREQUAL arm64)
    add_definitions(-DDEVICE)
endif()

add_definitions(-DTEST_RUNNER)

include_directories("../../../artifacts/bin/mono/iOS.${MONO_ARCH}.${MONO_CONFIG}/include/mono-2.0")

set_target_properties(XunitTestRunner PROPERTIES
    MACOSX_BUNDLE TRUE
    MACOSX_BUNDLE_INFO_PLIST ${CMAKE_CURRENT_SOURCE_DIR}/Plist.in
    XCODE_ATTRIBUTE_ENABLE_BITCODE "NO"
    XCODE_ATTRIBUTE_DEAD_CODE_STRIPPING "NO"
    RESOURCE "${APP_RESOURCES}"
)

# FIXME: `XCODE_ATTRIBUTE_DEAD_CODE_STRIPPING` should not be NO

target_link_libraries(
    XunitTestRunner
    "-framework Foundation"
    "-framework Security"
    "-framework UIKit"
    "-framework GSS"
    "-lz"
    "-liconv"
    "-force_load ../../../artifacts/bin/mono/iOS.${MONO_ARCH}.${MONO_CONFIG}/libmono.a"
    "-force_load ../../../artifacts/bin/native/netcoreapp5.0-iOS-${MONO_CONFIG}-${MONO_ARCH}/libSystem.IO.Compression.Native.a"
    "-force_load ../../../artifacts/bin/native/netcoreapp5.0-iOS-${MONO_CONFIG}-${MONO_ARCH}/libSystem.Native.a"
    "-force_load ../../../artifacts/bin/native/netcoreapp5.0-iOS-${MONO_CONFIG}-${MONO_ARCH}/libSystem.Net.Security.Native.a"
    "-force_load ../../../artifacts/bin/native/netcoreapp5.0-iOS-${MONO_CONFIG}-${MONO_ARCH}/libSystem.Security.Cryptography.Native.Apple.a"
    ${TEST_DLLS_AOT}
)